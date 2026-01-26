using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Defines an upgrade recipe following equivalent exchange
    /// </summary>
    [CreateAssetMenu(fileName = "New Upgrade", menuName = "GameJam2026/Upgrade Recipe")]
    public class UpgradeRecipe : ScriptableObject
    {
        [Header("Upgrade Info")]
        public string upgradeName = "Upgrade";
        [TextArea(2, 4)]
        public string description = "Upgrade description";
        public Sprite icon;
        public UpgradeType upgradeType;
        
        [Header("Cost (Equivalent Exchange)")]
        public UpgradeCost[] costs;
        
        [Header("Effect")]
        public float upgradeAmount = 10f;
        
        [Header("Requirements")]
        public int currentLevel = 0;
        public int maxLevel = 5;
    }

    [System.Serializable]
    public class UpgradeCost
    {
        public string resourceType; // "Materials", "Tech", "Power"
        public float amount;
    }

    public enum UpgradeType
    {
        MaxPower,           // Increase max power capacity
        CargoCapacity,      // Increase max cargo weight
        CommunicationRange, // Increase comm range
        LightRange,         // Increase light range/intensity
        CoolingSystem,      // Reduce heat generation
        SolarEfficiency     // Increase solar power generation
    }
}