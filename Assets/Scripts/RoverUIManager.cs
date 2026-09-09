using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// Manages all UI elements for displaying rover attributes
    /// Comm bars now show BOTH comm range AND signal tower proximity
    /// </summary>
    public class RoverUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private SignalTower signalTower; // NEW: Reference to tower

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

        [Header("Communication UI - Signal Bars Style")]
        [SerializeField] private SignalBarsUI commSignalBars;
        [SerializeField] private Image commStatusLight;
        [SerializeField] private TextMeshProUGUI commPercentageText;
        [SerializeField] private TextMeshProUGUI commLabelText; // NEW: To change label text
        [SerializeField] private Gradient commStatusGradient;
        [SerializeField] private bool showTowerSignal = true; // Toggle between modes
        [SerializeField] private float maxTowerSignalDistance = 100f;

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
        [SerializeField] private bool forceUpdateEveryFrame = true;
        [SerializeField] private float updateSpeed = 10f;
        [SerializeField] private bool smoothTransitions = true;
        [SerializeField] private bool pulseWarningLight = true;
        [SerializeField] private float pulseSpeed = 2f;

        // Target values for smooth transitions
        private float targetPowerFill;
        private float targetSpeedFill;
        private float targetHeatFill;
        private float targetCargoFill;

        // For pulsing warning light
        private float warningPulseTime;

        private void Awake()
        {
            if (roverAttributes == null)
            {
                roverAttributes = FindObjectOfType<RoverAttributeManager>();
            }
            
            if (signalTower == null)
            {
                signalTower = FindObjectOfType<SignalTower>();
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
                Debug.LogError("RoverAttributeManager not found!");
                enabled = false;
                return;
            }

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

            if (forceUpdateEveryFrame)
            {
                UpdateAllUI();
            }

            if (smoothTransitions)
            {
                SmoothUpdateBars();
            }

            UpdateDayNightUI();
            
            // Update comm bars for tower signal
            if (showTowerSignal && signalTower != null)
            {
                UpdateTowerSignalUI();
            }

            if (warningPanel != null && warningPanel.activeSelf && pulseWarningLight && warningStatusLight != null)
            {
                warningPulseTime += Time.deltaTime * pulseSpeed;
                float alpha = (Mathf.Sin(warningPulseTime) + 1f) * 0.5f;
                Color warningColor = UIPalette.Danger;
                warningColor.a = Mathf.Lerp(0.3f, 1f, alpha);
                warningStatusLight.color = warningColor;
            }
        }

        private void UpdateAllUI()
        {
            UpdatePowerUI(roverAttributes.CurrentPower, roverAttributes.MaxPower);
            UpdateSpeedUI(roverAttributes.SpeedMultiplier);
            UpdateHeatUI(roverAttributes.CurrentHeat, roverAttributes.MaxHeat);
            
            if (!showTowerSignal || signalTower == null || signalTower.IsFullyActivated)
            {
                // Show normal comm range when tower is active or not in tower mode
                UpdateCommRangeUI(roverAttributes.CurrentCommunicationRange, roverAttributes.MaxCommunicationRange);
            }
            
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

            if (powerPercentageText != null)
            {
                powerPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            if (powerStatusLight != null && powerStatusGradient != null)
                powerStatusLight.color = powerStatusGradient.Evaluate(normalizedPercentage);

            if (powerFillBar != null && powerStatusGradient != null)
                powerFillBar.color = powerStatusGradient.Evaluate(normalizedPercentage);
        }

        private void UpdateSpeedUI(float multiplier)
        {
            float normalizedSpeed = Mathf.Clamp01(multiplier / 2f);
            float percentage = normalizedSpeed * 100f;
            
            targetSpeedFill = normalizedSpeed;

            if (!smoothTransitions && speedFillBar != null)
                speedFillBar.fillAmount = normalizedSpeed;

            if (speedPercentageText != null)
            {
                speedPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            if (speedStatusLight != null && speedStatusGradient != null)
                speedStatusLight.color = speedStatusGradient.Evaluate(normalizedSpeed);

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

            if (heatPercentageText != null)
            {
                heatPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            if (heatStatusLight != null && heatStatusGradient != null)
                heatStatusLight.color = heatStatusGradient.Evaluate(normalizedPercentage);

            if (heatFillBar != null && heatStatusGradient != null)
                heatFillBar.color = heatStatusGradient.Evaluate(normalizedPercentage);
        }

        private void UpdateCommRangeUI(float current, float max)
        {
            float percentage = (current / max) * 100f;
            float normalizedPercentage = current / max;

            if (commPercentageText != null)
            {
                commPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            if (commSignalBars != null)
            {
                commSignalBars.UpdateSignalStrength(normalizedPercentage);
            }

            if (commStatusLight != null && commStatusGradient != null)
                commStatusLight.color = commStatusGradient.Evaluate(normalizedPercentage);
            
            // Update label to show it's comm range
            if (commLabelText != null)
            {
                commLabelText.text = "COMM";
            }
        }

        // NEW: Update comm bars based on tower distance
        private void UpdateTowerSignalUI()
        {
            if (signalTower == null || commSignalBars == null) return;
            
            // If tower is already active, show normal comm range
            if (signalTower.IsFullyActivated)
            {
                UpdateCommRangeUI(roverAttributes.CurrentCommunicationRange, roverAttributes.MaxCommunicationRange);
                return;
            }

            // Calculate signal strength based on distance to tower
            float distance = signalTower.DistanceToPlayer;
            float signalStrength = 1f - Mathf.Clamp01(distance / maxTowerSignalDistance);
            
            // Update signal bars with tower signal strength
            commSignalBars.UpdateSignalStrength(signalStrength);
            
            // Update percentage text to show distance instead
            if (commPercentageText != null)
            {
                commPercentageText.text = $"{distance:F0}";
            }
            
            // Update status light based on distance
            if (commStatusLight != null && commStatusGradient != null)
            {
                commStatusLight.color = commStatusGradient.Evaluate(signalStrength);
            }
            
            // Update label to show it's tower signal
            if (commLabelText != null)
            {
                commLabelText.text = "TOWER";
            }
        }

        private void UpdateCargoUI(float current, float max)
        {
            float percentage = (current / max) * 100f;
            float normalizedPercentage = current / max;
            
            targetCargoFill = normalizedPercentage;

            if (!smoothTransitions && cargoFillBar != null)
                cargoFillBar.fillAmount = normalizedPercentage;

            if (cargoPercentageText != null)
            {
                cargoPercentageText.text = $"{Mathf.RoundToInt(percentage)}";
            }

            if (cargoStatusLight != null && cargoStatusGradient != null)
                cargoStatusLight.color = cargoStatusGradient.Evaluate(normalizedPercentage);

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
            if (powerStatusGradient == null || powerStatusGradient.colorKeys.Length == 0)
            {
                powerStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(UIPalette.Danger, 0f);
                colorKeys[1] = new GradientColorKey(UIPalette.Warning, 0.5f);
                colorKeys[2] = new GradientColorKey(UIPalette.Power, 1f);
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                powerStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            if (speedStatusGradient == null || speedStatusGradient.colorKeys.Length == 0)
            {
                speedStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(UIPalette.Comm, 0f);
                colorKeys[1] = new GradientColorKey(UIPalette.Comm, 0.5f);
                colorKeys[2] = new GradientColorKey(UIPalette.Success, 1f);
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                speedStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            if (heatStatusGradient == null || heatStatusGradient.colorKeys.Length == 0)
            {
                heatStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(UIPalette.Comm, 0f);
                colorKeys[1] = new GradientColorKey(UIPalette.Warning, 0.5f);
                colorKeys[2] = new GradientColorKey(UIPalette.Heat, 1f);
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                heatStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            if (commStatusGradient == null || commStatusGradient.colorKeys.Length == 0)
            {
                commStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(UIPalette.Danger, 0f);
                colorKeys[1] = new GradientColorKey(UIPalette.Warning, 0.5f);
                colorKeys[2] = new GradientColorKey(UIPalette.Comm, 1f);
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                commStatusGradient.SetKeys(colorKeys, alphaKeys);
            }

            if (cargoStatusGradient == null || cargoStatusGradient.colorKeys.Length == 0)
            {
                cargoStatusGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(UIPalette.Cargo, 0f);
                colorKeys[1] = new GradientColorKey(UIPalette.Cargo, 0.7f);
                colorKeys[2] = new GradientColorKey(UIPalette.Warning, 1f);
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                cargoStatusGradient.SetKeys(colorKeys, alphaKeys);
            }
        }

        #endregion
    }
}