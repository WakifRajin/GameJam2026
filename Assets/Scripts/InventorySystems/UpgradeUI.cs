using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace GameJam2026
{
    /// <summary>
    /// Upgrade tab: lists recipes, shows the scaled cost of the next level, and buys it.
    /// Repaints whenever resources or levels change, so affordability is never stale.
    /// </summary>
    public class UpgradeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UpgradeManager upgradeManager;
        [SerializeField] private GridInventoryManager inventoryManager;

        [Header("Upgrade List")]
        [SerializeField] private Transform upgradeListContainer;
        [SerializeField] private GameObject upgradeButtonPrefab;

        [Header("Details Panel")]
        [SerializeField] private TextMeshProUGUI upgradeNameText;
        [SerializeField] private TextMeshProUGUI upgradeDescriptionText;
        [SerializeField] private TextMeshProUGUI upgradeLevelText;
        [Tooltip("Optional 'current -> next' effect preview.")]
        [SerializeField] private TextMeshProUGUI upgradeEffectText;
        [SerializeField] private Transform costContainer;
        [SerializeField] private GameObject costDisplayPrefab;
        [SerializeField] private Button performUpgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeButtonText;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private float statusMessageDuration = 3f;

        private readonly List<GameObject> spawnedButtons = new List<GameObject>();
        private UpgradeRecipe selectedUpgrade;
        private float statusMessageClearTime;

        #region Lifecycle

        private void Start()
        {
            if (upgradeManager == null) upgradeManager = FindObjectOfType<UpgradeManager>();
            if (inventoryManager == null) inventoryManager = FindObjectOfType<GridInventoryManager>();

            if (performUpgradeButton != null) performUpgradeButton.onClick.AddListener(OnPerformUpgradeClicked);

            if (upgradeManager != null)
            {
                upgradeManager.OnUpgradeCompleted += HandleUpgradeCompleted;
                upgradeManager.OnUpgradeFailed += HandleUpgradeFailed;
                upgradeManager.OnUpgradesRefreshed += Refresh;
            }
            else
            {
                Debug.LogError("UpgradeUI: UpgradeManager not found.");
            }

            RebuildUpgradeList();
        }

        private void OnDestroy()
        {
            if (upgradeManager != null)
            {
                upgradeManager.OnUpgradeCompleted -= HandleUpgradeCompleted;
                upgradeManager.OnUpgradeFailed -= HandleUpgradeFailed;
                upgradeManager.OnUpgradesRefreshed -= Refresh;
            }
        }

        private void OnEnable() => RebuildUpgradeList();

        private void Update()
        {
            if (statusText == null || statusMessageClearTime <= 0f) return;

            // Unscaled: the panel is usually open with the game paused.
            if (Time.unscaledTime >= statusMessageClearTime)
            {
                statusText.text = string.Empty;
                statusMessageClearTime = 0f;
            }
        }

        #endregion

        #region List

        private void RebuildUpgradeList()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null) Destroy(button);
            }
            spawnedButtons.Clear();

            if (upgradeManager == null || upgradeListContainer == null || upgradeButtonPrefab == null) return;

            foreach (var upgrade in upgradeManager.GetAvailableUpgrades())
            {
                if (upgrade == null) continue;

                GameObject buttonObj = Instantiate(upgradeButtonPrefab, upgradeListContainer);
                buttonObj.name = $"Upgrade_{upgrade.Id}";
                spawnedButtons.Add(buttonObj);

                Button button = buttonObj.GetComponent<Button>();
                if (button != null)
                {
                    UpgradeRecipe captured = upgrade;
                    button.onClick.AddListener(() => SelectUpgrade(captured));
                }

                PaintListEntry(buttonObj, upgrade);
            }

            // Keep a sensible default selection so the details panel is never blank.
            if (selectedUpgrade == null)
            {
                var upgrades = upgradeManager.GetAvailableUpgrades();
                if (upgrades.Count > 0) SelectUpgrade(upgrades[0]);
            }
            else
            {
                DisplayUpgradeDetails(selectedUpgrade);
            }
        }

        private void PaintListEntry(GameObject buttonObj, UpgradeRecipe upgrade)
        {
            int level = upgradeManager.GetUpgradeLevel(upgrade);
            var availability = upgradeManager.GetAvailability(upgrade);

            TextMeshProUGUI label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string suffix = availability == UpgradeAvailability.MaxLevel ? " MAX" :
                                availability == UpgradeAvailability.MissingPrerequisite ? " (locked)" : string.Empty;
                label.text = $"{upgrade.upgradeName} (Lv.{level}/{upgrade.maxLevel}){suffix}";
                label.color = availability == UpgradeAvailability.Available ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }

            Image icon = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && upgrade.icon != null)
            {
                icon.sprite = upgrade.icon;
                icon.enabled = true;
            }

            // Everything stays clickable so the player can read why something is locked.
            Button button = buttonObj.GetComponent<Button>();
            if (button != null) button.interactable = true;
        }

        /// <summary>Repaints levels, costs and affordability without rebuilding the list objects.</summary>
        private void Refresh()
        {
            if (upgradeManager == null) return;

            // Resources change on every pickup, but this panel is usually closed. Skip the
            // work rather than instantiating cost rows into a hidden container; OnEnable
            // rebuilds from scratch whenever the tab is opened.
            if (!isActiveAndEnabled) return;

            var upgrades = upgradeManager.GetAvailableUpgrades();
            for (int i = 0; i < spawnedButtons.Count && i < upgrades.Count; i++)
            {
                if (spawnedButtons[i] == null || upgrades[i] == null) continue;
                PaintListEntry(spawnedButtons[i], upgrades[i]);
            }

            if (selectedUpgrade != null) DisplayUpgradeDetails(selectedUpgrade);
        }

        #endregion

        #region Details

        private void SelectUpgrade(UpgradeRecipe upgrade)
        {
            selectedUpgrade = upgrade;
            DisplayUpgradeDetails(upgrade);
        }

        private void DisplayUpgradeDetails(UpgradeRecipe upgrade)
        {
            if (upgrade == null || upgradeManager == null) return;

            int level = upgradeManager.GetUpgradeLevel(upgrade);
            var availability = upgradeManager.GetAvailability(upgrade);
            bool maxed = availability == UpgradeAvailability.MaxLevel;

            if (upgradeNameText != null) upgradeNameText.text = upgrade.upgradeName;
            if (upgradeDescriptionText != null) upgradeDescriptionText.text = upgrade.description;
            if (upgradeLevelText != null) upgradeLevelText.text = $"Level: {level}/{upgrade.maxLevel}";

            if (upgradeEffectText != null)
            {
                float current = upgrade.GetCumulativeEffect(level);
                upgradeEffectText.text = maxed
                    ? $"{upgrade.upgradeType}: {current:0.##} (max)"
                    : $"{upgrade.upgradeType}: {current:0.##} -> {current + upgrade.GetEffectForNextLevel(level):0.##}";
            }

            DisplayCosts(upgrade, maxed);

            if (performUpgradeButton != null)
            {
                performUpgradeButton.interactable = availability == UpgradeAvailability.Available;
            }

            if (upgradeButtonText != null)
            {
                switch (availability)
                {
                    case UpgradeAvailability.MaxLevel:
                        upgradeButtonText.text = "MAX LEVEL";
                        break;
                    case UpgradeAvailability.MissingPrerequisite:
                        upgradeButtonText.text = "LOCKED";
                        break;
                    case UpgradeAvailability.InsufficientResources:
                        upgradeButtonText.text = "INSUFFICIENT RESOURCES";
                        break;
                    default:
                        upgradeButtonText.text = "PERFORM UPGRADE";
                        break;
                }
            }
        }

        private void DisplayCosts(UpgradeRecipe upgrade, bool maxed)
        {
            if (costContainer == null) return;

            for (int i = costContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(costContainer.GetChild(i).gameObject);
            }

            if (maxed || costDisplayPrefab == null || inventoryManager == null) return;

            foreach (var cost in upgradeManager.GetNextLevelCost(upgrade))
            {
                GameObject costObj = Instantiate(costDisplayPrefab, costContainer);
                TextMeshProUGUI costText = costObj.GetComponentInChildren<TextMeshProUGUI>();
                if (costText == null) continue;

                float have = inventoryManager.GetResource(cost.resourceType);
                bool enough = have >= cost.amount;
                string color = enough ? "#6EE87A" : "#E8706E";

                costText.text = $"<color={color}>{cost.resourceType}: {have:0.#} / {cost.amount:0.#}</color>";
            }
        }

        #endregion

        #region Actions

        private void OnPerformUpgradeClicked()
        {
            if (selectedUpgrade == null || upgradeManager == null) return;
            upgradeManager.PerformUpgrade(selectedUpgrade);
        }

        private void HandleUpgradeCompleted(UpgradeRecipe upgrade)
        {
            ShowStatus($"{upgrade.upgradeName} upgraded to Lv.{upgradeManager.GetUpgradeLevel(upgrade)}");
            Refresh();
        }

        private void HandleUpgradeFailed(UpgradeRecipe upgrade, string reason)
        {
            ShowStatus(reason);
        }

        private void ShowStatus(string message)
        {
            if (statusText == null)
            {
                Debug.Log($"[Upgrades] {message}");
                return;
            }

            statusText.text = message;
            statusMessageClearTime = Time.unscaledTime + statusMessageDuration;
        }

        #endregion
    }
}
