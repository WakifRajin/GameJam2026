using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// Manages all UI elements for displaying rover attributes with percentage text,
    /// status bars, and gradient-based status indicator lights
    /// </summary>
    public class RoverUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;

        [Header("Power UI")]
        [SerializeField] private Image powerFillBar;
        [SerializeField] private Image powerStatusLight;
        [SerializeField] private TextMeshProUGUI powerPercentageText;
        [SerializeField] private Gradient powerStatusGradient;

        [Header("Speed UI")]
        [SerializeField] private Image speedFillBar;
        [SerializeField] private Image speedStatusLight;
        [SerializeField] private TextMeshProUGUI speedPercentageText;
        [SerializeField] private Gradient speedStatusGradient;

        [Header("Heat UI")]
        [SerializeField] private Image heatFillBar;
        [SerializeField] private Image heatStatusLight;
        [SerializeField] private TextMeshProUGUI heatPercentageText;
        [SerializeField] private Gradient heatStatusGradient;

        [Header("Communication UI")]
        [SerializeField] private Image commRangeFillBar;
        [SerializeField] private Image commStatusLight;
        [SerializeField] private TextMeshProUGUI commPercentageText;
        [SerializeField] private Gradient commStatusGradient;

        [Header("Cargo UI")]
        [SerializeField] private Image cargoFillBar;
        [SerializeField] private Image cargoStatusLight;
        [SerializeField] private TextMeshProUGUI cargoPercentageText;
        [SerializeField] private Gradient cargoStatusGradient;

        [Header("Day/Night Status")]
        [SerializeField] private TextMeshProUGUI dayNightText;
        [SerializeField] private Image dayNightStatusLight;
        [SerializeField] private Color dayLightColor = new Color(1f, 0.9f, 0.3f);
        [SerializeField] private Color nightLightColor = new Color(0.3f, 0.3f, 0.6f);

        [Header("Warning Panel")]
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private TextMeshProUGUI warningText;
        [SerializeField] private Image warningStatusLight;

        [Header("Update Settings")]
        [SerializeField] private bool forceUpdateEveryFrame = true; // Enable for debugging
        [SerializeField] private float updateSpeed = 10f;
        [SerializeField] private bool smoothTransitions = true;
        [SerializeField] private bool pulseWarningLight = true;
        [SerializeField] private float pulseSpeed = 2f;

        // Target values for smooth transitions
        private float targetPowerFill;
        private float targetSpeedFill;
        private float targetHeatFill;
        private float targetCommFill;
        private float targetCargoFill;

        // For pulsing warning light
        private float warningPulseTime;

        private void Awake()
        {
            // Find rover attributes early
            if (roverAttributes == null)
            {
                roverAttributes = FindObjectOfType<RoverAttributeManager>();
            }

            InitializeGradients();
        }

        private void OnEnable()
        {
            if (roverAttributes != null)
            {
                SubscribeToEvents();
            }
        }

        private void OnDisable()
        {
            if (roverAttributes != null)
            {
                UnsubscribeFromEvents();
            }
        }

        private void Start()
        {
            if (roverAttributes == null)
            {
                Debug.LogError("RoverAttributeManager not found! Please assign it in the inspector.");
                enabled = false;
                return;
            }

            // Force initial update
            UpdateAllUI();
            
            if (warningPanel != null)
                warningPanel.SetActive(false);
        }

        private void SubscribeToEvents()
        {
            roverAttributes.OnPowerChanged += UpdatePowerUI;
            roverAttributes.OnSpeedMultiplierChanged += UpdateSpeedUI;
            roverAttributes.OnHeatChanged += UpdateHeatUI;
            roverAttributes.OnCommunicationRangeChanged += UpdateCommRangeUI;
            roverAttributes.OnCargoChanged += UpdateCargoUI;
            roverAttributes.OnCriticalCondition += ShowWarning;
            roverAttributes.OnPowerDepleted += OnPowerDepleted;
            roverAttributes.OnOverheated += OnOverheated;
        }

        private void UnsubscribeFromEvents()
        {
            roverAttributes.OnPowerChanged -= UpdatePowerUI;
            roverAttributes.OnSpeedMultiplierChanged -= UpdateSpeedUI;
            roverAttributes.OnHeatChanged -= UpdateHeatUI;
            roverAttributes.OnCommunicationRangeChanged -= UpdateCommRangeUI;
            roverAttributes.OnCargoChanged -= UpdateCargoUI;
            roverAttributes.OnCriticalCondition -= ShowWarning;
            roverAttributes.OnPowerDepleted -= OnPowerDepleted;
            roverAttributes.OnOverheated -= OnOverheated;
        }

        private void Update()
        {
            if (roverAttributes == null) return;

            // Force update every frame if enabled (for debugging/testing)
            if (forceUpdateEveryFrame)
            {
                UpdateAllUI();
            }

            // Smooth transitions for bars
            if (smoothTransitions)
            {
                SmoothUpdateBars();
            }

            // Update day/night indicator every frame
            UpdateDayNightUI();

            // Pulse warning light if active
            if (warningPanel != null && warningPanel.activeSelf && pulseWarningLight && warningStatusLight != null)
            {
                warningPulseTime += Time.deltaTime * pulseSpeed;
                float alpha = (Mathf.Sin(warningPulseTime) + 1f) * 0.5f;
                Color warningColor = Color.red;
                warningColor.a = Mathf.Lerp(0.3f, 1f, alpha);
                warningStatusLight.color = warningColor;
            }
        }

        private void UpdateAllUI()
        {
            UpdatePowerUI(roverAttributes.CurrentPower, roverAttributes.MaxPower);
            UpdateSpeedUI(roverAttributes.SpeedMultiplier);
            UpdateHeatUI(roverAttributes.CurrentHeat, roverAttributes.MaxHeat);
            UpdateCommRangeUI(roverAttributes.CurrentCommunicationRange, roverAttributes.MaxCommunicationRange);
            UpdateCargoUI(roverAttributes.CurrentCargoWeight, roverAttributes.MaxCargoCapacity);
        }

        private void SmoothUpdateBars()
        {
            if (powerFillBar != null)
                powerFillBar.fillAmount = Mathf.Lerp(powerFillBar.fillAmount, targetPowerFill, Time.deltaTime * updateSpeed);

            if (speedFillBar != null)
                speedFillBar.fillAmount = Mathf.Lerp(speedFillBar.fillAmount, targetSpeedFill, Time.deltaTime * updateSpeed);

            if (heatFillBar != null)
                heatFillBar.fillAmount = Mathf.Lerp(heatFillBar.fillAmount, targetHeatFill, Time.deltaTime * updateSpeed);

            if (commRangeFillBar != null)
                commRangeFillBar.fillAmount = Mathf.Lerp(commRangeFillBar.fillAmount, targetCommFill, Time.deltaTime * updateSpeed);

            if (cargoFillBar != null)
                cargoFillBar.fillAmount = Mathf.Lerp(cargoFillBar.fillAmount, targetCargoFill, Time.deltaTime * updateSpeed);
        }

        #region UI Update Methods

        private void UpdatePowerUI(float current, float max)
        {
            float percentage = (current / max) * 100f;
            float normalizedPercentage = current / max;
            
            targetPowerFill = normalizedPercentage;

            if (!smoothTransitions && powerFillBar != null)
                powerFillBar.fillAmount = normalizedPercentage;

            // Update percentage text - just the number
            if (powerPercentageText != null)
            {
                powerPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            // Update status light color based on gradient
            if (powerStatusLight != null && powerStatusGradient != null)
                powerStatusLight.color = powerStatusGradient.Evaluate(normalizedPercentage);

            // Also update bar color to match
            if (powerFillBar != null && powerStatusGradient != null)
                powerFillBar.color = powerStatusGradient.Evaluate(normalizedPercentage);
        }

        private void UpdateSpeedUI(float multiplier)
        {
            float normalizedSpeed = Mathf.Clamp01(multiplier / 2f); // Assuming max multiplier is 2
            float percentage = normalizedSpeed * 100f;
            
            targetSpeedFill = normalizedSpeed;

            if (!smoothTransitions && speedFillBar != null)
                speedFillBar.fillAmount = normalizedSpeed;

            // Update percentage text - just the number
            if (speedPercentageText != null)
            {
                speedPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            // Update status light color based on gradient
            if (speedStatusLight != null && speedStatusGradient != null)
                speedStatusLight.color = speedStatusGradient.Evaluate(normalizedSpeed);

            // Also update bar color to match
            if (speedFillBar != null && speedStatusGradient != null)
                speedFillBar.color = speedStatusGradient.Evaluate(normalizedSpeed);
        }

        private void UpdateHeatUI(float current, float max)
        {
            float percentage = (current / max) * 100f;
            float normalizedPercentage = current / max;
            
            targetHeatFill = normalizedPercentage;

            if (!smoothTransitions && heatFillBar != null)
                heatFillBar.fillAmount = normalizedPercentage;

            // Update percentage text - just the number
            if (heatPercentageText != null)
            {
                heatPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            // Update status light color based on gradient
            if (heatStatusLight != null && heatStatusGradient != null)
                heatStatusLight.color = heatStatusGradient.Evaluate(normalizedPercentage);

            // Also update bar color to match
            if (heatFillBar != null && heatStatusGradient != null)
                heatFillBar.color = heatStatusGradient.Evaluate(normalizedPercentage);
        }

        private void UpdateCommRangeUI(float current, float max)
        {
            float percentage = (current / max) * 100f;
            float normalizedPercentage = current / max;
            
            targetCommFill = normalizedPercentage;

            if (!smoothTransitions && commRangeFillBar != null)
                commRangeFillBar.fillAmount = normalizedPercentage;

            // Update percentage text - just the number
            if (commPercentageText != null)
            {
                commPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            // Update status light color based on gradient
            if (commStatusLight != null && commStatusGradient != null)
                commStatusLight.color = commStatusGradient.Evaluate(normalizedPercentage);

            // Also update bar color to match
            if (commRangeFillBar != null && commStatusGradient != null)
                commRangeFillBar.color = commStatusGradient.Evaluate(normalizedPercentage);
        }

        private void UpdateCargoUI(float current, float max)
        {
            float percentage = (current / max) * 100f;
            float normalizedPercentage = current / max;
            
            targetCargoFill = normalizedPercentage;

            if (!smoothTransitions && cargoFillBar != null)
                cargoFillBar.fillAmount = normalizedPercentage;

            // Update percentage text - just the number
            if (cargoPercentageText != null)
            {
                cargoPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            // Update status light color based on gradient
            if (cargoStatusLight != null && cargoStatusGradient != null)
                cargoStatusLight.color = cargoStatusGradient.Evaluate(normalizedPercentage);

            // Also update bar color to match
            if (cargoFillBar != null && cargoStatusGradient != null)
                cargoFillBar.color = cargoStatusGradient.Evaluate(normalizedPercentage);
        }

        private void UpdateDayNightUI()
        {
            bool isDaytime = roverAttributes.IsDaytime;

            if (dayNightText != null)
                dayNightText.text = isDaytime ? "DAY" : "NIGHT";

            if (dayNightStatusLight != null)
            {
                dayNightStatusLight.color = isDaytime ? dayLightColor : nightLightColor;
            }
        }

        #endregion

        #region Warning & Status Methods

        private void ShowWarning()
        {
            if (warningPanel != null)
                warningPanel.SetActive(true);

            if (warningText != null)
            {
                string warningMessage = "⚠ ALERT: ";
                if (roverAttributes.PowerPercentage < 0.2f)
                    warningMessage += "LOW POWER! ";
                if (roverAttributes.HeatPercentage > 0.8f)
                    warningMessage += "OVERHEATING! ";

                warningText.text = warningMessage;
            }

            // Auto-hide warning after 3 seconds if not critical anymore
            CancelInvoke(nameof(CheckAndHideWarning));
            Invoke(nameof(CheckAndHideWarning), 3f);
        }

        private void CheckAndHideWarning()
        {
            if (warningPanel != null && !roverAttributes.IsCriticalCondition)
                warningPanel.SetActive(false);
        }

        private void OnPowerDepleted()
        {
            if (warningText != null)
                warningText.text = "⚠ CRITICAL: POWER DEPLETED!";

            if (warningPanel != null)
                warningPanel.SetActive(true);
        }

        private void OnOverheated()
        {
            if (warningText != null)
                warningText.text = "⚠ CRITICAL: SYSTEM OVERHEATED!";

            if (warningPanel != null)
                warningPanel.SetActive(true);
        }

        #endregion

        #region Gradient Initialization

        private void InitializeGradients()
        {
            // Power gradient: Red (0%) → Yellow (50%) → Green (100%)
            if (powerStatusGradient == null || powerStatusGradient.colorKeys.Length == 0)
            {
                powerStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(Color.red, 0f);
                colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
                colorKeys[2] = new GradientColorKey(Color.green, 1f);

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                powerStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            // Speed gradient: Blue (0%) → Cyan (50%) → Green (100%)
            if (speedStatusGradient == null || speedStatusGradient.colorKeys.Length == 0)
            {
                speedStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(new Color(0.3f, 0.3f, 1f), 0f); // Blue
                colorKeys[1] = new GradientColorKey(Color.cyan, 0.5f);
                colorKeys[2] = new GradientColorKey(Color.green, 1f);

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                speedStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            // Heat gradient: Green (0%) → Yellow (50%) → Red (100%)
            if (heatStatusGradient == null || heatStatusGradient.colorKeys.Length == 0)
            {
                heatStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(Color.green, 0f);
                colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
                colorKeys[2] = new GradientColorKey(Color.red, 1f);

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                heatStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            // Communication gradient: Red (0%) → Orange (50%) → Green (100%)
            if (commStatusGradient == null || commStatusGradient.colorKeys.Length == 0)
            {
                commStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(Color.red, 0f);
                colorKeys[1] = new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f); // Orange
                colorKeys[2] = new GradientColorKey(Color.green, 1f);

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                commStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            // Cargo gradient: Green (0%) → Yellow (70%) → Red (100%)
            if (cargoStatusGradient == null || cargoStatusGradient.colorKeys.Length == 0)
            {
                cargoStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(Color.green, 0f);
                colorKeys[1] = new GradientColorKey(Color.yellow, 0.7f);
                colorKeys[2] = new GradientColorKey(Color.red, 1f);

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                cargoStatusGradient.SetKeys(colorKeys, alphaKeys);
            }
        }

        #endregion

        #region Debug Methods

        [ContextMenu("Force Update UI")]
        private void DebugForceUpdate()
        {
            UpdateAllUI();
            Debug.Log("UI Force Updated!");
        }

        [ContextMenu("Test Power Change")]
        private void DebugTestPower()
        {
            if (roverAttributes != null)
            {
                roverAttributes.ModifyPower(-10f);
                Debug.Log($"Power: {roverAttributes.CurrentPower}");
            }
        }

        #endregion
    }
}