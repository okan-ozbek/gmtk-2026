using UnityEngine;

namespace Environment
{
    public class PointLightFlicker : MonoBehaviour
    {
        [SerializeField] private Light light;
        [SerializeField] private Vector2 flickerRange;
        [SerializeField] private float flickerIntensity = 7f;
        [Range(0f, 1f)]
        [SerializeField] private float flickerChance = 0.05f;
        [SerializeField] private bool flickerEnabled = true;

        private void Update()
        {
            if (!flickerEnabled)
            {
                return;
            }

            var random = Random.Range(0f, 1f);
            if (random <= flickerChance)
            {
                light.intensity = Mathf.Lerp(flickerRange.x, flickerRange.y, Mathf.PerlinNoise(Time.time * flickerIntensity, 0f));    
            }
        }
    }
}