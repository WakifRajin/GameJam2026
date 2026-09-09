using System;
using System.Collections;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Manages all rover attributes including power, speed, heat, and other resources.
    /// Uses events to notify other systems when attributes change.
    /// </summary>
    public class RoverAttributeManager : MonoBehaviour
    {
        [Header("Power Settings")]
        [SerializeField] private float maxPower = 100f;
        [SerializeField] private float currentPower = 100f;
        [SerializeField] private float powerDrainPerSecond = 0.5f;
        [SerializeField] private float solarRechargeRate = 2f; // Recharge during day

        [Header("Speed Settings")]
        [SerializeField] private float baseSpeed = 10f;
        [SerializeField] private float currentSpeedMultiplier = 1f;
        [SerializeField] private float maxSpeedMultiplier = 2f;

        [Header("Heat Settings")]
        [SerializeField] private float maxHeat = 100f;
        [SerializeField] private float currentHeat = 50f;
        [SerializeField] private float heatGainPerSecond = 0.3f;
        [SerializeField] private float heatLossPerSecond = 0.5f;
        [SerializeField] private float criticalHeatThreshold = 80f;

        [Header("Communication Settings")]
        [SerializeField] private float maxCommunicationRange = 1000f;
        [SerializeField] private float currentCommunicationRange = 1000f;
        [SerializeField] private float communicationPowerCost = 0.1f; // Power cost per second

        [Header("Cargo Settings")]
        [SerializeField] private float maxCargoCapacity = 50f;
        [SerializeField] private float currentCargoWeight = 0f;

        [Header("Efficiency Multipliers (driven by upgrades)")]
        [Tooltip("Scales solar recharge. 1 = stock, 1.5 = +50% recharge.")]
        [SerializeField] private float solarEfficiency = 1f;
        [Tooltip("Scales power drain. Lower is better - 0.8 = 20% less drain.")]
        [SerializeField] private float powerEfficiency = 1f;
        [Tooltip("Scales heat generated while moving. Lower is better.")]
        [SerializeField] private float heatGenerationMultiplier = 1f;
        [Tooltip("Scales passive cooling while stationary. Higher is better.")]
        [SerializeField] private float coolingEfficiency = 1f;

        [Header("Status")]
        [SerializeField] private bool isDaytime = true;
        [SerializeField] private bool isMoving = false;
        [SerializeField] private bool isCriticalCondition = false;

        // Events for other systems to subscribe to
        public event Action<float, float> OnPowerChanged; // current, max
        public event Action<float> OnSpeedMultiplierChanged;
        public event Action<float, float> OnHeatChanged; // current, max
        public event Action<float, float> OnCommunicationRangeChanged; // current, max
        public event Action<float, float> OnCargoChanged; // current, max
        public event Action OnCriticalCondition;
        public event Action OnPowerDepleted;
        public event Action OnOverheated;

        // Properties for easy access
        public float CurrentPower => currentPower;
        public float MaxPower => maxPower;
        public float PowerPercentage => currentPower / maxPower;
        
        public float CurrentSpeed => baseSpeed * currentSpeedMultiplier;
        public float BaseSpeed => baseSpeed;
        public float SpeedMultiplier => currentSpeedMultiplier;
        
        public float CurrentHeat => currentHeat;
        public float MaxHeat => maxHeat;
        public float HeatPercentage => currentHeat / maxHeat;
        
        public float CurrentCommunicationRange => currentCommunicationRange;
        public float MaxCommunicationRange => maxCommunicationRange;
        
        public float CurrentCargoWeight => currentCargoWeight;
        public float MaxCargoCapacity => maxCargoCapacity;
        public float CargoPercentage => currentCargoWeight / maxCargoCapacity;
        
        public bool IsDaytime => isDaytime;
        public bool IsMoving => isMoving;
        public bool IsCriticalCondition => isCriticalCondition;

        public float SolarEfficiency => solarEfficiency;
        public float PowerEfficiency => powerEfficiency;
        public float HeatGenerationMultiplier => heatGenerationMultiplier;
        public float CoolingEfficiency => coolingEfficiency;
        public float MaxSpeedMultiplier => maxSpeedMultiplier;

        private void Update()
        {
            HandlePowerDrain();
            HandleHeatManagement();
            CheckCriticalConditions();
        }

        #region Power Management

        private void HandlePowerDrain()
        {
            if (currentPower <= 0)
            {
                currentPower = 0;
                OnPowerDepleted?.Invoke();
                return;
            }

            // Drain power based on activity
            float drainAmount = powerDrainPerSecond * powerEfficiency * Time.deltaTime;

            if (isMoving)
            {
                drainAmount *= 1.5f; // Moving costs more power
                drainAmount *= currentSpeedMultiplier; // Higher speed = more drain
            }

            // Solar recharge during daytime
            if (isDaytime && !isMoving)
            {
                drainAmount -= solarRechargeRate * solarEfficiency * Time.deltaTime;
            }

            ModifyPower(-drainAmount);
        }

        public bool ModifyPower(float amount)
        {
            float oldPower = currentPower;
            currentPower = Mathf.Clamp(currentPower + amount, 0, maxPower);
            
            if (Mathf.Abs(oldPower - currentPower) > 0.01f)
            {
                OnPowerChanged?.Invoke(currentPower, maxPower);
                return true;
            }
            return false;
        }

        public bool HasPower(float amount)
        {
            return currentPower >= amount;
        }

        public bool ConsumePower(float amount)
        {
            if (HasPower(amount))
            {
                ModifyPower(-amount);
                return true;
            }
            return false;
        }

        #endregion

        #region Speed Management

        public void SetSpeedMultiplier(float multiplier)
        {
            float oldMultiplier = currentSpeedMultiplier;
            currentSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, maxSpeedMultiplier);
            
            if (Mathf.Abs(oldMultiplier - currentSpeedMultiplier) > 0.01f)
            {
                OnSpeedMultiplierChanged?.Invoke(currentSpeedMultiplier);
            }
        }

        public void ModifySpeedMultiplier(float amount)
        {
            SetSpeedMultiplier(currentSpeedMultiplier + amount);
        }

        public void SetMoving(bool moving)
        {
            isMoving = moving;
        }

        #endregion

        #region Heat Management

        private void HandleHeatManagement()
        {
            float heatChange = 0f;

            if (isMoving)
            {
                // Generate heat while moving
                heatChange += heatGainPerSecond * heatGenerationMultiplier * Time.deltaTime;
                heatChange += heatGainPerSecond * heatGenerationMultiplier * currentSpeedMultiplier * Time.deltaTime * 0.5f;
            }
            else
            {
                // Cool down when stationary
                heatChange -= heatLossPerSecond * coolingEfficiency * Time.deltaTime;
            }

            // Cool down faster at night
            if (!isDaytime)
            {
                heatChange -= heatLossPerSecond * coolingEfficiency * Time.deltaTime * 0.5f;
            }

            ModifyHeat(heatChange);

            // Check overheating
            if (currentHeat >= maxHeat)
            {
                OnOverheated?.Invoke();
            }
        }

        public bool ModifyHeat(float amount)
        {
            float oldHeat = currentHeat;
            currentHeat = Mathf.Clamp(currentHeat + amount, 0, maxHeat);
            
            if (Mathf.Abs(oldHeat - currentHeat) > 0.01f)
            {
                OnHeatChanged?.Invoke(currentHeat, maxHeat);
                return true;
            }
            return false;
        }

        #endregion

        #region Communication Management

        public void SetCommunicationRange(float range)
        {
            float oldRange = currentCommunicationRange;
            currentCommunicationRange = Mathf.Clamp(range, 0, maxCommunicationRange);
            
            if (Mathf.Abs(oldRange - currentCommunicationRange) > 0.01f)
            {
                OnCommunicationRangeChanged?.Invoke(currentCommunicationRange, maxCommunicationRange);
            }
        }

        public void ModifyCommunicationRange(float amount)
        {
            SetCommunicationRange(currentCommunicationRange + amount);
        }

        #endregion

        #region Cargo Management

        public bool AddCargo(float weight)
        {
            if (currentCargoWeight + weight <= maxCargoCapacity)
            {
                SetCargoWeight(currentCargoWeight + weight);
                return true;
            }
            return false;
        }

        public bool RemoveCargo(float weight)
        {
            if (currentCargoWeight >= weight)
            {
                SetCargoWeight(currentCargoWeight - weight);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets cargo weight to an absolute value. The inventory pushes its real total through
        /// here every time it changes, so the two can never drift apart the way +/- deltas do.
        /// </summary>
        public void SetCargoWeight(float weight)
        {
            float clamped = Mathf.Max(0f, weight);
            if (Mathf.Approximately(clamped, currentCargoWeight)) return;

            currentCargoWeight = clamped;
            OnCargoChanged?.Invoke(currentCargoWeight, maxCargoCapacity);
        }

        #endregion

        #region Upgradeable Stats

        public void SetMaxPower(float value)
        {
            maxPower = Mathf.Max(1f, value);
            currentPower = Mathf.Min(currentPower, maxPower);
            OnPowerChanged?.Invoke(currentPower, maxPower);
        }

        /// <summary>Raises the power ceiling and tops the rover up by the same amount.</summary>
        public void AddMaxPower(float amount)
        {
            SetMaxPower(maxPower + amount);
            if (amount > 0f) ModifyPower(amount);
        }

        public void SetMaxCargoCapacity(float value)
        {
            maxCargoCapacity = Mathf.Max(1f, value);
            OnCargoChanged?.Invoke(currentCargoWeight, maxCargoCapacity);
        }

        public void AddMaxCargoCapacity(float amount) => SetMaxCargoCapacity(maxCargoCapacity + amount);

        public void SetMaxHeat(float value)
        {
            maxHeat = Mathf.Max(1f, value);
            currentHeat = Mathf.Min(currentHeat, maxHeat);
            OnHeatChanged?.Invoke(currentHeat, maxHeat);
        }

        public void SetMaxCommunicationRange(float value)
        {
            maxCommunicationRange = Mathf.Max(0f, value);
            OnCommunicationRangeChanged?.Invoke(currentCommunicationRange, maxCommunicationRange);
        }

        /// <summary>Raises the comm ceiling and extends the active range to match.</summary>
        public void AddMaxCommunicationRange(float amount)
        {
            SetMaxCommunicationRange(maxCommunicationRange + amount);
            ModifyCommunicationRange(amount);
        }

        public void SetMaxSpeedMultiplier(float value)
        {
            maxSpeedMultiplier = Mathf.Max(0.1f, value);
            SetSpeedMultiplier(currentSpeedMultiplier); // re-clamp
        }

        public void AddMaxSpeedMultiplier(float amount) => SetMaxSpeedMultiplier(maxSpeedMultiplier + amount);

        public void SetBaseSpeed(float value) => baseSpeed = Mathf.Max(0.1f, value);

        public void SetSolarEfficiency(float value) => solarEfficiency = Mathf.Max(0f, value);
        public void AddSolarEfficiency(float amount) => SetSolarEfficiency(solarEfficiency + amount);

        public void SetPowerEfficiency(float value) => powerEfficiency = Mathf.Max(0.05f, value);
        /// <summary>Positive amount = less drain. Floors at 5% of stock consumption.</summary>
        public void ReducePowerDrain(float fraction) => SetPowerEfficiency(powerEfficiency - fraction);

        public void SetHeatGenerationMultiplier(float value) => heatGenerationMultiplier = Mathf.Max(0f, value);
        /// <summary>Positive amount = less heat produced while moving.</summary>
        public void ReduceHeatGeneration(float fraction) => SetHeatGenerationMultiplier(heatGenerationMultiplier - fraction);

        public void SetCoolingEfficiency(float value) => coolingEfficiency = Mathf.Max(0f, value);
        public void AddCoolingEfficiency(float amount) => SetCoolingEfficiency(coolingEfficiency + amount);

        #endregion

        #region Temporary Modifiers

        /// <summary>
        /// Applies a timed speed boost. Uses unscaled time so a boost bought from a paused
        /// menu does not sit frozen. Stacking calls each run their own timer.
        /// </summary>
        public void ApplyTemporarySpeedBoost(float amount, float duration)
        {
            StartCoroutine(TemporaryStat(
                apply: () => ModifySpeedMultiplier(amount),
                revert: () => ModifySpeedMultiplier(-amount),
                duration: duration));
        }

        public void ApplyTemporaryCooling(float multiplierBonus, float duration)
        {
            StartCoroutine(TemporaryStat(
                apply: () => AddCoolingEfficiency(multiplierBonus),
                revert: () => AddCoolingEfficiency(-multiplierBonus),
                duration: duration));
        }

        public void ApplyTemporaryPowerEfficiency(float fraction, float duration)
        {
            StartCoroutine(TemporaryStat(
                apply: () => ReducePowerDrain(fraction),
                revert: () => ReducePowerDrain(-fraction),
                duration: duration));
        }

        private IEnumerator TemporaryStat(Action apply, Action revert, float duration)
        {
            apply();
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
            revert();
        }

        #endregion

        #region Status Checks

        private void CheckCriticalConditions()
        {
            bool wasCritical = isCriticalCondition;
            
            isCriticalCondition = currentPower < (maxPower * 0.2f) || 
                                  currentHeat > criticalHeatThreshold;

            if (isCriticalCondition && !wasCritical)
            {
                OnCriticalCondition?.Invoke();
            }
        }

        public void SetDaytime(bool daytime)
        {
            isDaytime = daytime;
        }

        #endregion

        #region Debug & Editor

        // For testing in the inspector
        [ContextMenu("Add 20 Power")]
        private void DebugAddPower()
        {
            ModifyPower(20f);
        }

        [ContextMenu("Remove 20 Power")]
        private void DebugRemovePower()
        {
            ModifyPower(-20f);
        }

        [ContextMenu("Increase Speed")]
        private void DebugIncreaseSpeed()
        {
            ModifySpeedMultiplier(0.2f);
        }

        [ContextMenu("Toggle Day/Night")]
        private void DebugToggleDayNight()
        {
            SetDaytime(!isDaytime);
        }

        #endregion
    }
}