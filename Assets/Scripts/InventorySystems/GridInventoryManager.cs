using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam2026
{
    /// <summary>
    /// Grid-based inventory system with visual representation
    /// </summary>
    public class GridInventoryManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 6;
        [SerializeField] private int gridHeight = 4;
        
        [Header("Input System")]
        [SerializeField] private InputActionAsset inputActions;
        private InputAction inventoryAction;
        
        // Grid storage - each slot can hold one item
        private CollectibleItem[,] inventoryGrid;
        
        // Resources (for crafting/upgrades)
        private Dictionary<string, float> resources = new Dictionary<string, float>();
        
        // Events
        public event Action<CollectibleItem, int, int> OnItemAddedToGrid; // item, x, y
        public event Action<int, int> OnItemRemovedFromGrid; // x, y
        public event Action<string, float> OnResourceChanged; // resource type, amount
        public event Action OnInventoryOpened;
        public event Action OnInventoryClosed;
        
        // Properties
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public int TotalSlots => gridWidth * gridHeight;
        public int UsedSlots { get; private set; }
        public CollectibleItem[,] InventoryGrid => inventoryGrid;
        
        private void Awake()
        {
            inventoryGrid = new CollectibleItem[gridWidth, gridHeight];
            
            // Initialize resources
            resources["Materials"] = 0f;
            resources["Tech"] = 0f;
            resources["Power"] = 0f;
            
            // Setup Input System
            if (inputActions != null)
            {
                inventoryAction = inputActions.FindAction("Inventory");
                if (inventoryAction == null)
                {
                    Debug.LogWarning("GridInventoryManager: 'Inventory' action not found in Input Actions!");
                }
            }
            else
            {
                Debug.LogError("GridInventoryManager: Input Actions asset not assigned!");
            }
        }

        private void Start()
        {
            if (roverAttributes == null)
            {
                roverAttributes = GetComponent<RoverAttributeManager>();
            }
        }

        private void OnEnable()
        {
            if (inventoryAction != null)
            {
                inventoryAction.Enable();
                inventoryAction.performed += OnInventoryKeyPressed;
            }
        }

        private void OnDisable()
        {
            if (inventoryAction != null)
            {
                inventoryAction.performed -= OnInventoryKeyPressed;
                inventoryAction.Disable();
            }
        }

        private void OnInventoryKeyPressed(InputAction.CallbackContext context)
        {
            ToggleInventory();
        }

        #region Grid Management

        public bool AddItem(CollectibleItem item)
        {
            if (item == null) return false;
            
            // Find first empty slot
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (inventoryGrid[x, y] == null)
                    {
                        return AddItemAt(item, x, y);
                    }
                }
            }
            
            Debug.Log("Inventory full! No empty slots.");
            return false;
        }

        public bool AddItemAt(CollectibleItem item, int x, int y)
        {
            if (item == null) return false;
            if (!IsValidPosition(x, y)) return false;
            if (inventoryGrid[x, y] != null) return false;
            
            // Check cargo weight
            if (roverAttributes != null)
            {
                float totalWeight = GetTotalWeight() + item.weight;
                if (totalWeight > roverAttributes.MaxCargoCapacity)
                {
                    Debug.Log("Cargo capacity exceeded!");
                    return false;
                }
            }
            
            inventoryGrid[x, y] = item;
            UsedSlots++;
            
            // Update cargo weight
            if (roverAttributes != null)
            {
                roverAttributes.AddCargo(item.weight);
            }
            
            // Process item (convert to resources if needed)
            ProcessItem(item);
            
            OnItemAddedToGrid?.Invoke(item, x, y);
            
            Debug.Log($"Added {item.itemName} to inventory at ({x}, {y})");
            return true;
        }

        public CollectibleItem RemoveItemAt(int x, int y)
        {
            if (!IsValidPosition(x, y)) return null;
            if (inventoryGrid[x, y] == null) return null;
            
            CollectibleItem item = inventoryGrid[x, y];
            inventoryGrid[x, y] = null;
            UsedSlots--;
            
            // Update cargo weight
            if (roverAttributes != null)
            {
                roverAttributes.RemoveCargo(item.weight);
            }
            
            OnItemRemovedFromGrid?.Invoke(x, y);
            
            Debug.Log($"Removed {item.itemName} from inventory at ({x}, {y})");
            return item;
        }

        public bool MoveItem(int fromX, int fromY, int toX, int toY)
        {
            if (!IsValidPosition(fromX, fromY) || !IsValidPosition(toX, toY)) return false;
            if (inventoryGrid[fromX, fromY] == null) return false;
            if (inventoryGrid[toX, toY] != null) return false; // Destination must be empty
            
            CollectibleItem item = inventoryGrid[fromX, fromY];
            inventoryGrid[fromX, fromY] = null;
            inventoryGrid[toX, toY] = item;
            
            OnItemRemovedFromGrid?.Invoke(fromX, fromY);
            OnItemAddedToGrid?.Invoke(item, toX, toY);
            
            return true;
        }

        public CollectibleItem GetItemAt(int x, int y)
        {
            if (!IsValidPosition(x, y)) return null;
            return inventoryGrid[x, y];
        }

        public bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        #endregion

        #region Item Processing

        private void ProcessItem(CollectibleItem item)
        {
            // Auto-consume certain items and convert to resources
            switch (item.itemType)
            {
                case ItemType.PowerCell:
                case ItemType.Fuel:
                    // Convert to power resource
                    AddResource("Power", item.powerValue);
                    break;
                    
                case ItemType.Material:
                    // Add to materials
                    AddResource("Materials", item.materialValue);
                    break;
                    
                case ItemType.TechComponent:
                    // Add to tech
                    AddResource("Tech", item.techValue);
                    break;
            }
        }

        public void ConsumeItem(int x, int y)
        {
            CollectibleItem item = GetItemAt(x, y);
            if (item == null) return;
            
            // Consume the item for immediate effect
            switch (item.itemType)
            {
                case ItemType.PowerCell:
                case ItemType.Fuel:
                    if (roverAttributes != null)
                    {
                        roverAttributes.ModifyPower(item.powerValue);
                        Debug.Log($"Consumed {item.itemName} - gained {item.powerValue} power");
                    }
                    break;
                    
                case ItemType.CoolingUnit:
                    if (roverAttributes != null)
                    {
                        roverAttributes.ModifyHeat(-20f);
                        Debug.Log($"Consumed {item.itemName} - reduced heat");
                    }
                    break;
            }
            
            // Remove from inventory
            RemoveItemAt(x, y);
        }

        public void DiscardItem(int x, int y)
        {
            CollectibleItem item = RemoveItemAt(x, y);
            if (item != null)
            {
                Debug.Log($"Discarded {item.itemName}");
            }
        }

        #endregion

        #region Resources

        public void AddResource(string resourceType, float amount)
        {
            if (!resources.ContainsKey(resourceType))
            {
                resources[resourceType] = 0f;
            }
            
            resources[resourceType] += amount;
            OnResourceChanged?.Invoke(resourceType, resources[resourceType]);
            
            Debug.Log($"Added {amount} {resourceType}. Total: {resources[resourceType]}");
        }

        public bool ConsumeResource(string resourceType, float amount)
        {
            if (!resources.ContainsKey(resourceType)) return false;
            if (resources[resourceType] < amount) return false;
            
            resources[resourceType] -= amount;
            OnResourceChanged?.Invoke(resourceType, resources[resourceType]);
            
            Debug.Log($"Consumed {amount} {resourceType}. Remaining: {resources[resourceType]}");
            return true;
        }

        public float GetResource(string resourceType)
        {
            if (!resources.ContainsKey(resourceType)) return 0f;
            return resources[resourceType];
        }

        public bool HasResource(string resourceType, float amount)
        {
            if (!resources.ContainsKey(resourceType)) return false;
            return resources[resourceType] >= amount;
        }

        #endregion

        #region Utility

        public float GetTotalWeight()
        {
            float total = 0f;
            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    if (inventoryGrid[x, y] != null)
                    {
                        total += inventoryGrid[x, y].weight;
                    }
                }
            }
            return total;
        }

        public int GetEmptySlots()
        {
            return TotalSlots - UsedSlots;
        }

        private void ToggleInventory()
        {
            // This will be handled by the UI manager
            // Just fire the event
            bool isOpen = InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;
            
            if (isOpen)
            {
                OnInventoryClosed?.Invoke();
            }
            else
            {
                OnInventoryOpened?.Invoke();
            }
        }

        #endregion

        #region Debug

        [ContextMenu("Print Inventory")]
        private void DebugPrintInventory()
        {
            Debug.Log("=== INVENTORY GRID ===");
            for (int y = 0; y < gridHeight; y++)
            {
                string row = $"Row {y}: ";
                for (int x = 0; x < gridWidth; x++)
                {
                    if (inventoryGrid[x, y] != null)
                    {
                        row += $"[{inventoryGrid[x, y].itemName}] ";
                    }
                    else
                    {
                        row += "[Empty] ";
                    }
                }
                Debug.Log(row);
            }
            
            Debug.Log($"Used Slots: {UsedSlots}/{TotalSlots}");
            Debug.Log($"Total Weight: {GetTotalWeight()}/{roverAttributes?.MaxCargoCapacity}");
            
            Debug.Log("=== RESOURCES ===");
            foreach (var resource in resources)
            {
                Debug.Log($"{resource.Key}: {resource.Value}");
            }
        }

        [ContextMenu("Add Test Resources")]
        private void DebugAddResources()
        {
            AddResource("Materials", 100);
            AddResource("Tech", 100);
            AddResource("Power", 100);
        }

        #endregion
    }
}