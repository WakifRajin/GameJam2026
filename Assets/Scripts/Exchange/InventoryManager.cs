using System.Collections.Generic;
using UnityEngine;
using System;

namespace GameJam2026
{
    /// <summary>
    /// Manages the rover's inventory and collected items
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        
        [Header("Inventory Settings")]
        [SerializeField] private int maxInventorySlots = 20;
        
        // Inventory storage
        private List<CollectibleItem> inventory = new List<CollectibleItem>();
        
        // Resources (processed from items)
        private float storedPower = 0f;
        private float storedMaterials = 0f;
        private float storedTech = 0f;
        
        // Events
        public event Action<CollectibleItem> OnItemAdded;
        public event Action<CollectibleItem> OnItemRemoved;
        public event Action<string, float> OnResourceChanged; // resource type, new amount

        // Properties
        public List<CollectibleItem> Inventory => inventory;
        public float StoredPower => storedPower;
        public float StoredMaterials => storedMaterials;
        public float StoredTech => storedTech;
        public int CurrentInventoryCount => inventory.Count;
        public int MaxInventorySlots => maxInventorySlots;

        private void Start()
        {
            if (roverAttributes == null)
            {
                roverAttributes = GetComponent<RoverAttributeManager>();
            }
        }

        #region Inventory Management

        public bool CanAddItem(CollectibleItem item)
        {
            if (item == null) return false;
            
            // Check cargo weight
            if (roverAttributes != null)
            {
                float totalWeight = GetTotalInventoryWeight() + item.weight;
                if (totalWeight > roverAttributes.MaxCargoCapacity)
                {
                    return false;
                }
            }
            
            // Check slot limit
            return inventory.Count < maxInventorySlots;
        }

        public bool AddItem(CollectibleItem item)
        {
            if (!CanAddItem(item)) return false;
            
            inventory.Add(item);
            
            // Update cargo weight
            if (roverAttributes != null)
            {
                roverAttributes.AddCargo(item.weight);
            }
            
            // Process item based on type
            ProcessItemCollection(item);
            
            OnItemAdded?.Invoke(item);
            
            Debug.Log($"Collected: {item.itemName}");
            return true;
        }

        public bool RemoveItem(CollectibleItem item)
        {
            if (!inventory.Contains(item)) return false;
            
            inventory.Remove(item);
            
            // Update cargo weight
            if (roverAttributes != null)
            {
                roverAttributes.RemoveCargo(item.weight);
            }
            
            OnItemRemoved?.Invoke(item);
            return true;
        }

        private void ProcessItemCollection(CollectibleItem item)
        {
            // Some items are auto-consumed and add to resources
            switch (item.itemType)
            {
                case ItemType.PowerCell:
                case ItemType.Fuel:
                    // Immediately add power
                    if (roverAttributes != null)
                    {
                        roverAttributes.ModifyPower(item.powerValue);
                    }
                    // Remove from inventory (it's consumed)
                    inventory.Remove(item);
                    break;
                    
                case ItemType.Material:
                    // Add to materials resource
                    AddResource("Materials", item.materialValue);
                    break;
                    
                case ItemType.TechComponent:
                    // Add to tech resource
                    AddResource("Tech", item.techValue);
                    break;
                    
                case ItemType.CoolingUnit:
                    // Reduce heat immediately
                    if (roverAttributes != null)
                    {
                        roverAttributes.ModifyHeat(-20f);
                    }
                    inventory.Remove(item);
                    break;
            }
        }

        public float GetTotalInventoryWeight()
        {
            float total = 0f;
            foreach (var item in inventory)
            {
                total += item.weight;
            }
            return total;
        }

        #endregion

        #region Resource Management

        public void AddResource(string resourceType, float amount)
        {
            switch (resourceType)
            {
                case "Power":
                    storedPower += amount;
                    OnResourceChanged?.Invoke("Power", storedPower);
                    break;
                case "Materials":
                    storedMaterials += amount;
                    OnResourceChanged?.Invoke("Materials", storedMaterials);
                    break;
                case "Tech":
                    storedTech += amount;
                    OnResourceChanged?.Invoke("Tech", storedTech);
                    break;
            }
        }

        public bool ConsumeResource(string resourceType, float amount)
        {
            switch (resourceType)
            {
                case "Power":
                    if (storedPower >= amount)
                    {
                        storedPower -= amount;
                        OnResourceChanged?.Invoke("Power", storedPower);
                        return true;
                    }
                    break;
                case "Materials":
                    if (storedMaterials >= amount)
                    {
                        storedMaterials -= amount;
                        OnResourceChanged?.Invoke("Materials", storedMaterials);
                        return true;
                    }
                    break;
                case "Tech":
                    if (storedTech >= amount)
                    {
                        storedTech -= amount;
                        OnResourceChanged?.Invoke("Tech", storedTech);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public bool HasResource(string resourceType, float amount)
        {
            switch (resourceType)
            {
                case "Power":
                    return storedPower >= amount;
                case "Materials":
                    return storedMaterials >= amount;
                case "Tech":
                    return storedTech >= amount;
            }
            return false;
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Show Inventory")]
        private void DebugShowInventory()
        {
            Debug.Log($"=== INVENTORY ({inventory.Count}/{maxInventorySlots}) ===");
            foreach (var item in inventory)
            {
                Debug.Log($"- {item.itemName} (Weight: {item.weight})");
            }
            Debug.Log($"Resources - Power: {storedPower}, Materials: {storedMaterials}, Tech: {storedTech}");
        }

        #endregion
    }
}