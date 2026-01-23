using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Defines a recipe for exchanging one resource for another
    /// Following the "Equivalent Exchange" theme
    /// </summary>
    [CreateAssetMenu(fileName = "New Exchange", menuName = "GameJam2026/Exchange Recipe")]
    public class ExchangeRecipe : ScriptableObject
    {
        [Header("Recipe Info")]
        public string exchangeName = "Exchange";
        [TextArea(2, 4)]
        public string description = "Exchange one resource for another";
        public Sprite icon;
        
        [Header("Cost (What you sacrifice)")]
        public ResourceCost[] costs;
        
        [Header("Result (What you gain)")]
        public ResourceReward[] rewards;
        
        [Header("Special Effects")]
        public bool hasSpecialEffect = false;
        public ExchangeEffectType effectType;
        public float effectValue = 0f;
        public float effectDuration = 0f; // 0 = permanent
        
        [Header("Requirements")]
        public float minimumPowerRequired = 0f;
        public bool requiresDaytime = false;
        public bool requiresNighttime = false;
    }

    [System.Serializable]
    public class ResourceCost
    {
        public ResourceType resourceType;
        public float amount;
    }

    [System.Serializable]
    public class ResourceReward
    {
        public ResourceType resourceType;
        public float amount;
    }

    public enum ResourceType
    {
        Power,              // Current rover power
        MaxPower,           // Permanent max power increase
        Speed,              // Speed multiplier
        Heat,               // Current heat
        CommunicationRange, // Comm range
        Materials,          // Stored materials
        Tech,              // Stored tech components
        CargoCapacity      // Max cargo weight
    }

    public enum ExchangeEffectType
    {
        None,
        TemporarySpeedBoost,
        TemporaryCooling,
        TemporaryPowerEfficiency,
        PermanentUpgrade,
        InstantHeal,
        InstantCooling
    }
}