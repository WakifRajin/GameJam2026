using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Manages upgrades with equivalent exchange philosophy
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private GridInventoryManager inventoryManager;
        
        [Header("Available Upgrades")]
        [SerializeField] private List<UpgradeRecipe> availableUpgrades = new List<UpgradeRecipe>();
        
        // Track current upgrade levels
        private Dictionary<UpgradeRecipe, int> upgradeLevels = new Dictionary<UpgradeRecipe, int>();
        
        // Events
        public event Action<UpgradeRecipe> OnUpgradeCompleted;
        public event Action<UpgradeRecipe, string> OnUpgradeFailed;
        
        private void Start()
        {
            if (roverAttributes == null)
            {
                roverAttributes = GetComponent<RoverAttributeManager>();
            }
            
            if (inventoryManager == null)
            {
                inventoryManager = GetComponent<GridInventoryManager>();
            }
            
            // Initialize upgrade levels
            foreach (var upgrade in availableUpgrades)
            {
                upgradeLevels[upgrade] = upgrade.currentLevel;
            }
        }

        public bool CanPerformUpgrade(UpgradeRecipe upgrade)
        {
            if (upgrade == null) return false;
            
            // Check if already at max level
            int currentLevel = GetUpgradeLevel(upgrade);
            if (currentLevel >= upgrade.maxLevel)
            {
                return false;
            }
            
            // Check if we have all required resources
            foreach (var cost in upgrade.costs)
            {
                if (!inventoryManager.HasResource(cost.resourceType, cost.amount))
                {
                    return false;
                }
            }
            
            return true;
        }

        public bool PerformUpgrade(UpgradeRecipe upgrade)
        {
            if (!CanPerformUpgrade(upgrade))
            {
                string reason = GetUpgradeFailureReason(upgrade);
                OnUpgradeFailed?.Invoke(upgrade, reason);
                Debug.Log($"Upgrade failed: {reason}");
                return false;
            }
            
            // Consume resources (the sacrifice)
            foreach (var cost in upgrade.costs)
            {
                inventoryManager.ConsumeResource(cost.resourceType, cost.amount);
            }
            
            // Apply upgrade (the gain)
            ApplyUpgrade(upgrade);
            
            // Increase level
            upgradeLevels[upgrade]++;
            
            OnUpgradeCompleted?.Invoke(upgrade);
            
            Debug.Log($"✓ Upgrade completed: {upgrade.upgradeName} (Level {upgradeLevels[upgrade]})");
            return true;
        }

        private void ApplyUpgrade(UpgradeRecipe upgrade)
        {
            if (roverAttributes == null) return;
            
            switch (upgrade.upgradeType)
            {
                case UpgradeType.MaxPower:
                    // Increase max power capacity
                    float newMaxPower = roverAttributes.MaxPower + upgrade.upgradeAmount;
                    // You'll need to add a SetMaxPower method to RoverAttributeManager
                    Debug.Log($"Max Power increased by {upgrade.upgradeAmount}");
                    break;
                    
                case UpgradeType.CargoCapacity:
                    float newCargo = roverAttributes.MaxCargoCapacity + upgrade.upgradeAmount;
                    // You'll need to add a SetMaxCargoCapacity method
                    Debug.Log($"Cargo Capacity increased by {upgrade.upgradeAmount}");
                    break;
                    
                case UpgradeType.CommunicationRange:
                    roverAttributes.ModifyCommunicationRange(upgrade.upgradeAmount);
                    Debug.Log($"Communication Range increased by {upgrade.upgradeAmount}");
                    break;
                    
                case UpgradeType.LightRange:
                    // Apply to rover's light component
                    Light roverLight = GetComponentInChildren<Light>();
                    if (roverLight != null)
                    {
                        roverLight.range += upgrade.upgradeAmount;
                        Debug.Log($"Light Range increased by {upgrade.upgradeAmount}");
                    }
                    break;
                    
                case UpgradeType.CoolingSystem:
                    // Reduce current heat and improve cooling
                    roverAttributes.ModifyHeat(-upgrade.upgradeAmount);
                    Debug.Log($"Cooling System upgraded - heat reduced by {upgrade.upgradeAmount}");
                    break;
                    
                case UpgradeType.SolarEfficiency:
                    // Increase solar power generation rate
                    // You'll need to add this to RoverAttributeManager
                    Debug.Log($"Solar Efficiency increased by {upgrade.upgradeAmount}%");
                    break;
            }
        }

        private string GetUpgradeFailureReason(UpgradeRecipe upgrade)
        {
            int currentLevel = GetUpgradeLevel(upgrade);
            if (currentLevel >= upgrade.maxLevel)
                return $"Already at max level ({upgrade.maxLevel})";
            
            foreach (var cost in upgrade.costs)
            {
                if (!inventoryManager.HasResource(cost.resourceType, cost.amount))
                {
                    float have = inventoryManager.GetResource(cost.resourceType);
                    return $"Insufficient {cost.resourceType} (need {cost.amount}, have {have})";
                }
            }
            
            return "Unknown reason";
        }

        public int GetUpgradeLevel(UpgradeRecipe upgrade)
        {
            if (!upgradeLevels.ContainsKey(upgrade))
            {
                upgradeLevels[upgrade] = upgrade.currentLevel;
            }
            return upgradeLevels[upgrade];
        }

        public List<UpgradeRecipe> GetAvailableUpgrades()
        {
            return availableUpgrades;
        }
    }
}