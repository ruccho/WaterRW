using Unity.Mathematics;
using UnityEngine;

namespace Ruccho
{
    public class WaterRWInteractionProvider : MonoBehaviour, IWaterRWInteractionProvider
    {
        [SerializeField] private float noiseFrequency;
        [SerializeField] private float noiseAmplitude;

#if UNITY_6000_4_OR_NEWER
        private bool hasNoiseOffset;
        private float noiseOffset;

        // Per-instance offset in [0, 100), derived from the entity id without touching UnityEngine.Random.
        private float NoiseOffset
        {
            get
            {
                if (!hasNoiseOffset)
                {
                    var id = EntityId.ToULong(GetEntityId());
                    var hash = math.hash(new uint2((uint)(id >> 32), (uint)id));
                    noiseOffset = (hash & 0xFFFFFFu) / (float)(1 << 24) * 100f;
                    hasNoiseOffset = true;
                }

                return noiseOffset;
            }
        }
#endif

        public Vector2 Velocity
        {
            get
            {
                return new Vector2(0f, (Mathf.PerlinNoise1D(noiseFrequency * Time.fixedTime +
#if UNITY_6000_4_OR_NEWER
                                                            NoiseOffset
#else
                                                            GetInstanceID()
#endif
                                                            * 0.13f) * 2f - 1f) * noiseAmplitude);
            }
        }
    }
}