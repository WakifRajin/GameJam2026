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

        [Tooltip("Ambient light only responds to a gradient in Flat mode; Skybox mode ignores it entirely. Forced on when Update Ambient Light is enabled.")]
        [SerializeField] private bool forceFlatAmbient = true;

        [Header("Fog Settings")]
        [SerializeField] private bool controlFog = true;
        [SerializeField] private bool enableFogAutomatically = true;
        [SerializeField] private Gradient fogColorGradient;
        [SerializeField] private float fogDensity = 0.01f;
        [Tooltip("Density multiplier over the day. Thicker at dawn/dusk, thinner at noon, so nights feel closed-in without fogging out midday.")]
        [SerializeField] private AnimationCurve fogDensityCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField] private float fogStartDistance = 10f;
        [SerializeField] private float fogEndDistance = 100f;

        [Header("Sky")]
        [Tooltip("Drive the procedural skybox so the horizon darkens at night instead of staying lit.")]
        [SerializeField] private bool controlSkybox = true;
        [SerializeField] private Gradient skyTintGradient;
        [SerializeField] private Gradient skyGroundGradient;
        [SerializeField] private AnimationCurve skyExposureCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField] private float maxSkyExposure = 1.15f;
        [SerializeField] private float minSkyExposure = 0.12f;

        [Header("Visual Rotation")]
        [SerializeField] private bool rotateSun = true;

        // Current time (0 to 1, where 0.5 is noon)
        private float currentTime;
        private bool wasDay = true;
        private bool fogWasEnabled = false;

        // Runtime clone of the skybox. Writing to RenderSettings.skybox directly would edit the
        // shared .mat asset on disk and leave it dirty in source control after every play session.
        private Material skyboxInstance;

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

            // Setup fog
            SetupFog();

            SetupSkybox();

            // Initial update
            UpdateCycle();
            
            Debug.Log($"Day/Night Cycle initialized. Fog enabled: {RenderSettings.fog}");
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
                Debug.Log($"Time changed to: {(isDay ? "DAY" : "NIGHT")}");
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
            if (controlFog)
            {
                UpdateFog();
            }

            if (controlSkybox)
            {
                UpdateSkybox();
            }
        }

        private void SetupSkybox()
        {
            if (!controlSkybox || RenderSettings.skybox == null) return;

            skyboxInstance = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (runtime)" };
            RenderSettings.skybox = skyboxInstance;
        }

        private void UpdateSkybox()
        {
            if (skyboxInstance == null) return;

            if (skyTintGradient != null && skyTintGradient.colorKeys.Length > 0 && skyboxInstance.HasProperty("_SkyTint"))
            {
                skyboxInstance.SetColor("_SkyTint", skyTintGradient.Evaluate(currentTime));
            }

            if (skyGroundGradient != null && skyGroundGradient.colorKeys.Length > 0 && skyboxInstance.HasProperty("_GroundColor"))
            {
                skyboxInstance.SetColor("_GroundColor", skyGroundGradient.Evaluate(currentTime));
            }

            if (skyboxInstance.HasProperty("_Exposure"))
            {
                float t = (skyExposureCurve != null && skyExposureCurve.length > 0)
                    ? skyExposureCurve.Evaluate(currentTime)
                    : DefaultDaylightArc(currentTime);
                skyboxInstance.SetFloat("_Exposure", Mathf.Lerp(minSkyExposure, maxSkyExposure, Mathf.Clamp01(t)));
            }
        }


        private void UpdateLighting()
        {
            // Update light color
            if (lightColorGradient != null && lightColorGradient.colorKeys.Length > 0)
            {
                directionalLight.color = lightColorGradient.Evaluate(currentTime);
            }

            // An empty curve evaluates to 0, which used to pin the sun at minLightIntensity
            // all day. Fall back to a real arc rather than silently going dark.
            float intensity = (lightIntensityCurve != null && lightIntensityCurve.length > 0)
                ? lightIntensityCurve.Evaluate(currentTime)
                : DefaultDaylightArc(currentTime);
            directionalLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, Mathf.Clamp01(intensity));

            // Rotate sun
            if (rotateSun)
            {
                float sunAngle = currentTime * 360f - 90f; // -90 so noon is overhead
                directionalLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
            }
        }

        private void UpdateAmbient()
        {
            if (ambientColorGradient == null || ambientColorGradient.colorKeys.Length == 0) return;

            // Skybox ambient mode ignores ambientLight outright, so the gradient did nothing.
            if (forceFlatAmbient && RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Flat)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            }

            RenderSettings.ambientLight = ambientColorGradient.Evaluate(currentTime);
        }

        /// <summary>
        /// Sun brightness over a full cycle: dark through the night, easing up through dawn,
        /// peaking at noon and easing back down. Used when no curve is authored.
        /// </summary>
        private float DefaultDaylightArc(float t)
        {
            if (t <= sunriseTime || t >= sunsetTime) return 0f;

            // 0 at sunrise -> 1 at midday -> 0 at sunset, smoothed at both ends.
            float dayProgress = Mathf.InverseLerp(sunriseTime, sunsetTime, t);
            return Mathf.SmoothStep(0f, 1f, Mathf.Sin(dayProgress * Mathf.PI));
        }

        private void UpdateFog()
        {
            // Make sure fog is enabled
            if (!RenderSettings.fog && enableFogAutomatically)
            {
                RenderSettings.fog = true;
                Debug.Log("Fog was disabled - automatically enabled it");
            }

            if (!RenderSettings.fog) return;

            if (fogColorGradient != null && fogColorGradient.colorKeys.Length > 0)
            {
                RenderSettings.fogColor = fogColorGradient.Evaluate(currentTime);
            }

            // Density used to be constant, so midnight haze matched midday haze.
            float densityScale = (fogDensityCurve != null && fogDensityCurve.length > 0)
                ? Mathf.Max(0f, fogDensityCurve.Evaluate(currentTime))
                : 1f;
            RenderSettings.fogDensity = fogDensity * densityScale;
        }

        private void SetupFog()
        {
            if (!controlFog) return;

            // Store original fog state
            fogWasEnabled = RenderSettings.fog;

            // Enable fog if requested
            if (enableFogAutomatically)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = fogDensity;
                
                // Alternative: Use Linear fog
                // RenderSettings.fogMode = FogMode.Linear;
                // RenderSettings.fogStartDistance = fogStartDistance;
                // RenderSettings.fogEndDistance = fogEndDistance;
                
                Debug.Log("Fog enabled and configured");
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

            // Create default fog gradient if empty - MARS ATMOSPHERE COLORS
            if (fogColorGradient == null || fogColorGradient.colorKeys.Length == 0)
            {
                fogColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[5];
                colorKeys[0] = new GradientColorKey(new Color(0.25f, 0.15f, 0.15f), 0f);    // Night - dark reddish
                colorKeys[1] = new GradientColorKey(new Color(0.9f, 0.6f, 0.4f), 0.25f);    // Sunrise - orange/red
                colorKeys[2] = new GradientColorKey(new Color(0.95f, 0.7f, 0.5f), 0.5f);    // Day - butterscotch
                colorKeys[3] = new GradientColorKey(new Color(0.85f, 0.5f, 0.35f), 0.75f);  // Sunset - red/orange
                colorKeys[4] = new GradientColorKey(new Color(0.25f, 0.15f, 0.15f), 1f);    // Night - dark reddish

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

        public float DayDuration => dayDurationInSeconds;
        public float SunriseTime => sunriseTime;
        public float SunsetTime => sunsetTime;

        /// <summary>
        /// Real seconds until day flips to night or back. <paramref name="nightIsComing"/> is
        /// true when the pending transition is sunset.
        ///
        /// Drives the "can I reach the next relay before dark?" readout: solar only recharges
        /// in daylight, so this countdown is the level's core pressure.
        /// </summary>
        public float SecondsUntilTransition(out bool nightIsComing)
        {
            nightIsComing = IsDaytime();

            float boundary = nightIsComing ? sunsetTime : sunriseTime;
            float remaining = boundary - currentTime;
            if (remaining < 0f) remaining += 1f; // wrap past midnight

            return remaining * dayDurationInSeconds;
        }

        /// <summary>
        /// Set the speed of the day/night cycle
        /// </summary>
        public void SetDayDuration(float seconds)
        {
            dayDurationInSeconds = Mathf.Max(1f, seconds);
        }

        /// <summary>
        /// Force fog to be enabled
        /// </summary>
        public void EnableFog(bool enable)
        {
            RenderSettings.fog = enable;
            if (enable)
            {
                UpdateFog();
                Debug.Log("Fog manually enabled");
            }
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

        [ContextMenu("Enable Fog")]
        private void DebugEnableFog()
        {
            EnableFog(true);
        }

        [ContextMenu("Print Fog Status")]
        private void DebugPrintFogStatus()
        {
            Debug.Log($"=== FOG STATUS ===");
            Debug.Log($"Fog Enabled: {RenderSettings.fog}");
            Debug.Log($"Fog Mode: {RenderSettings.fogMode}");
            Debug.Log($"Fog Color: {RenderSettings.fogColor}");
            Debug.Log($"Fog Density: {RenderSettings.fogDensity}");
            Debug.Log($"Control Fog: {controlFog}");
            Debug.Log($"Enable Fog Automatically: {enableFogAutomatically}");
        }

        #endregion

        private void OnDestroy()
        {
            // Restore original fog state
            if (controlFog && !enableFogAutomatically)
            {
                RenderSettings.fog = fogWasEnabled;
            }

            // Drop the runtime skybox clone so the scene does not keep a dangling material.
            if (skyboxInstance != null)
            {
                if (Application.isPlaying) Destroy(skyboxInstance);
                else DestroyImmediate(skyboxInstance);
                skyboxInstance = null;
            }
        }
    }
}