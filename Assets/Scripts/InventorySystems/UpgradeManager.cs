using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameJam2026
{
    public enum UpgradeAvailability
    {
        Available,
        MaxLevel,
        MissingPrerequisite,
        InsufficientResources
    }

    /// <summary>
    /// Owns the player's upgrade levels and actually applies them to the rover.
    ///
    /// Levels live here rather than on the ScriptableObject, so assets stay clean and a run can
    /// be saved and restored. Effects are stored as per-level deltas, so restoring a save just
    /// replays each purchased level.
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private GridInventoryManager inventoryManager;
        [Tooltip("Headlight driven by the LightRange upgrade. Auto-found in children if left empty.")]
        [SerializeField] private Light roverLight;

        [Header("Available Upgrades")]
        [SerializeField] private List<UpgradeRecipe> availableUpgrades = new List<UpgradeRecipe>();

        [Header("Persistence")]
        [SerializeField] private string saveKey = "rover_upgrades";
        [Tooltip("Restore saved levels automatically on Start.")]
        [SerializeField] private bool loadOnStart = false;

        // upgrade id -> level owned
        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        #region Events

        public event Action<UpgradeRecipe> OnUpgradeCompleted;
        public event Action<UpgradeRecipe, string> OnUpgradeFailed;
        /// <summary>Recipe, new level. Also fires when a save is restored.</summary>
        public event Action<UpgradeRecipe, int> OnUpgradeLevelChanged;
        /// <summary>Fired after any change that could alter what is affordable.</summary>
        public event Action OnUpgradesRefreshed;

        #endregion

        private void Awake()
        {
            // This component often lives on a UI object rather than on the rover, so a plain
            // GetComponent is not enough - fall back to a scene lookup.
            if (roverAttributes == null)
            {
                roverAttributes = GetComponent<RoverAttributeManager>() ?? FindObjectOfType<RoverAttributeManager>();
            }

            if (inventoryManager == null)
            {
                inventoryManager = GetComponent<GridInventoryManager>() ?? FindObjectOfType<GridInventoryManager>();
            }

            // The headlight belongs to the rover, not to whatever holds this component.
            if (roverLight == null && roverAttributes != null)
            {
                roverLight = roverAttributes.GetComponentInChildren<Light>();
            }

            if (inventoryManager == null)
            {
                Debug.LogError("UpgradeManager: no GridInventoryManager found - upgrades cannot be paid for.");
            }

            foreach (var upgrade in availableUpgrades)
            {
                if (upgrade == null) continue;
                levels[upgrade.Id] = Mathf.Clamp(upgrade.startingLevel, 0, upgrade.maxLevel);
            }
        }

        private void Start()
        {
            // Starting levels have to take effect too, or a recipe that begins at level 2
            // shows as owned while granting nothing.
            foreach (var upgrade in availableUpgrades)
            {
                if (upgrade == null) continue;
                ReapplyLevels(upgrade, 0, GetUpgradeLevel(upgrade));
            }

            if (loadOnStart) Load();

            OnUpgradesRefreshed?.Invoke();
        }

        private void OnEnable()
        {
            if (inventoryManager != null) inventoryManager.OnResourceChanged += HandleResourceChanged;
        }

        private void OnDisable()
        {
            if (inventoryManager != null) inventoryManager.OnResourceChanged -= HandleResourceChanged;
        }

        private void HandleResourceChanged(string resourceType, float amount) => OnUpgradesRefreshed?.Invoke();

        #region Queries

        public List<UpgradeRecipe> GetAvailableUpgrades() => availableUpgrades;

        public int GetUpgradeLevel(UpgradeRecipe upgrade)
        {
            if (upgrade == null) return 0;
            if (!levels.TryGetValue(upgrade.Id, out int level))
            {
                level = Mathf.Clamp(upgrade.startingLevel, 0, upgrade.maxLevel);
                levels[upgrade.Id] = level;
            }
            return level;
        }

        public bool IsMaxLevel(UpgradeRecipe upgrade) =>
            upgrade != null && GetUpgradeLevel(upgrade) >= upgrade.maxLevel;

        /// <summary>Cost of the next level, aggregated so a recipe listing one resource twice is charged once.</summary>
        public List<UpgradeCost> GetNextLevelCost(UpgradeRecipe upgrade)
        {
            if (upgrade == null) return new List<UpgradeCost>();

            var raw = upgrade.GetCostForNextLevel(GetUpgradeLevel(upgrade));
            var totals = new Dictionary<string, float>();
            var order = new List<string>();

            foreach (var cost in raw)
            {
                if (!totals.ContainsKey(cost.resourceType)) order.Add(cost.resourceType);
                totals.TryGetValue(cost.resourceType, out float running);
                totals[cost.resourceType] = running + cost.amount;
            }

            var aggregated = new List<UpgradeCost>();
            foreach (string key in order)
            {
                aggregated.Add(new UpgradeCost { resourceType = key, amount = totals[key] });
            }
            return aggregated;
        }

        public UpgradeAvailability GetAvailability(UpgradeRecipe upgrade)
        {
            if (upgrade == null) return UpgradeAvailability.MaxLevel;
            if (IsMaxLevel(upgrade)) return UpgradeAvailability.MaxLevel;
            if (!PrerequisitesMet(upgrade)) return UpgradeAvailability.MissingPrerequisite;

            if (inventoryManager != null)
            {
                foreach (var cost in GetNextLevelCost(upgrade))
                {
                    if (!inventoryManager.HasResource(cost.resourceType, cost.amount))
                    {
                        return UpgradeAvailability.InsufficientResources;
                    }
                }
            }
            return UpgradeAvailability.Available;
        }

        public bool CanPerformUpgrade(UpgradeRecipe upgrade) =>
            GetAvailability(upgrade) == UpgradeAvailability.Available;

        private bool PrerequisitesMet(UpgradeRecipe upgrade)
        {
            if (upgrade.prerequisites == null) return true;

            foreach (var prerequisite in upgrade.prerequisites)
            {
                if (prerequisite == null || prerequisite.upgrade == null) continue;
                if (GetUpgradeLevel(prerequisite.upgrade) < prerequisite.requiredLevel) return false;
            }
            return true;
        }

        public string GetFailureReason(UpgradeRecipe upgrade)
        {
            switch (GetAvailability(upgrade))
            {
                case UpgradeAvailability.MaxLevel:
                    return $"Already at max level ({upgrade.maxLevel})";

                case UpgradeAvailability.MissingPrerequisite:
                    var missing = new StringBuilder();
                    foreach (var prerequisite in upgrade.prerequisites)
                    {
                        if (prerequisite == null || prerequisite.upgrade == null) continue;
                        if (GetUpgradeLevel(prerequisite.upgrade) >= prerequisite.requiredLevel) continue;
                        if (missing.Length > 0) missing.Append(", ");
                        missing.Append($"{prerequisite.upgrade.upgradeName} Lv.{prerequisite.requiredLevel}");
                    }
                    return $"Requires {missing}";

                case UpgradeAvailability.InsufficientResources:
                    foreach (var cost in GetNextLevelCost(upgrade))
                    {
                        float have = inventoryManager != null ? inventoryManager.GetResource(cost.resourceType) : 0f;
                        if (have < cost.amount)
                        {
                            return $"Insufficient {cost.resourceType} (need {cost.amount:0.#}, have {have:0.#})";
                        }
                    }
                    return "Insufficient resources";

                default:
                    return string.Empty;
            }
        }

        #endregion

        #region Purchasing

        public bool PerformUpgrade(UpgradeRecipe upgrade)
        {
            if (upgrade == null) return false;

            if (!CanPerformUpgrade(upgrade))
            {
                string reason = GetFailureReason(upgrade);
                OnUpgradeFailed?.Invoke(upgrade, reason);
                Debug.Log($"Upgrade failed: {reason}");
                return false;
            }

            // Every cost was verified affordable above and the pools are independent, so the
            // spend below cannot half-succeed.
            var costs = GetNextLevelCost(upgrade);
            foreach (var cost in costs)
            {
                if (inventoryManager.ConsumeResource(cost.resourceType, cost.amount)) continue;

                Debug.LogError($"UpgradeManager: failed to spend {cost.amount} {cost.resourceType} after passing the affordability check.");
                OnUpgradeFailed?.Invoke(upgrade, "Payment failed");
                return false;
            }

            int previousLevel = GetUpgradeLevel(upgrade);
            int newLevel = previousLevel + 1;
            levels[upgrade.Id] = newLevel;

            ApplyLevel(upgrade, previousLevel);

            OnUpgradeLevelChanged?.Invoke(upgrade, newLevel);
            OnUpgradeCompleted?.Invoke(upgrade);
            OnUpgradesRefreshed?.Invoke();

            Debug.Log($"Upgrade complete: {upgrade.upgradeName} Lv.{newLevel}/{upgrade.maxLevel}");
            return true;
        }

        /// <summary>Grants a level for free (debug, story rewards). Returns false if already maxed.</summary>
        public bool GrantUpgradeLevel(UpgradeRecipe upgrade)
        {
            if (upgrade == null || IsMaxLevel(upgrade)) return false;

            int previousLevel = GetUpgradeLevel(upgrade);
            levels[upgrade.Id] = previousLevel + 1;
            ApplyLevel(upgrade, previousLevel);

            OnUpgradeLevelChanged?.Invoke(upgrade, previousLevel + 1);
            OnUpgradesRefreshed?.Invoke();
            return true;
        }

        #endregion

        #region Applying effects

        /// <summary>
        /// Applies the delta for stepping up from <paramref name="fromLevel"/>.
        /// Every branch is a real stat change - nothing here is cosmetic logging.
        /// </summary>
        private void ApplyLevel(UpgradeRecipe upgrade, int fromLevel)
        {
            float amount = upgrade.GetEffectForNextLevel(fromLevel);

            switch (upgrade.upgradeType)
            {
                case UpgradeType.MaxPower:
                    if (roverAttributes != null) roverAttributes.AddMaxPower(amount);
                    break;

                case UpgradeType.CargoCapacity:
                    if (roverAttributes != null) roverAttributes.AddMaxCargoCapacity(amount);
                    break;

                case UpgradeType.CommunicationRange:
                    if (roverAttributes != null) roverAttributes.AddMaxCommunicationRange(amount);
                    break;

                case UpgradeType.LightRange:
                    if (roverLight != null)
                    {
                        roverLight.range += amount;
                        roverLight.intensity += amount * 0.05f;
                    }
                    break;

                case UpgradeType.CoolingSystem:
                    if (roverAttributes != null)
                    {
                        // Permanent: less heat produced, and better passive cooling.
                        roverAttributes.ReduceHeatGeneration(amount);
                        roverAttributes.AddCoolingEfficiency(amount);
                    }
                    break;

                case UpgradeType.SolarEfficiency:
                    if (roverAttributes != null) roverAttributes.AddSolarEfficiency(amount);
                    break;

                case UpgradeType.PowerEfficiency:
                    if (roverAttributes != null) roverAttributes.ReducePowerDrain(amount);
                    break;

                case UpgradeType.InventorySlots:
                    if (inventoryManager != null)
                    {
                        int extraColumns = Mathf.Max(1, Mathf.RoundToInt(amount));
                        inventoryManager.Resize(inventoryManager.GridWidth + extraColumns, inventoryManager.GridHeight);
                    }
                    break;

                case UpgradeType.TopSpeed:
                    if (roverAttributes != null) roverAttributes.AddMaxSpeedMultiplier(amount);
                    break;
            }
        }

        private void ReapplyLevels(UpgradeRecipe upgrade, int fromLevel, int toLevel)
        {
            for (int level = fromLevel; level < toLevel; level++) ApplyLevel(upgrade, level);
        }

        #endregion

        #region Persistence

        public UpgradeSaveData CaptureState()
        {
            var data = new UpgradeSaveData();
            foreach (var upgrade in availableUpgrades)
            {
                if (upgrade == null) continue;
                data.levels.Add(new UpgradeLevelSaveData
                {
                    upgradeId = upgrade.Id,
                    level = GetUpgradeLevel(upgrade)
                });
            }
            return data;
        }

        /// <summary>
        /// Restores saved levels, replaying only the levels the rover does not already have.
        /// Safe to call after Start, when starting levels have already been applied.
        /// </summary>
        public void RestoreState(UpgradeSaveData data)
        {
            if (data == null) return;

            foreach (var entry in data.levels)
            {
                var upgrade = availableUpgrades.Find(u => u != null && u.Id == entry.upgradeId);
                if (upgrade == null)
                {
                    Debug.LogWarning($"UpgradeManager: save references unknown upgrade '{entry.upgradeId}'.");
                    continue;
                }

                int current = GetUpgradeLevel(upgrade);
                int target = Mathf.Clamp(entry.level, 0, upgrade.maxLevel);
                if (target <= current) continue;

                ReapplyLevels(upgrade, current, target);
                levels[upgrade.Id] = target;
                OnUpgradeLevelChanged?.Invoke(upgrade, target);
            }

            OnUpgradesRefreshed?.Invoke();
        }

        [ContextMenu("Save Upgrades")]
        public void Save()
        {
            PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(CaptureState()));
            PlayerPrefs.Save();
            Debug.Log($"Upgrades saved to '{saveKey}'.");
        }

        [ContextMenu("Load Upgrades")]
        public void Load()
        {
            if (!PlayerPrefs.HasKey(saveKey))
            {
                Debug.Log($"No upgrade save under '{saveKey}'.");
                return;
            }
            RestoreState(JsonUtility.FromJson<UpgradeSaveData>(PlayerPrefs.GetString(saveKey)));
        }

        [ContextMenu("Clear Upgrade Save")]
        public void ClearSave() => PlayerPrefs.DeleteKey(saveKey);

        #endregion

        #region Debug

        [ContextMenu("Print Upgrades")]
        private void DebugPrintUpgrades()
        {
            var report = new StringBuilder("=== UPGRADES ===\n");
            foreach (var upgrade in availableUpgrades)
            {
                if (upgrade == null) continue;

                int level = GetUpgradeLevel(upgrade);
                report.Append($"{upgrade.upgradeName}: Lv.{level}/{upgrade.maxLevel} [{GetAvailability(upgrade)}]");

                var costs = GetNextLevelCost(upgrade);
                if (costs.Count > 0)
                {
                    report.Append("  next: ");
                    foreach (var cost in costs) report.Append($"{cost.amount:0.#} {cost.resourceType}  ");
                }
                report.AppendLine();
            }
            Debug.Log(report.ToString());
        }

        #endregion
    }
}
