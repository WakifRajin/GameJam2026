using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// A multi-level rover upgrade bought with equivalent exchange.
    ///
    /// The asset holds only the definition - the player's live level lives in UpgradeManager,
    /// so playing in the editor never dirties the asset the way the old currentLevel field did.
    /// </summary>
    [CreateAssetMenu(fileName = "New Upgrade", menuName = "GameJam2026/Upgrade Recipe")]
    public class UpgradeRecipe : ScriptableObject
    {
        [Header("Upgrade Info")]
        [Tooltip("Stable id used as the save key. Falls back to the asset name when blank.")]
        [SerializeField] private string upgradeId = "";
        public string upgradeName = "Upgrade";
        [TextArea(2, 4)]
        public string description = "Upgrade description";
        public Sprite icon;
        public UpgradeType upgradeType;

        [Header("Cost (Equivalent Exchange)")]
        [Tooltip("Cost of the FIRST level. Later levels scale by Cost Growth.")]
        public UpgradeCost[] costs;
        [Tooltip("Cost multiplier per level already owned. 1 = flat pricing, 1.5 = +50% each level.")]
        [Min(1f)] public float costGrowth = 1.5f;

        [Header("Effect")]
        [Tooltip("Effect granted by the FIRST level. Units depend on Upgrade Type - see the tooltip on each type.")]
        public float upgradeAmount = 10f;
        [Tooltip("Effect multiplier per level already owned. 1 = every level gives the same, <1 = diminishing returns.")]
        [Min(0f)] public float effectGrowth = 1f;

        [Header("Progression")]
        [Tooltip("Level the player starts the run with.")]
        [Min(0)] public int startingLevel = 0;
        [Min(1)] public int maxLevel = 5;
        [Tooltip("Every one of these must be at or above its required level before this unlocks.")]
        public UpgradePrerequisite[] prerequisites;

        [Header("Legacy")]
        [HideInInspector, Tooltip("Superseded by Starting Level. Kept so old assets still deserialize.")]
        public int currentLevel = 0;

        /// <summary>Stable save key for this upgrade.</summary>
        public string Id => string.IsNullOrWhiteSpace(upgradeId) ? name : upgradeId;

        private void OnValidate()
        {
            // Migrate assets authored against the old field.
            if (startingLevel == 0 && currentLevel > 0) startingLevel = currentLevel;
            startingLevel = Mathf.Clamp(startingLevel, 0, maxLevel);
        }

        /// <summary>
        /// Cost to go from <paramref name="currentLevel"/> to the next level.
        /// Returns an empty list when already maxed.
        /// </summary>
        public List<UpgradeCost> GetCostForNextLevel(int currentLevel)
        {
            var result = new List<UpgradeCost>();
            if (costs == null || currentLevel >= maxLevel) return result;

            float scale = Mathf.Pow(Mathf.Max(1f, costGrowth), currentLevel);
            foreach (var cost in costs)
            {
                if (cost == null || string.IsNullOrWhiteSpace(cost.resourceType)) continue;
                result.Add(new UpgradeCost
                {
                    resourceType = ResourceIds.Normalize(cost.resourceType),
                    amount = ScaleCost(cost.amount, scale)
                });
            }
            return result;
        }

        /// <summary>
        /// Applies the level multiplier to a base cost.
        ///
        /// A whole-number base price stays whole at every level - "7 Materials" reads better
        /// than "6.75", and materials are counted in whole scrap anyway. A deliberately
        /// fractional base price (0.5 Power, say) is left fractional and only tidied to 2dp.
        /// </summary>
        private static float ScaleCost(float baseAmount, float scale)
        {
            float scaled = baseAmount * scale;

            bool baseIsWhole = Mathf.Approximately(baseAmount, Mathf.Round(baseAmount));
            if (baseIsWhole && baseAmount >= 1f) return Mathf.Max(1f, Mathf.Round(scaled));

            return Mathf.Round(scaled * 100f) / 100f;
        }

        /// <summary>Effect delta granted by the step from <paramref name="currentLevel"/> to the next.</summary>
        public float GetEffectForNextLevel(int currentLevel)
        {
            return upgradeAmount * Mathf.Pow(Mathf.Max(0f, effectGrowth), currentLevel);
        }

        /// <summary>Total effect accumulated across all levels up to and including <paramref name="level"/>.</summary>
        public float GetCumulativeEffect(int level)
        {
            float total = 0f;
            for (int i = 0; i < level; i++) total += GetEffectForNextLevel(i);
            return total;
        }
    }

    [System.Serializable]
    public class UpgradeCost
    {
        [Tooltip("Materials, Tech, Power or Cooling. Aliases like \"Scrap\" are normalised.")]
        public string resourceType = ResourceIds.Materials;
        public float amount = 10f;
    }

    [System.Serializable]
    public class UpgradePrerequisite
    {
        public UpgradeRecipe upgrade;
        [Min(1)] public int requiredLevel = 1;
    }

    public enum UpgradeType
    {
        MaxPower,           // +N max power capacity
        CargoCapacity,      // +N kg cargo capacity
        CommunicationRange, // +N metres comm range
        LightRange,         // +N metres headlight range
        CoolingSystem,      // -N fraction of heat generated while moving (0.1 = 10% less)
        SolarEfficiency,    // +N fraction of solar recharge rate (0.25 = +25%)
        PowerEfficiency,    // -N fraction of passive power drain (0.1 = 10% less)
        InventorySlots,     // +N extra inventory columns
        TopSpeed            // +N to the speed multiplier ceiling
    }
}
