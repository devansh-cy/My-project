using UnityEngine;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Environment
{
    /// <summary>
    /// Controls dynamic diurnal lighting, sun position, ambient colors, and atmospheric fog
    /// driven by ScenarioDefinition TimeOfDay and WeatherCondition parameters.
    /// </summary>
    [ExecuteAlways]
    public class EnvironmentLightingController : MonoBehaviour
    {
        [Header("Sun & Atmospheric Lights")]
        [SerializeField] private Light sunLight;
        [SerializeField] private TimeOfDay currentTimeOfDay = TimeOfDay.Day;
        [SerializeField] private WeatherCondition currentWeather = WeatherCondition.Clear;

        [Header("Daylight Tuning")]
        [SerializeField] private Color daySunColor = new Color(1.0f, 0.96f, 0.90f);
        [SerializeField] private float daySunIntensity = 1.3f;
        [SerializeField] private Vector3 daySunRotation = new Vector3(50f, -30f, 0f);

        [Header("Dawn / Dusk Tuning")]
        [SerializeField] private Color dawnDuskSunColor = new Color(1.0f, 0.55f, 0.25f);
        [SerializeField] private float dawnDuskSunIntensity = 0.75f;
        [SerializeField] private Vector3 dawnSunRotation = new Vector3(15f, 75f, 0f);
        [SerializeField] private Vector3 duskSunRotation = new Vector3(12f, -105f, 0f);

        [Header("Night Tuning")]
        [SerializeField] private Color nightMoonColor = new Color(0.25f, 0.40f, 0.65f);
        [SerializeField] private float nightMoonIntensity = 0.12f;
        [SerializeField] private Vector3 nightMoonRotation = new Vector3(65f, 150f, 0f);

        [Header("Atmospheric Fog Tuning")]
        [SerializeField] private bool manageFog = true;
        [SerializeField] private Color clearFogColor = new Color(0.70f, 0.82f, 0.95f);
        [SerializeField] private Color denseFogColor = new Color(0.60f, 0.65f, 0.70f);
        [SerializeField] private Color nightFogColor = new Color(0.04f, 0.06f, 0.10f);

        private void Start()
        {
            EnsureSunLight();
            ApplyLighting();
        }

        private void OnValidate()
        {
            EnsureSunLight();
            ApplyLighting();
        }

        public void ApplyScenarioEnvironment(ScenarioDefinition scenario)
        {
            if (scenario == null) return;
            currentTimeOfDay = scenario.timeOfDay;
            currentWeather = scenario.weather;
            ApplyLighting();
        }

        public void ApplyLighting()
        {
            EnsureSunLight();
            if (sunLight == null) return;

            // 1. Apply Sun / Moon Lighting
            switch (currentTimeOfDay)
            {
                case TimeOfDay.Day:
                    sunLight.color = daySunColor;
                    sunLight.intensity = daySunIntensity;
                    sunLight.transform.rotation = Quaternion.Euler(daySunRotation);
                    RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.75f);
                    RenderSettings.ambientEquatorColor = new Color(0.40f, 0.45f, 0.50f);
                    break;

                case TimeOfDay.Dawn:
                    sunLight.color = dawnDuskSunColor;
                    sunLight.intensity = dawnDuskSunIntensity;
                    sunLight.transform.rotation = Quaternion.Euler(dawnSunRotation);
                    RenderSettings.ambientSkyColor = new Color(0.50f, 0.35f, 0.30f);
                    RenderSettings.ambientEquatorColor = new Color(0.30f, 0.25f, 0.25f);
                    break;

                case TimeOfDay.Dusk:
                    sunLight.color = dawnDuskSunColor;
                    sunLight.intensity = dawnDuskSunIntensity;
                    sunLight.transform.rotation = Quaternion.Euler(duskSunRotation);
                    RenderSettings.ambientSkyColor = new Color(0.45f, 0.30f, 0.35f);
                    RenderSettings.ambientEquatorColor = new Color(0.28f, 0.22f, 0.22f);
                    break;

                case TimeOfDay.Night:
                    sunLight.color = nightMoonColor;
                    sunLight.intensity = nightMoonIntensity;
                    sunLight.transform.rotation = Quaternion.Euler(nightMoonRotation);
                    RenderSettings.ambientSkyColor = new Color(0.05f, 0.08f, 0.15f);
                    RenderSettings.ambientEquatorColor = new Color(0.02f, 0.04f, 0.08f);
                    break;
            }

            // 2. Apply Atmospheric Weather Fog
            if (manageFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;

                Color baseFogColor = (currentTimeOfDay == TimeOfDay.Night) ? nightFogColor : clearFogColor;

                switch (currentWeather)
                {
                    case WeatherCondition.DenseFog:
                        RenderSettings.fogColor = (currentTimeOfDay == TimeOfDay.Night) ? nightFogColor * 1.5f : denseFogColor;
                        RenderSettings.fogDensity = 0.015f; // Heavy visibility cutoff around 150m
                        break;

                    case WeatherCondition.HeavyRain:
                        RenderSettings.fogColor = (currentTimeOfDay == TimeOfDay.Night) ? nightFogColor : new Color(0.45f, 0.50f, 0.55f);
                        RenderSettings.fogDensity = 0.008f;
                        break;

                    case WeatherCondition.LightRain:
                    case WeatherCondition.Overcast:
                        RenderSettings.fogColor = baseFogColor;
                        RenderSettings.fogDensity = 0.003f;
                        break;

                    case WeatherCondition.Clear:
                    default:
                        RenderSettings.fogColor = baseFogColor;
                        RenderSettings.fogDensity = 0.0008f; // Light distant atmospheric haze
                        break;
                }
            }
        }

        private void EnsureSunLight()
        {
            if (sunLight == null)
            {
                sunLight = RenderSettings.sun;
                if (sunLight == null)
                {
                    Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    for (int i = 0; i < lights.Length; i++)
                    {
                        if (lights[i].type == LightType.Directional)
                        {
                            sunLight = lights[i];
                            break;
                        }
                    }
                }
            }
        }
    }
}
