using UnityEngine;

namespace Ruccho
{
    public class WaterRWInteractionProvider : MonoBehaviour, IWaterRWInteractionProvider
    {
        [SerializeField] private float noiseFrequency;
        [SerializeField] private float noiseAmplitude;

        public Vector2 Velocity => new(0f, (Mathf.PerlinNoise1D(noiseFrequency * Time.fixedTime + GetInstanceID() * 0.13f) * 2f - 1f) * noiseAmplitude);
    }
}