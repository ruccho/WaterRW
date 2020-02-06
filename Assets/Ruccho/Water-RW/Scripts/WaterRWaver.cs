using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Ruccho
{
    [RequireComponent(typeof(MeshRenderer)), RequireComponent(typeof(MeshFilter))]
    public class WaterRWaver : MonoBehaviour
    {
        [SerializeField]
        private int resolution = 20;

        [SerializeField]
        private float waveConstant = 1.0f;

        [SerializeField]
        private float interactMultiplier = 0.2f;
        [SerializeField]
        private float interactHorizontalMultiplier = 1.0f;
        [SerializeField]
        private LayerMask layersToInteract;
        [SerializeField]
        private int waveUpdateLoop;
        [SerializeField]
        private float decay = 0.998f;

        private int bufferLength;
        private readonly int bufferCount = 4;
        private NativeArray<float>[] waveBuffer;
        private int currentBuffer;

        private NativeArray<Vector3> Vertices;
        private JobHandle deformWaveJobHandles;
        private JobHandle updateWaveJobHandles;

        private Mesh mesh;

        private void Start()
        {
            Setup();
        }

        private void OnDisable()
        {
            deformWaveJobHandles.Complete();
            Vertices.Dispose();
            for (int i = 0; i < waveBuffer.Length; i++)
            {
                waveBuffer[i].Dispose();
            }
        }

        private void Setup()
        {
            bufferLength = Mathf.RoundToInt(transform.localScale.x * resolution);
            mesh = BuildMesh(bufferLength - 1);
            GetComponent<MeshFilter>().sharedMesh = mesh;

            List<Vector3> vlist = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vlist);
            Vertices = new NativeArray<Vector3>(vlist.ToArray(), Allocator.Persistent);

            waveBuffer = new NativeArray<float>[bufferCount];
            for (int i = 0; i < waveBuffer.Length; i++)
            {
                waveBuffer[i] = new NativeArray<float>(bufferLength, Allocator.Persistent);
            }
        }

        private static Mesh BuildMesh(int divisions)
        {
            if (divisions < 1) divisions = 1;
            divisions++;
            Vector3[] vertices = new Vector3[divisions * 2];
            Vector2[] uvs = new Vector2[divisions * 2];
            int[] tris = new int[3 * 2 * (divisions - 1)];
            Vector3 origin = new Vector3(-0.5f, -0.5f, 0f);

            for (int i = 0; i < divisions; i++)
            {
                float r = (float)i / (divisions - 1);
                vertices[i * 2] = origin + new Vector3(r, 0f);
                vertices[i * 2 + 1] = origin + new Vector3(r, 1f);
                uvs[i * 2] = new Vector2(r, 0f);
                uvs[i * 2 + 1] = new Vector2(r, 1f);
            }

            for (int i = 0; i < divisions - 1; i++)
            {
                tris[i * 6 + 0] = i * 2;
                tris[i * 6 + 1] = i * 2 + 3;
                tris[i * 6 + 2] = i * 2 + 1;
                tris[i * 6 + 3] = i * 2;
                tris[i * 6 + 4] = i * 2 + 2;
                tris[i * 6 + 5] = i * 2 + 3;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private RaycastHit2D[] hits = new RaycastHit2D[20];
        private void Update()
        {
            int hitCount = Physics2D.LinecastNonAlloc((Vector2)transform.position + transform.lossyScale * new Vector2(-0.5f, 0.5f), (Vector2)transform.position + transform.lossyScale * new Vector2(0.5f, 0.5f), hits, layersToInteract);

            for (int loop = 0; loop < waveUpdateLoop; loop++)
            {
                currentBuffer++;
                currentBuffer %= bufferCount;

                UpdateBuffer();

                for (int i = 0; i < hitCount; i++)
                {
                    var hit = hits[i];
                    var otherRigidbody = hit.rigidbody;
                    if (otherRigidbody == null) continue;
                    var otherTransform = hit.transform;
                    var width = hit.collider.bounds.size.x * 0.5f;
                    float otherHeight = hit.collider.bounds.size.y;
                    float otherCenterHeight = hit.collider.bounds.center.y - (transform.position.y + 0.5f * transform.lossyScale.y);
                    float upperLength = otherHeight * 0.5f + otherCenterHeight;
                    float lowerLength = otherHeight * 0.5f - otherCenterHeight;

                    var center = hit.collider.bounds.min.x + width;
                    
                    float centerLocal = (center - transform.position.x) / transform.lossyScale.x * 2f;

                    int bufferCenter = Mathf.FloorToInt(bufferLength * (centerLocal * 0.5f + 0.5f));
                    int bufferWidth = Mathf.FloorToInt(width / transform.lossyScale.x * bufferLength);

                    for (int b = bufferCenter - bufferWidth; b <= bufferCenter + bufferWidth; b++)
                    {
                        if (b < 0 || b >= bufferLength) continue;
                        waveBuffer[currentBuffer][b] = otherRigidbody.velocity.y * interactMultiplier / width;
                        if (b < bufferCenter)
                        {
                            waveBuffer[currentBuffer][b] += interactHorizontalMultiplier * (-otherRigidbody.velocity.x) * 2.0f;
                        }
                        else if (b > bufferCenter)
                        {
                            waveBuffer[currentBuffer][b] += interactHorizontalMultiplier * (otherRigidbody.velocity.x);
                        }

                        waveBuffer[currentBuffer][b] = Mathf.Clamp(waveBuffer[currentBuffer][b], -lowerLength * 0.5f, upperLength * 0.5f);
                    }
                }
            }

            DeformMesh();

        }

        private void UpdateBuffer()
        {
            // buffer to write
            int b0 = currentBuffer;
            // buffers to read
            int b1 = (currentBuffer - 1 + bufferCount) % bufferCount;
            int b2 = (currentBuffer - 2 + bufferCount) % bufferCount;


            updateWaveJobHandles = new UpdateWaveJob
            {
                currentBuffer = waveBuffer[b0],
                prevBuffer = waveBuffer[b1],
                prevPrevBuffer = waveBuffer[b2],
                waveConstant = waveConstant,
                decay = decay

            }.Schedule(waveBuffer[b0].Length, 0);
            JobHandle.ScheduleBatchedJobs();

            updateWaveJobHandles.Complete();
            /*
            for (int i = 0; i < bufferLength; i++)
            {
                float w0b0 = waveBuffer[b0][i];
                float w0b1 = waveBuffer[b1][i];
                float wlb1 = (i - 1 >= 0 && i - 1 < bufferLength ? waveBuffer[b1][i - 1] : 0);
                float wrb1 = (i + 1 >= 0 && i + 1 < bufferLength ? waveBuffer[b1][i + 1] : 0);
                float w0b2 = waveBuffer[b2][i];
                waveBuffer[b0][i] = 2 * w0b1 - w0b2 + waveConstant * (wrb1 + wlb1 - 2 * w0b1);
                waveBuffer[b0][i] *= 0.92f;
            }
            */
        }

        private void DeformMesh()
        {
            //deform
            deformWaveJobHandles.Complete();

            // メッシュの更新を反映
#if UNITY_2019_3_OR_NEWER
            mesh.SetVertices(Vertices);
#else
            mesh.vertices = Vertices.ToArray();
#endif

            // メッシュを更新
            deformWaveJobHandles = new DeformWaveJob
            {
                vertices = Vertices,
                buffer = waveBuffer[currentBuffer],
                multiplier = 0.2f
            }.Schedule(Vertices.Length, 0);
            JobHandle.ScheduleBatchedJobs();
        }

        [BurstCompile]
        public struct DeformWaveJob : IJobParallelFor
        {
            public NativeArray<Vector3> vertices;
            [ReadOnly]
            public NativeArray<float> buffer;
            public float multiplier;

            public void Execute(int index)
            {
                if (index % 2 == 0) return;
                var v = vertices[index];
                v.y = 0.5f + buffer[index / 2] * multiplier;
                vertices[index] = v;
            }
        }

        [BurstCompile]
        public struct UpdateWaveJob : IJobParallelFor
        {
            public NativeArray<float> currentBuffer;
            [ReadOnly]
            public NativeArray<float> prevBuffer;
            [ReadOnly]
            public NativeArray<float> prevPrevBuffer;

            public float waveConstant;
            public float decay;

            public void Execute(int index)
            {
                int bufferLength = currentBuffer.Length;
                float w0b1 = prevBuffer[index];
                float wlb1 = (index - 1 >= 0 && index - 1 < bufferLength ? prevBuffer[index - 1] : 0);
                float wrb1 = (index + 1 >= 0 && index + 1 < bufferLength ? prevBuffer[index + 1] : 0);
                float w0b2 = prevPrevBuffer[index];
                currentBuffer[index] = 2 * w0b1 - w0b2 + waveConstant * (wrb1 + wlb1 - 2 * w0b1);
                currentBuffer[index] *= decay;
            }
        }
    }
}