using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NuclearReMind
{
    /// <summary>
    /// Cheap organic flicker for fire-ish Light2D (reactor glow, window embers, candles).
    /// Perlin-noise based — non-repeating, per-instance phase so lights never sync.
    /// Visual layer only (RENDER_PLAN §7.1). Spawners that clone a template light
    /// should set <see cref="baseIntensity"/> after overriding the light's intensity.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class LightFlicker : MonoBehaviour
    {
        [Tooltip("Resting intensity the flicker dips from")]
        public float baseIntensity = 1f;
        [Range(0f, 1f)] public float amplitude = 0.15f; // how deep it dips
        [Range(0.1f, 30f)] public float speed = 6f;     // flicker frequency
        [Range(1f, 30f)] public float smooth = 12f;     // response smoothing

        private Light2D _light;
        private float _seed;
        private float _current;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            _seed = Random.value * 100f; // unique phase per instance
            _current = baseIntensity;
        }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * speed);
            float target = baseIntensity - amplitude * (1f - n);
            _current = Mathf.Lerp(_current, target, Time.deltaTime * smooth);
            _light.intensity = _current;
        }
    }
}
