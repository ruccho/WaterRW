﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Ruccho
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class WaterRWCompute : MonoBehaviour
    {
        #region Static Members

        private static readonly int NumComputeThreads = 1024;

        #endregion

        #region References

        [SerializeField, Header("References")] private MeshFilter meshFilter = default;
        [SerializeField] private ComputeShader computeShader = default;

        #endregion

        #region Parameters

        [SerializeField, Header("Mesh"), Min(0.001f)]
        private float meshSegmentsPerUnit = 16f;

        [SerializeField, Header("Wave Calculation")]
        private float fixedTimeStep = 0.02f;

        [SerializeField, Range(0.05f, 0.95f)] private float c = 0.1f;
        [SerializeField, Range(0f, 1f)] private float decay = default;
        [SerializeField] private LayerMask layersToInteractWith = 1;
        [SerializeField, Min(0.1f)] private float spatialScale = 1f;

        [SerializeField] private int maxInteractionItems = 16;
        [SerializeField, Min(1f)] private float waveBufferPixelsPerUnit = 4f;

        /// <summary>
        /// Max width of wave area to be calculated (in world space).
        /// </summary>
        [SerializeField, Min(1f)] private int maxWaveWidth = 256;

        #endregion

        #region Variables

        private ComputeBuffer interactionBuffer = default;
        private RenderTexture waveBufferA = default;
        private RenderTexture waveBufferB = default;
        private RenderTexture waveBufferFixed = default;

        private int waveBufferSizeInPixels = 0;

        private int numInteractionItems = 0;
        private InteractionItem[] interactionItems = default;
        private RaycastHit2D[] tempLinecastHits = default;

        private int? kernelIndex = default;
        private int k_InteractionBuffer = Shader.PropertyToID("_InteractionBuffer");
        private int k_NumInteractionItems = Shader.PropertyToID("_NumInteractionItems");

        private int k_WaveBufferPixelsPerUnit = Shader.PropertyToID("_WaveBufferPixelsPerUnit");
        private int k_WaveBufferPrePreDest = Shader.PropertyToID("_WaveBufferPrePreDest");
        private int k_WaveBufferPreSrc = Shader.PropertyToID("_WaveBufferPreSrc");

        private int k_SpatialScale = Shader.PropertyToID("_SpatialScale");
        private int k_WaveConstant2 = Shader.PropertyToID("_WaveConstant2");
        private int k_Decay = Shader.PropertyToID("_Decay");
        private int k_DeltaTime = Shader.PropertyToID("_DeltaTime");

        private float stackedDeltaTime = 0f;

        private readonly Dictionary<Rigidbody2D, InteractionItem> tempInteractionItems =
            new Dictionary<Rigidbody2D, InteractionItem>();

        private bool currentBuffer = false;

        private NativeArray<Vertex> vertexBuffer = default;
        private NativeArray<IndexSegment> indexBuffer = default;
        private int currentSegments = 0;
        private Mesh mesh = default;

        private MeshRenderer meshRenderer = default;
        private Material material = default;

        #endregion

        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        private void Update()
        {
            if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
            if (!meshFilter) return;

            stackedDeltaTime += Time.deltaTime;

            int steps = Mathf.FloorToInt(stackedDeltaTime / fixedTimeStep);
            stackedDeltaTime -= steps * fixedTimeStep;

            UpdateWave(steps);
        }

        private void UpdateWave(int numPerform)
        {
            //Prepare Instances

            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (material == null) material = meshRenderer.material;

            if (interactionItems == null) interactionItems = new InteractionItem[maxInteractionItems];
            if (tempLinecastHits == null) tempLinecastHits = new RaycastHit2D[maxInteractionItems * 2];

            if (!kernelIndex.HasValue) kernelIndex = computeShader.FindKernel("Main");

            if (interactionBuffer == null)
            {
                interactionBuffer?.Dispose();
                interactionBuffer = new ComputeBuffer(interactionItems.Length, Marshal.SizeOf<InteractionItem>());

                computeShader.SetBuffer(kernelIndex.Value, k_InteractionBuffer, interactionBuffer);
            }

            if (waveBufferSizeInPixels == 0)
                waveBufferSizeInPixels = Mathf.RoundToInt(waveBufferPixelsPerUnit * maxWaveWidth);

            if (waveBufferA == null)
            {
                waveBufferA = new RenderTexture(waveBufferSizeInPixels, 1, 0, GraphicsFormat.R32G32B32A32_SFloat);
                waveBufferA.enableRandomWrite = true;
                waveBufferA.Create();
            }

            if (waveBufferB == null)
            {
                waveBufferB = new RenderTexture(waveBufferSizeInPixels, 1, 0, GraphicsFormat.R32G32B32A32_SFloat);
                waveBufferB.enableRandomWrite = true;
                waveBufferB.Create();
            }

            if (waveBufferFixed == null)
            {
                waveBufferFixed = new RenderTexture(waveBufferSizeInPixels, 1, 0, GraphicsFormat.R32G32B32A32_SFloat);
                waveBufferFixed.Create();
                
                material.mainTexture = waveBufferFixed;
            }

            // Gather Rigidbodies to interact with

            var transform1 = transform;
            var position = transform1.position;
            var lossyScale = transform1.lossyScale;
            Vector2 surfaceLeftWorld = (Vector2) position + new Vector2(-0.5f, 0.5f) * lossyScale;
            Vector2 surfaceRightWorld = (Vector2) position + new Vector2(0.5f, 0.5f) * lossyScale;

            tempInteractionItems.Clear();

            // |→|
            int numHits = Physics2D.LinecastNonAlloc(surfaceLeftWorld, surfaceRightWorld, tempLinecastHits,
                layersToInteractWith);

            for (int i = 0; i < numHits; i++)
            {
                var hit = tempLinecastHits[i];
                var rig = hit.rigidbody;
                if (!rig) continue;

                var localPoint = hit.point - (Vector2) transform1.position;
                var vel = rig.velocity;

                if (!tempInteractionItems.ContainsKey(rig))
                {
                    tempInteractionItems[rig] = new InteractionItem()
                    {
                        startPosition = localPoint.x,
                        endPosition = lossyScale.x * 0.5f,
                        horizontalVelocity = vel.x,
                        verticalVelocity = vel.y
                    };
                }
            }

            // |←|
            numHits = Physics2D.LinecastNonAlloc(surfaceRightWorld, surfaceLeftWorld, tempLinecastHits,
                layersToInteractWith);

            for (int i = 0; i < numHits; i++)
            {
                var hit = tempLinecastHits[i];
                var rig = hit.rigidbody;
                if (!rig) continue;

                var localPoint = hit.point - (Vector2) transform1.position;

                if (tempInteractionItems.ContainsKey(rig))
                {
                    var old = tempInteractionItems[rig];
                    old.endPosition = localPoint.x;
                    tempInteractionItems[rig] = old;
                }
                else
                {
                    var vel = rig.velocity;

                    tempInteractionItems[rig] = new InteractionItem()
                    {
                        startPosition = lossyScale.x * -0.5f,
                        endPosition = localPoint.x,
                        horizontalVelocity = vel.x,
                        verticalVelocity = vel.y
                    };
                }
            }

            numInteractionItems = Mathf.Min(interactionItems.Length, tempInteractionItems.Count);

            {
                int i = 0;
                foreach (var item in tempInteractionItems.Values)
                {
                    if (i >= interactionItems.Length) break;
                    interactionItems[i] = item;
                    i++;
                }
            }

            // Compute

            interactionBuffer.SetData(interactionItems);
            computeShader.SetInt(k_NumInteractionItems, numInteractionItems);
            computeShader.SetFloat(k_WaveBufferPixelsPerUnit, waveBufferPixelsPerUnit);
            computeShader.SetFloat(k_SpatialScale, spatialScale);
            computeShader.SetFloat(k_WaveConstant2, c * c);
            computeShader.SetFloat(k_Decay, decay);
            computeShader.SetFloat(k_DeltaTime, fixedTimeStep);

            for (int i = 0; i < numPerform; i++)
            {
                computeShader.SetTexture(kernelIndex.Value, k_WaveBufferPrePreDest,
                    currentBuffer ? waveBufferA : waveBufferB);
                computeShader.SetTexture(kernelIndex.Value, k_WaveBufferPreSrc,
                    currentBuffer ? waveBufferB : waveBufferA);
                currentBuffer = !currentBuffer;

                int numGroups = Mathf.CeilToInt((float) waveBufferSizeInPixels / NumComputeThreads);
                computeShader.Dispatch(kernelIndex.Value, numGroups, 1, 1);
            }

            Graphics.Blit(currentBuffer ? waveBufferB : waveBufferA, waveBufferFixed);

            // Mesh

            if (mesh == null)
            {
                mesh = new Mesh();
                GetComponent<MeshFilter>().mesh = mesh;
            }

            float width = Mathf.Abs(transform.lossyScale.x);
            int groups = Mathf.Max(1, (int) (width * meshSegmentsPerUnit) / 4);
            int segments = groups * 4;
            if (currentSegments != segments)
            {
                int indexBufferLength = segments;
                int vertices = (segments + 1) * 2;
                int vertexBufferLength = vertices;

                if (!vertexBuffer.IsCreated || vertexBuffer.Length < vertexBufferLength)
                {
                    if (vertexBuffer.IsCreated) vertexBuffer.Dispose();
                    vertexBuffer = new NativeArray<Vertex>(vertexBufferLength, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                }

                if (!indexBuffer.IsCreated || indexBuffer.Length < indexBufferLength)
                {
                    if (indexBuffer.IsCreated) indexBuffer.Dispose();
                    indexBuffer = new NativeArray<IndexSegment>(indexBufferLength, Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                }

                // calculate

                var vjob = new VertexConstructionJob(vertexBuffer, segments);
                var ijob = new IndexConstructionJob(indexBuffer);

                var handle = vjob.Schedule(vertexBufferLength, 64);
                handle = ijob.Schedule(indexBufferLength, 64, handle);

                handle.Complete();

                //create

                mesh.SetVertexBufferParams(vertices,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));

                mesh.SetIndexBufferParams(indexBufferLength * 6, IndexFormat.UInt32);

                mesh.SetVertexBufferData(vertexBuffer, 0, 0, vertexBufferLength);
                mesh.SetIndexBufferData(indexBuffer, 0, 0, indexBufferLength);

                var meshDesc = new SubMeshDescriptor(0, indexBufferLength * 6);
                mesh.SetSubMesh(0, meshDesc, MeshUpdateFlags.DontRecalculateBounds);

                mesh.bounds = new Bounds(Vector3.zero, Vector3.one);

                currentSegments = segments;
            }

            material.SetFloat(k_WaveBufferPixelsPerUnit, waveBufferPixelsPerUnit);
        }

        [StructLayout(LayoutKind.Sequential)]
        readonly struct Vertex
        {
            public readonly float2 position;
            public readonly float2 texcoord;

            public Vertex(float2 position, float2 texcoord)
            {
                this.position = position;
                this.texcoord = texcoord;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        readonly struct IndexSegment
        {
            public readonly uint i0, i1, i2, i3, i4, i5;

            public IndexSegment(uint i0, uint i1, uint i2, uint i3, uint i4, uint i5)
            {
                this.i0 = i0;
                this.i1 = i1;
                this.i2 = i2;
                this.i3 = i3;
                this.i4 = i4;
                this.i5 = i5;
            }
        }

        [BurstCompile]
        struct VertexConstructionJob : IJobParallelFor
        {
            NativeArray<Vertex> vertexBuffer;
            int segments;

            public VertexConstructionJob(NativeArray<Vertex> vertexBuffer, int segments)
            {
                this.vertexBuffer = vertexBuffer;
                this.segments = segments;
            }

            public void Execute(int i)
            {
                float x = (float) (i / 2) / segments;
                float y = i % 2 == 0 ? 0 : 1;
                vertexBuffer[i] = new Vertex(new float2(x - 0.5f, y - 0.5f), new float2(x, y));
            }
        }

        [BurstCompile]
        struct IndexConstructionJob : IJobParallelFor
        {
            NativeArray<IndexSegment> indexBuffer;

            public IndexConstructionJob(NativeArray<IndexSegment> indexBuffer)
            {
                this.indexBuffer = indexBuffer;
            }

            public void Execute(int i)
            {
                int seg = i;
                uint baseIndex = (uint) seg * 2;

                indexBuffer[i] = new IndexSegment(
                    baseIndex,
                    baseIndex + 1,
                    baseIndex + 3,
                    baseIndex,
                    baseIndex + 3,
                    baseIndex + 2
                );
            }
        }

        private void OnDestroy()
        {
            interactionBuffer?.Release();
            if (vertexBuffer.IsCreated) vertexBuffer.Dispose();
            if (indexBuffer.IsCreated) indexBuffer.Dispose();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < numInteractionItems; i++)
            {
                var item = interactionItems[i];

                Vector2 p0 = (Vector2) transform.position +
                             new Vector2(item.startPosition, transform.lossyScale.y * 0.5f - 0.5f);
                Vector2 p1 = (Vector2) transform.position +
                             new Vector2(item.endPosition, transform.lossyScale.y * 0.5f - 0.5f);

                Gizmos.DrawLine(p0, p1);
            }
        }

        private void OnGUI()
        {
            if (!waveBufferFixed) return;
            GUI.DrawTexture(new Rect(0, 0, 1024, 20), waveBufferFixed, ScaleMode.StretchToFill, false);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct InteractionItem
        {
            public float startPosition;
            public float endPosition;
            public float horizontalVelocity;
            public float verticalVelocity;

            public bool Equals(InteractionItem other)
            {
                return startPosition.Equals(other.startPosition) && endPosition.Equals(other.endPosition) &&
                       horizontalVelocity.Equals(other.horizontalVelocity) &&
                       verticalVelocity.Equals(other.verticalVelocity);
            }

            public override bool Equals(object obj)
            {
                return obj is InteractionItem other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = startPosition.GetHashCode();
                    hashCode = (hashCode * 397) ^ endPosition.GetHashCode();
                    hashCode = (hashCode * 397) ^ horizontalVelocity.GetHashCode();
                    hashCode = (hashCode * 397) ^ verticalVelocity.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}