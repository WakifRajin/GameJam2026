using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace GameJam2026
{
    /// <summary>
    /// UI for the upgrade system
    /// </summary>
    public class UpgradeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private GridInventoryManager inventoryManager;
        
        [Header("UI Elements")]
        [SerializeField] private Transform upgradeListContainer;
        [SerializeField] private GameObject upgradeButtonPrefab;
        
        [Header("Details Panel")]
        [SerializeField] private TextMeshProUGUI upgradeNameText;
        [SerializeField] private TextMeshProUGUI upgradeDescriptionText;
        [SerializeField] private TextMeshProUGUI upgradeLevelText;
        [SerializeField] private Transform costContainer;
        [SerializeField] private GameObject costDisplayPrefab;
        [SerializeField] private Button performUpgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeButtonText;
        
        private List<GameObject> spawnedButtons = new List<GameObject>();
        private UpgradeRecipe selectedUpgrade;

        private void Start()
        {
            if (upgradeManager == null)
            {
                upgradeManager = FindObjectOfType<UpgradeManager>();
            }
            
            if (inventoryManager == null)
            {
                inventoryManager = FindObjectOfType<GridInventoryManager>();
            }
            
            if (performUpgradeButton != null)
            {
                performUpgradeButton.onClick.AddListener(OnPerformUpgradeClicked);
            }
            
            if (upgradeManager != null)
            {
                upgradeManager.OnUpgradeCompleted += OnUpgradeCompleted;
                upgradeManager.OnUpgradeFailed += OnUpgradeFailed;
            }
            
            RefreshUpgradeList();
        }

        private void OnEnable()
        {
            RefreshUpgradeList();
        }

        private void RefreshUpgradeList()
        {
            // Clear existing buttons
            foreach (var button in spawnedButtons)
            {
                Destroy(button);
            }
            spawnedButtons.Clear();
            
            if (upgradeManager == null) return;
            
            // Create buttons for each upgrade
            var upgrades = upgradeManager.GetAvailableUpgrades();
            foreach (var upgrade in upgrades)
            {
                GameObject buttonObj = Instantiate(upgradeButtonPrefab, upgradeListContainer);
                spawnedButtons.Add(buttonObj);
                
                // Setup button
                Button button = buttonObj.GetComponent<Button>();
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                
                int currentLevel = upgradeManager.GetUpgradeLevel(upgrade);
                
                if (buttonText != null)
                {
                    buttonText.text = $"{upgrade.upgradeName} (Lv.{currentLevel}/{upgrade.maxLevel})";
                }
                
                if (button != null)
                {
                    UpgradeRecipe recipe = upgrade; // Local copy for closure
                    button.onClick.AddListener(() => SelectUpgrade(recipe));
                }
                
                // Check if upgrade is available
                bool canUpgrade = upgradeManager.CanPerformUpgrade(upgrade);
                button.interactable = canUpgrade || currentLevel < upgrade.maxLevel;
            }
        }

        private void SelectUpgrade(UpgradeRecipe upgrade)
        {
            selectedUpgrade = upgrade;
            DisplayUpgradeDetails(upgrade);
        }

        private void DisplayUpgradeDetails(UpgradeRecipe upgrade)
        {
            if (upgradeNameText != null)
            {
                upgradeNameText.text = upgrade.upgradeName;
            }
            
            if (upgradeDescriptionText != null)
            {
                upgradeDescriptionText.text = upgrade.description;
            }
            
            int currentLevel = upgradeManager.GetUpgradeLevel(upgrade);
            if (upgradeLevelText != null)
            {
                upgradeLevelText.text = $"Level: {currentLevel}/{upgrade.maxLevel}";
            }
            
            // Display costs
            ClearCostDisplay();
            foreach (var cost in upgrade.costs)
            {
                DisplayCost(cost);
            }
            
            // Update button
            bool canUpgrade = upgradeManager.CanPerformUpgrade(upgrade);
            if (performUpgradeButton != null)
            {
                performUpgradeButton.interactable = canUpgrade;
            }
            
            if (upgradeButtonText != null)
            {
                if (currentLevel >= upgrade.maxLevel)
                {
                    upgradeButtonText.text = "MAX LEVEL";
                }
                else if (canUpgrade)
                {
                    upgradeButtonText.text = "PERFORM UPGRADE";
                }
                else
                {
                    upgradeButtonText.text = "INSUFFICIENT RESOURCES";
                }
            }
        }

        private void DisplayCost(UpgradeCost cost)
        {
            if (costDisplayPrefab == null || costContainer == null) return;
            
            GameObject costObj = Instantiate(costDisplayPrefab, costContainer);
            TextMeshProUGUI costText = costObj.GetComponentInChildren<TextMeshProUGUI>();
            
            if (costText != null && inventoryManager != null)
            {
                float have = inventoryManager.GetResource(cost.resourceType);
                bool hasEnough = have >= cost.amount;
                
                string color = hasEnough ? "green" : "red";
                costText.text = $"<color={color}>{cost.resourceType}: {have}/{cost.amount}</color>";
            }
        }

        private void ClearCostDisplay()
        {
            if (costContainer == null) return;
            
            foreach (Transform child in costContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void OnPerformUpgradeClicked()
        {
            if (selectedUpgrade == null) return;
            
            upgradeManager.PerformUpgrade(selectedUpgrade);
        }

        private void OnUpgradeCompleted(UpgradeRecipe upgrade)
        {
            Debug.Log($"✓ Upgrade completed: {upgrade.upgradeName}");
            RefreshUpgradeList();
            
            if (selectedUpgrade == upgrade)
            {
                DisplayUpgradeDetails(upgrade);
            }
        }

        private void OnUpgradeFailed(UpgradeRecipe upgrade, string reason)
        {
            Debug.Log($"✗ Upgrade failed: {reason}");
        }

        private void OnDestroy()
        {
            if (upgradeManager != null)
            {
                upgradeManager.OnUpgradeCompleted -= OnUpgradeCompleted;
                upgradeManager.OnUpgradeFailed -= OnUpgradeFailed;
            }
        }
    }
}