using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// Manages all UI elements for displaying rover attributes
    /// </summary>
    public class RoverUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;

        [Header("Power UI")]
        [SerializeField] private Image powerFillBar;
        [SerializeField] private TextMeshProUGUI powerText;
        [SerializeField] private Image powerIcon;
        [SerializeField] private Color powerNormalColor = Color.green;
        [SerializeField] private Color powerWarningColor = Color.yellow;
        [SerializeField] private Color powerCriticalColor = Color.red;

        [Header("Speed UI")]
        [SerializeField] private Image speedFillBar;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private TextMeshProUGUI speedMultiplierText;

        [Header("Heat UI")]
        [SerializeField] private Image heatFillBar;
        [SerializeField] private TextMeshProUGUI heatText;
        [SerializeField] private Color heatNormalColor = Color.blue;
        [SerializeField] private Color heatWarningColor = Color.yellow;
        [SerializeField] private Color heatCriticalColor = Color.red;

        [Header("Communication UI")]
        [SerializeField] private Image commRangeFillBar;
        [SerializeField] private TextMeshProUGUI commRangeText;

        [Header("Cargo UI")]
        [SerializeField] private Image cargoFillBar;
        [SerializeField] private TextMeshProUGUI cargoText;

        [Header("Status UI")]
        [SerializeField] private TextMeshProUGUI dayNightText;
        [SerializeField] private Image dayNightIcon;
        [SerializeField] private Sprite dayIcon;
        [SerializeField] private Sprite nightIcon;
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private TextMeshProUGUI warningText;

        [Header("Animation Settings")]
        [SerializeField] private float updateSpeed = 5f;
        [SerializeField] private bool smoothTransitions = true;

        // Target values for smooth transitions
        private float targetPowerFill;
        private float targetSpeedFill;
        private float targetHeatFill;
        private float targetCommFill;
        private float targetCargoFill;

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
                roverAttributes = FindObjectOfType<RoverAttributeManager>();
                if (roverAttributes == null)
                {
                    Debug.LogError("RoverAttributeManager not found! Please assign it in the inspector.");
                    enabled = false;
                    return;
                }
            }

            SubscribeToEvents();
            InitializeUI();
            
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

        private void InitializeUI()
        {
            UpdatePowerUI(roverAttributes.CurrentPower, roverAttributes.MaxPower);
            UpdateSpeedUI(roverAttributes.SpeedMultiplier);
            UpdateHeatUI(roverAttributes.CurrentHeat, roverAttributes.MaxHeat);
            UpdateCommRangeUI(roverAttributes.CurrentCommunicationRange, roverAttributes.MaxCommunicationRange);
            UpdateCargoUI(roverAttributes.CurrentCargoWeight, roverAttributes.MaxCargoCapacity);
        }

        private void Update()
        {
            // Smooth transitions for bars
            if (smoothTransitions)
            {
                SmoothUpdateBars();
            }

            // Update day/night indicator
            UpdateDayNightUI();
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
            float percentage = current / max;
            targetPowerFill = percentage;

            if (!smoothTransitions && powerFillBar != null)
                powerFillBar.fillAmount = percentage;

            if (powerText != null)
                powerText.text = $"Power: {current:F0}/{max:F0}";

            // Color coding based on power level
            Color color = powerNormalColor;
            if (percentage < 0.2f)
                color = powerCriticalColor;
            else if (percentage < 0.5f)
                color = powerWarningColor;

            if (powerFillBar != null)
                powerFillBar.color = color;
            if (powerIcon != null)
                powerIcon.color = color;
        }

        private void UpdateSpeedUI(float multiplier)
        {
            targetSpeedFill = multiplier / 2f; // Assuming max multiplier is 2

            if (!smoothTransitions && speedFillBar != null)
                speedFillBar.fillAmount = targetSpeedFill;

            float currentSpeed = roverAttributes.CurrentSpeed;
            if (speedText != null)
                speedText.text = $"Speed: {currentSpeed:F1} m/s";

            if (speedMultiplierText != null)
                speedMultiplierText.text = $"x{multiplier:F2}";
        }

        private void UpdateHeatUI(float current, float max)
        {
            float percentage = current / max;
            targetHeatFill = percentage;

            if (!smoothTransitions && heatFillBar != null)
                heatFillBar.fillAmount = percentage;

            if (heatText != null)
                heatText.text = $"Heat: {current:F0}°/{max:F0}°";

            // Color coding based on heat level
            Color color = heatNormalColor;
            if (percentage > 0.8f)
                color = heatCriticalColor;
            else if (percentage > 0.6f)
                color = heatWarningColor;

            if (heatFillBar != null)
                heatFillBar.color = color;
        }

        private void UpdateCommRangeUI(float current, float max)
        {
            float percentage = current / max;
            targetCommFill = percentage;

            if (!smoothTransitions && commRangeFillBar != null)
                commRangeFillBar.fillAmount = percentage;

            if (commRangeText != null)
                commRangeText.text = $"Comm: {current:F0}m/{max:F0}m";
        }

        private void UpdateCargoUI(float current, float max)
        {
            float percentage = current / max;
            targetCargoFill = percentage;

            if (!smoothTransitions && cargoFillBar != null)
                cargoFillBar.fillAmount = percentage;

            if (cargoText != null)
                cargoText.text = $"Cargo: {current:F1}/{max:F1} kg";
        }

        private void UpdateDayNightUI()
        {
            bool isDaytime = roverAttributes.IsDaytime;

            if (dayNightText != null)
                dayNightText.text = isDaytime ? "Day" : "Night";

            if (dayNightIcon != null)
            {
                dayNightIcon.sprite = isDaytime ? dayIcon : nightIcon;
                dayNightIcon.color = isDaytime ? Color.yellow : new Color(0.5f, 0.5f, 1f);
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
                string warningMessage = "WARNING: ";
                if (roverAttributes.PowerPercentage < 0.2f)
                    warningMessage += "LOW POWER! ";
                if (roverAttributes.HeatPercentage > 0.8f)
                    warningMessage += "OVERHEATING! ";

                warningText.text = warningMessage;
            }

            // Auto-hide warning after 3 seconds
            Invoke(nameof(HideWarning), 3f);
        }

        private void HideWarning()
        {
            if (warningPanel != null && !roverAttributes.IsCriticalCondition)
                warningPanel.SetActive(false);
        }

        private void OnPowerDepleted()
        {
            if (warningText != null)
                warningText.text = "POWER DEPLETED! SYSTEMS OFFLINE!";

            if (warningPanel != null)
                warningPanel.SetActive(true);
        }

        private void OnOverheated()
        {
            if (warningText != null)
                warningText.text = "CRITICAL OVERHEAT! SHUTTING DOWN!";

            if (warningPanel != null)
                warningPanel.SetActive(true);
        }

        #endregion
    }
}