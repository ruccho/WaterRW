using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Ruccho
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class WaterRWCompute : MonoBehaviour
    {
        private static readonly int NumComputeThreads = 1024;

        [SerializeField, Header("References")] private MeshFilter meshFilter;
        [SerializeField] private ComputeShader computeShader;

        [SerializeField, Header("Mesh"), Min(0.001f)]
        private float meshSegmentsPerUnit = 16f;

        [SerializeField, Header("Simulation")] private UpdateModeType updateMode = UpdateModeType.FixedUpdate;

        [SerializeField] private bool overrideFixedTimeStep;
        [SerializeField] private float fixedTimeStep = 0.02f;
        [SerializeField] private bool updateWhenPaused = true;

        [SerializeField, Range(0.05f, 0.95f)] private float c = 0.1f;
        [SerializeField, Range(0f, 1f)] private float decay;
        [SerializeField] private bool enableInteraction = true;
        [SerializeField] private LayerMask layersToInteractWith = 1;
        [SerializeField, Min(0.0001f)] private float spatialScale = 1f;

        [SerializeField] private int maxInteractionItems = 16;
        [SerializeField, Min(0.0001f)] private float waveBufferPixelsPerUnit = 4f;

        [SerializeField] public bool scrollToMainCamera = true;
        [SerializeField] private float flowVelocity;

        [SerializeField, Min(1f), FormerlySerializedAs("maxWaveWidth")]
        private int maxSurfaceWidth = 256;

        private ComputeBuffer interactionBuffer;

        private int currentBuffer;
        private WaveBuffer[] waveBuffers;

        private ref WaveBuffer WaveBufferPrePre => ref waveBuffers[currentBuffer % 3];
        private ref WaveBuffer WaveBufferPre => ref waveBuffers[(currentBuffer + 1) % 3];
        private ref WaveBuffer WaveBufferDest => ref waveBuffers[(currentBuffer + 2) % 3];


        private int numInteractionItems;
        private InteractionItem[] interactionItems;
        private RaycastHit2D[] tempLinecastHits;

        private int? kernelIndex;
        private readonly int kInteractionBuffer = Shader.PropertyToID("_InteractionBuffer");
        private readonly int kNumInteractionItems = Shader.PropertyToID("_NumInteractionItems");

        private readonly int kWaveBufferPixelsPerUnit = Shader.PropertyToID("_WaveBufferPixelsPerUnit");
        private readonly int kWaveBufferPrePre = Shader.PropertyToID("_WaveBufferPrePre");
        private readonly int kWaveBufferPre = Shader.PropertyToID("_WaveBufferPre");
        private readonly int kWaveBufferDest = Shader.PropertyToID("_WaveBufferDest");

        private readonly int kSpatialScale = Shader.PropertyToID("_SpatialScale");
        private readonly int kWaveConstant2 = Shader.PropertyToID("_WaveConstant2");
        private readonly int kDecay = Shader.PropertyToID("_Decay");
        private readonly int kDeltaTime = Shader.PropertyToID("_DeltaTime");

        private readonly int kWavePositionLocal = Shader.PropertyToID("_WavePositionLocal");
        private readonly int kSimulationOffsetPrePre = Shader.PropertyToID("_SimulationOffsetPrePre");
        private readonly int kSimulationOffsetPre = Shader.PropertyToID("_SimulationOffsetPre");

        private float stackedDeltaTime;

        private readonly Dictionary<object, InteractionItem> tempInteractionItems = new();

        private NativeArray<Vertex> vertexBuffer;
        private NativeArray<IndexSegment> indexBuffer;
        private int currentSegments;
        private Mesh mesh;

        private MeshRenderer meshRenderer;
        private Material material;

        public float WavePosition { get; set; }

        private float TimeStep => overrideFixedTimeStep ? fixedTimeStep : Time.fixedDeltaTime;

        private void NextBuffer() => currentBuffer = (currentBuffer + 1) % 3;

        private void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        private void Start()
        {
            WavePosition = transform.position.x;
        }

        private void FixedUpdate()
        {
            if (updateMode != UpdateModeType.FixedUpdate) return;

            UpdateWave(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (updateMode == UpdateModeType.Update)
            {
                UpdateWave(Time.deltaTime);
            }
            else if (updateWhenPaused && Time.timeScale == 0)
            {
                UpdateWave(Time.unscaledDeltaTime);
            }
        }

        private void UpdateWave(float deltaTime)
        {
            if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
            if (!meshFilter) return;

            stackedDeltaTime += deltaTime;
            int numPerform = Mathf.FloorToInt(stackedDeltaTime / TimeStep);
            stackedDeltaTime -= numPerform * TimeStep;

            UpdateWave(numPerform);
        }

        private void UpdateWave(int numPerform)
        {
            //Prepare Instances

            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (material == null) material = meshRenderer.material;

            interactionItems ??= new InteractionItem[maxInteractionItems];
            tempLinecastHits ??= new RaycastHit2D[maxInteractionItems * 2];

            kernelIndex ??= computeShader.FindKernel("Main");

            if (interactionBuffer == null)
            {
                interactionBuffer?.Dispose();
                interactionBuffer = new ComputeBuffer(interactionItems.Length, Marshal.SizeOf<InteractionItem>());
            }

            var waveBufferSizeInPixels = Mathf.RoundToInt(waveBufferPixelsPerUnit * maxSurfaceWidth);

            if (scrollToMainCamera)
            {
                var cam = Camera.main;
                if (cam) WavePosition = cam.transform.position.x;
            }

            if (waveBuffers == null)
            {
                waveBuffers = new WaveBuffer[3];
                for (int i = 0; i < 3; i++)
                {
                    waveBuffers[i] = new WaveBuffer(waveBufferSizeInPixels)
                    {
                        simulationPosition = 0,
                        worldPosition = WavePosition
                    };
                }
            }

            tempInteractionItems.Clear();

            if (enableInteraction)
            {
                // Gather Rigidbodies to interact with

                var transform1 = transform;
                var position = transform1.position;
                var lossyScale = transform1.lossyScale;
                var scaleX = Mathf.Abs(lossyScale.x);
                var scaleY = Mathf.Abs(lossyScale.y);

                var surfaceLeft = position.x - scaleX * 0.5f;
                var waveLeft = WavePosition - maxSurfaceWidth * 0.5f;
                var left = Mathf.Max(surfaceLeft, waveLeft);

                var surfaceRight = position.x + scaleX * 0.5f;
                var waveRight = WavePosition + maxSurfaceWidth * 0.5f;
                var right = Mathf.Min(surfaceRight, waveRight);

                var surfaceHeight = position.y + scaleY * 0.5f;

                if (left < right)
                {
                    var surfaceLeftWorld = new Vector2(left, surfaceHeight);
                    var surfaceRightWorld = new Vector2(right, surfaceHeight);

                    // |→|
                    var numHits = Physics2D.Linecast(surfaceLeftWorld, surfaceRightWorld,
                        new ContactFilter2D
                            { layerMask = layersToInteractWith, useLayerMask = true, useTriggers = true },
                        tempLinecastHits);

                    for (var i = 0; i < numHits; i++)
                    {
                        var hit = tempLinecastHits[i];
                        var rig = hit.rigidbody;
                        object key;
                        Vector2 vel;
                        if (rig)
                        {
                            key = rig;
                            vel = rig.velocity;
                        }
                        else
                        {
                            if (!hit.transform.TryGetComponent(out IWaterRWInteractionProvider provider)) continue;
                            key = provider;
                            vel = provider.Velocity;
                        }

                        var localPoint = hit.point - (Vector2)transform1.position;

                        if (!tempInteractionItems.ContainsKey(key))
                        {
                            tempInteractionItems[key] = new InteractionItem()
                            {
                                startPosition = localPoint.x,
                                endPosition = lossyScale.x * 0.5f,
                                horizontalVelocity = vel.x,
                                verticalVelocity = vel.y
                            };
                        }
                    }

                    // |←|
                    numHits = Physics2D.Linecast(surfaceRightWorld, surfaceLeftWorld,
                        new ContactFilter2D
                            { layerMask = layersToInteractWith, useLayerMask = true, useTriggers = true },
                        tempLinecastHits);

                    for (int i = 0; i < numHits; i++)
                    {
                        var hit = tempLinecastHits[i];
                        var rig = hit.rigidbody;
                        object key;
                        Vector2 vel;
                        if (rig)
                        {
                            key = rig;
                            vel = rig.velocity;
                        }
                        else
                        {
                            if (!hit.transform.TryGetComponent(out IWaterRWInteractionProvider provider)) continue;
                            key = provider;
                            vel = provider.Velocity;
                        }

                        var localPoint = hit.point - (Vector2)transform1.position;

                        if (tempInteractionItems.ContainsKey(key))
                        {
                            var old = tempInteractionItems[key];
                            old.endPosition = localPoint.x;
                            tempInteractionItems[key] = old;
                        }
                        else
                        {
                            tempInteractionItems[key] = new InteractionItem()
                            {
                                startPosition = lossyScale.x * -0.5f,
                                endPosition = localPoint.x,
                                horizontalVelocity = vel.x,
                                verticalVelocity = vel.y
                            };
                        }
                    }
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
            computeShader.SetBuffer(kernelIndex.Value, kInteractionBuffer, interactionBuffer);
            computeShader.SetInt(kNumInteractionItems, numInteractionItems);
            computeShader.SetFloat(kWaveBufferPixelsPerUnit, waveBufferPixelsPerUnit);
            computeShader.SetFloat(kSpatialScale, spatialScale);
            computeShader.SetFloat(kWaveConstant2, c * c);
            computeShader.SetFloat(kDecay, decay);
            computeShader.SetFloat(kDeltaTime, TimeStep);

            for (int i = 0; i < numPerform; i++)
            {
                ref var prePre = ref WaveBufferPrePre;
                ref var pre = ref WaveBufferPre;
                ref var dest = ref WaveBufferDest;

                // Determine current buffer positions.
                // Simulation pixels should be snapped to the nearest integer.
                var expectedWorldPosition = WavePosition;
                var deltaFlow = -TimeStep * flowVelocity;
                var expectedDeltaWorldPosition = expectedWorldPosition - pre.worldPosition;
                var deltaSimulationPositionUnit = deltaFlow + expectedDeltaWorldPosition;
                var deltaSimulationPosition = deltaSimulationPositionUnit * waveBufferPixelsPerUnit;
                var deltaSimulationPositionRounded = Mathf.RoundToInt(deltaSimulationPosition);
                var deltaSimulationPositionRoundedUnit =
                    deltaSimulationPositionRounded / waveBufferPixelsPerUnit;
                var actualDeltaWorldPosition = deltaSimulationPositionRoundedUnit - deltaFlow;
                var actualWorldPosition = actualDeltaWorldPosition + pre.worldPosition;

                dest.worldPosition = actualWorldPosition;
                dest.simulationPosition = pre.simulationPosition + deltaSimulationPositionRounded;

                computeShader.SetFloat(kWavePositionLocal, actualWorldPosition - transform.position.x);
                computeShader.SetInt(kSimulationOffsetPrePre, prePre.simulationPosition - dest.simulationPosition);
                computeShader.SetInt(kSimulationOffsetPre, pre.simulationPosition - dest.simulationPosition);
                computeShader.SetTexture(kernelIndex.Value, kWaveBufferPrePre, WaveBufferPrePre.Buffer);
                computeShader.SetTexture(kernelIndex.Value, kWaveBufferPre, WaveBufferPre.Buffer);
                computeShader.SetTexture(kernelIndex.Value, kWaveBufferDest, WaveBufferDest.Buffer);
                NextBuffer();

                int numGroups = Mathf.CeilToInt((float)waveBufferSizeInPixels / NumComputeThreads);
                computeShader.Dispatch(kernelIndex.Value, numGroups, 1, 1);
            }

            material.mainTexture = WaveBufferPre.Buffer;
            material.SetFloat(kWavePositionLocal, WaveBufferPre.worldPosition - transform.position.x);

            // Mesh

            if (mesh == null)
            {
                mesh = new Mesh();
                GetComponent<MeshFilter>().mesh = mesh;
            }

            float width = Mathf.Abs(transform.lossyScale.x);
            int groups = Mathf.Max(1, (int)(width * meshSegmentsPerUnit) / 4);
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

            material.SetFloat(kWaveBufferPixelsPerUnit, waveBufferPixelsPerUnit);
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
            private NativeArray<Vertex> vertexBuffer;
            private readonly int segments;

            public VertexConstructionJob(NativeArray<Vertex> vertexBuffer, int segments)
            {
                this.vertexBuffer = vertexBuffer;
                this.segments = segments;
            }

            public void Execute(int i)
            {
                float x = (float)(i / 2) / segments;
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
                uint baseIndex = (uint)seg * 2;

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

            for (var i = 0; i < waveBuffers.Length; i++)
            {
                waveBuffers[i].Dispose();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < numInteractionItems; i++)
            {
                var item = interactionItems[i];

                Vector2 p0 = (Vector2)transform.position +
                             new Vector2(item.startPosition, transform.lossyScale.y * 0.5f - 0.5f);
                Vector2 p1 = (Vector2)transform.position +
                             new Vector2(item.endPosition, transform.lossyScale.y * 0.5f - 0.5f);

                Gizmos.DrawLine(p0, p1);
            }

            // surface
            Gizmos.DrawWireCube(new Vector2(WavePosition, transform.position.y),
                new Vector3(maxSurfaceWidth, transform.lossyScale.y, 0));
        }

        [StructLayout(LayoutKind.Sequential)]
        struct InteractionItem
        {
            public float startPosition;
            public float endPosition;
            public float horizontalVelocity;
            public float verticalVelocity;

            private bool Equals(InteractionItem other)
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

        private enum UpdateModeType
        {
            FixedUpdate,
            Update
        }

        private struct WaveBuffer : IDisposable
        {
            public RenderTexture Buffer { get; private set; }

            public float worldPosition;

            public int simulationPosition;

            public WaveBuffer(int length) : this()
            {
                Buffer = new RenderTexture(length, 1, 0, GraphicsFormat.R32_SFloat)
                {
                    enableRandomWrite = true
                };
                Buffer.Create();
            }

            public void Dispose()
            {
                Destroy(Buffer);
                Buffer = default;
            }
        }
    }
}