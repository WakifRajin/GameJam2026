using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Defines a collectible item with its properties and effects
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "GameJam2026/Collectible Item")]
    public class CollectibleItem : ScriptableObject
    {
        [Header("Item Info")]
        public string itemName = "Item";
        [TextArea(3, 5)]
        public string description = "A collectible item";
        public Sprite icon;
        
        [Header("Item Properties")]
        public ItemType itemType;
        public ItemRarity rarity;
        public float weight = 1f;
        
        [Header("Resource Values")]
        public float powerValue = 0f;
        public float materialValue = 0f;
        public float techValue = 0f;
        
        [Header("Visual")]
        public Color glowColor = Color.cyan;
        public GameObject worldModelPrefab;
    }

    public enum ItemType
    {
        PowerCell,      // Provides power
        SolarPanel,     // Permanent solar upgrade
        Material,       // Raw materials for crafting
        TechComponent,  // Tech parts
        Fuel,          // Temporary power boost
        CoolingUnit,   // Reduces heat
        CommBooster,   // Increases communication range
        SpeedUpgrade   // Increases speed
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}