using UnityEngine;

namespace NuclearReMind
{
    /// <summary>
    /// Slow ambient drift for fog/haze sprites (RENDER_PLAN §7.3).
    /// Perlin wander around the spawn position — no allocation, no physics.
    /// Visual layer only.
    /// </summary>
    public class AshfallDrifter : MonoBehaviour
    {
        [Tooltip("Max drift distance from the start position (world units)")]
        public Vector2 amplitude = new Vector2(0.8f, 0.2f);
        [Range(0.005f, 1f)] public float speed = 0.04f;

        private Vector3 _origin;
        private float _seed;

        private void Awake()
        {
            _origin = transform.localPosition;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time * speed;
            float x = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f * amplitude.x;
            float y = (Mathf.PerlinNoise(_seed + 37f, t) - 0.5f) * 2f * amplitude.y;
            transform.localPosition = _origin + new Vector3(x, y, 0f);
        }
    }
}
