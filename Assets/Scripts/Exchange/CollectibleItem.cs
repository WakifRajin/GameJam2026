using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Defines a collectible item's properties
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "GameJam2026/Collectible Item")]
    public class CollectibleItem : ScriptableObject
    {
        [Header("Basic Info")]
        public string itemName = "Item";
        [TextArea(2, 4)]
        public string description = "A collectible item";
        public Sprite icon;
        public GameObject worldModelPrefab;
        
        [Header("Item Type")]
        public ItemType itemType = ItemType.Material;
        
        [Header("Resource Type (for objectives)")]
        [Tooltip("Leave empty to auto-use itemType. Or specify custom like 'Material', 'PowerCell', 'Tech'")]
        public string resourceType = "";
        
        [Header("Properties")]
        public float weight = 1f;
        public int stackSize = 1;
        
        [Header("Effects")]
        public float powerValue = 0f;
        public float heatReduction = 0f;
        public float materialValue = 0f;
        public float techValue = 0f;
        
        [Header("Visual")]
        public Color glowColor = Color.yellow;
        
        [Header("Audio")]
        public AudioClip collectSound;
        
        /// <summary>
        /// Get the resource type, using itemType as fallback
        /// </summary>
        public string GetResourceType()
        {
            if (!string.IsNullOrEmpty(resourceType))
            {
                return resourceType;
            }
            
            // Auto-map itemType to resource type
            switch (itemType)
            {
                case ItemType.Material:
                    return "Material";
                case ItemType.PowerCell:
                case ItemType.Fuel:
                    return "PowerCell";
                case ItemType.TechComponent:
                case ItemType.CommModule:
                    return "Tech";
                case ItemType.CoolingUnit:
                    return "Cooling";
                default:
                    return itemType.ToString();
            }
        }
    }

    public enum ItemType
    {
        PowerCell,
        Fuel,
        Material,
        TechComponent,
        CoolingUnit,
        CommModule,
        Tool,
        Consumable
    }
}