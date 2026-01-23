using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Manages the day/night cycle, controlling lighting and time of day
    /// Automatically updates the RoverAttributeManager
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light directionalLight; // The sun
        [SerializeField] private RoverAttributeManager roverAttributes;

        [Header("Time Settings")]
        [SerializeField] private float dayDurationInSeconds = 120f; // 2 minutes for a full day
        [SerializeField] private float startTimeNormalized = 0.25f; // Start at dawn (0 = midnight, 0.5 = noon, 1 = midnight)
        [SerializeField] private bool pauseTime = false;

        [Header("Day/Night Thresholds")]
        [SerializeField] private float sunriseTime = 0.25f; // 6 AM equivalent
        [SerializeField] private float sunsetTime = 0.75f;  // 6 PM equivalent

        [Header("Lighting Settings")]
        [SerializeField] private Gradient lightColorGradient;
        [SerializeField] private AnimationCurve lightIntensityCurve;
        [SerializeField] private float maxLightIntensity = 1.5f;
        [SerializeField] private float minLightIntensity = 0.1f;

        [Header("Ambient Lighting")]
        [SerializeField] private Gradient ambientColorGradient;
        [SerializeField] private bool updateAmbientLight = true;

        [Header("Fog Settings")]
        [SerializeField] private bool updateFog = true;
        [SerializeField] private Gradient fogColorGradient;

        [Header("Visual Rotation")]
        [SerializeField] private bool rotateSun = true;
        [SerializeField] private float sunRotationSpeed = 15f; // Degrees per game hour

        // Current time (0 to 1, where 0.5 is noon)
        private float currentTime;
        private bool wasDay = true;

        private void Start()
        {
            // Find references if not assigned
            if (directionalLight == null)
            {
                directionalLight = FindObjectOfType<Light>();
                if (directionalLight != null && directionalLight.type != LightType.Directional)
                {
                    Debug.LogWarning("Found light is not directional. Day/Night cycle works best with a Directional Light.");
                }
            }

            if (roverAttributes == null)
            {
                roverAttributes = FindObjectOfType<RoverAttributeManager>();
            }

            // Initialize time
            currentTime = startTimeNormalized;

            // Create default gradients if not set
            InitializeDefaultGradients();

            // Initial update
            UpdateCycle();
        }

        private void Update()
        {
            if (pauseTime) return;

            // Progress time
            currentTime += Time.deltaTime / dayDurationInSeconds;
            if (currentTime >= 1f)
            {
                currentTime -= 1f; // Loop back to start of day
            }

            UpdateCycle();
        }

        private void UpdateCycle()
        {
            // Determine if it's day or night
            bool isDay = currentTime >= sunriseTime && currentTime < sunsetTime;

            // Update rover attributes when day/night changes
            if (roverAttributes != null && isDay != wasDay)
            {
                roverAttributes.SetDaytime(isDay);
                wasDay = isDay;
            }

            // Update lighting
            if (directionalLight != null)
            {
                UpdateLighting();
            }

            // Update ambient light
            if (updateAmbientLight)
            {
                UpdateAmbient();
            }

            // Update fog
            if (updateFog && RenderSettings.fog)
            {
                UpdateFog();
            }
        }

        private void UpdateLighting()
        {
            // Update light color
            if (lightColorGradient != null && lightColorGradient.colorKeys.Length > 0)
            {
                directionalLight.color = lightColorGradient.Evaluate(currentTime);
            }

            // Update light intensity
            float intensity = lightIntensityCurve.Evaluate(currentTime);
            directionalLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, intensity);

            // Rotate sun
            if (rotateSun)
            {
                float sunAngle = currentTime * 360f - 90f; // -90 so noon is overhead
                directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
            }
        }

        private void UpdateAmbient()
        {
            if (ambientColorGradient != null && ambientColorGradient.colorKeys.Length > 0)
            {
                RenderSettings.ambientLight = ambientColorGradient.Evaluate(currentTime);
            }
        }

        private void UpdateFog()
        {
            if (fogColorGradient != null && fogColorGradient.colorKeys.Length > 0)
            {
                RenderSettings.fogColor = fogColorGradient.Evaluate(currentTime);
            }
        }

        private void InitializeDefaultGradients()
        {
            // Create default light color gradient if empty
            if (lightColorGradient == null || lightColorGradient.colorKeys.Length == 0)
            {
                lightColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[5];
                colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0f);    // Midnight - dark blue
                colorKeys[1] = new GradientColorKey(new Color(1f, 0.6f, 0.4f), 0.25f);    // Sunrise - orange
                colorKeys[2] = new GradientColorKey(new Color(1f, 0.95f, 0.9f), 0.5f);    // Noon - bright white
                colorKeys[3] = new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.75f);    // Sunset - orange/red
                colorKeys[4] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1f);     // Midnight - dark blue

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                lightColorGradient.SetKeys(colorKeys, alphaKeys);
            }

            // Create default intensity curve if empty
            if (lightIntensityCurve == null || lightIntensityCurve.length == 0)
            {
                lightIntensityCurve = new AnimationCurve();
                lightIntensityCurve.AddKey(0f, 0f);      // Midnight - dark
                lightIntensityCurve.AddKey(0.25f, 0.5f); // Sunrise - medium
                lightIntensityCurve.AddKey(0.5f, 1f);    // Noon - bright
                lightIntensityCurve.AddKey(0.75f, 0.5f); // Sunset - medium
                lightIntensityCurve.AddKey(1f, 0f);      // Midnight - dark
            }

            // Create default ambient gradient if empty
            if (ambientColorGradient == null || ambientColorGradient.colorKeys.Length == 0)
            {
                ambientColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[5];
                colorKeys[0] = new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 0f);    // Midnight
                colorKeys[1] = new GradientColorKey(new Color(0.5f, 0.4f, 0.4f), 0.25f); // Sunrise
                colorKeys[2] = new GradientColorKey(new Color(0.8f, 0.8f, 0.9f), 0.5f);  // Noon
                colorKeys[3] = new GradientColorKey(new Color(0.5f, 0.3f, 0.3f), 0.75f); // Sunset
                colorKeys[4] = new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 1f);    // Midnight

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                ambientColorGradient.SetKeys(colorKeys, alphaKeys);
            }

            // Create default fog gradient if empty
            if (fogColorGradient == null || fogColorGradient.colorKeys.Length == 0)
            {
                fogColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(new Color(0.3f, 0.2f, 0.2f), 0f);    // Night - dark reddish (Mars atmosphere)
                colorKeys[1] = new GradientColorKey(new Color(0.8f, 0.5f, 0.4f), 0.5f);  // Day - Mars atmosphere color
                colorKeys[2] = new GradientColorKey(new Color(0.3f, 0.2f, 0.2f), 1f);    // Night

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                fogColorGradient.SetKeys(colorKeys, alphaKeys);
            }
        }

        #region Public Methods

        /// <summary>
        /// Set the current time (0 to 1, where 0.5 is noon)
        /// </summary>
        public void SetTime(float time)
        {
            currentTime = Mathf.Clamp01(time);
            UpdateCycle();
        }

        /// <summary>
        /// Get the current time (0 to 1)
        /// </summary>
        public float GetCurrentTime()
        {
            return currentTime;
        }

        /// <summary>
        /// Get the current time in hours (0-24)
        /// </summary>
        public float GetCurrentTimeInHours()
        {
            return currentTime * 24f;
        }

        /// <summary>
        /// Check if it's currently daytime
        /// </summary>
        public bool IsDaytime()
        {
            return currentTime >= sunriseTime && currentTime < sunsetTime;
        }

        /// <summary>
        /// Pause or resume the day/night cycle
        /// </summary>
        public void SetPaused(bool paused)
        {
            pauseTime = paused;
        }

        /// <summary>
        /// Set the speed of the day/night cycle
        /// </summary>
        public void SetDayDuration(float seconds)
        {
            dayDurationInSeconds = Mathf.Max(1f, seconds);
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Set to Dawn")]
        private void DebugSetDawn()
        {
            SetTime(0.25f);
        }

        [ContextMenu("Set to Noon")]
        private void DebugSetNoon()
        {
            SetTime(0.5f);
        }

        [ContextMenu("Set to Dusk")]
        private void DebugSetDusk()
        {
            SetTime(0.75f);
        }

        [ContextMenu("Set to Midnight")]
        private void DebugSetMidnight()
        {
            SetTime(0f);
        }

        [ContextMenu("Toggle Pause")]
        private void DebugTogglePause()
        {
            pauseTime = !pauseTime;
        }

        #endregion
    }
}