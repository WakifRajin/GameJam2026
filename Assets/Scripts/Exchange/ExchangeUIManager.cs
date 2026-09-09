using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace GameJam2026
{
    /// <summary>
    /// UI for the exchange/trading system
    /// </summary>
    public class ExchangeUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExchangeManager exchangeManager;
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private GridInventoryManager inventoryManager;
        
        [Header("UI Panels")]
        [SerializeField] private GameObject exchangePanel;
        [SerializeField] private Transform exchangeListContainer;
        [SerializeField] private GameObject exchangeButtonPrefab;
        
        [Header("Details Panel")]
        [SerializeField] private TextMeshProUGUI exchangeTitleText;
        [SerializeField] private TextMeshProUGUI exchangeDescriptionText;
        [SerializeField] private Transform costsContainer;
        [SerializeField] private Transform rewardsContainer;
        [SerializeField] private Button confirmExchangeButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;
        
        [Header("Resource Display Prefab")]
        [SerializeField] private GameObject resourceDisplayPrefab;
        
        [Header("Hotkey")]
        [SerializeField] private KeyCode exchangeMenuKey = KeyCode.E;
        
        private ExchangeRecipe selectedRecipe;
        private List<GameObject> spawnedButtons = new List<GameObject>();

        private void Start()
        {
            if (exchangeManager == null)
            {
                exchangeManager = FindObjectOfType<ExchangeManager>();
            }
            
            if (exchangePanel != null)
            {
                exchangePanel.SetActive(false);
            }
            
            if (confirmExchangeButton != null)
            {
                confirmExchangeButton.onClick.AddListener(OnConfirmExchange);
            }
            
            // Subscribe to events
            if (exchangeManager != null)
            {
                exchangeManager.OnExchangeCompleted += OnExchangeCompleted;
                exchangeManager.OnExchangeFailed += OnExchangeFailed;
            }
        }

        private void Update()
        {
            // Toggle exchange menu with hotkey
            if (Input.GetKeyDown(exchangeMenuKey))
            {
                ToggleExchangeMenu();
            }
        }

        public void ToggleExchangeMenu()
        {
            if (exchangePanel == null) return;
            
            bool isActive = exchangePanel.activeSelf;
            exchangePanel.SetActive(!isActive);
            
            if (!isActive)
            {
                // Opening menu
                RefreshExchangeList();
                Time.timeScale = 0f; // Pause game
            }
            else
            {
                // Closing menu
                Time.timeScale = 1f; // Unpause game
            }
        }

        private void RefreshExchangeList()
        {
            // Clear existing buttons
            foreach (var button in spawnedButtons)
            {
                Destroy(button);
            }
            spawnedButtons.Clear();
            
            // Create buttons for each exchange
            var exchanges = exchangeManager.GetAvailableExchanges();
            foreach (var exchange in exchanges)
            {
                GameObject buttonObj = Instantiate(exchangeButtonPrefab, exchangeListContainer);
                spawnedButtons.Add(buttonObj);
                
                // Setup button
                Button button = buttonObj.GetComponent<Button>();
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                
                if (buttonText != null)
                {
                    buttonText.text = exchange.exchangeName;
                }
                
                if (button != null)
                {
                    ExchangeRecipe recipe = exchange; // Local copy for closure
                    button.onClick.AddListener(() => SelectExchange(recipe));
                }
                
                // Check if exchange is available
                bool canPerform = exchangeManager.CanPerformExchange(exchange);
                button.interactable = canPerform;
                
                if (!canPerform && buttonText != null)
                {
                    buttonText.color = Color.gray;
                }
            }
        }

        private void SelectExchange(ExchangeRecipe recipe)
        {
            selectedRecipe = recipe;
            DisplayExchangeDetails(recipe);
        }

        private void DisplayExchangeDetails(ExchangeRecipe recipe)
        {
            if (exchangeTitleText != null)
            {
                exchangeTitleText.text = recipe.exchangeName;
            }
            
            if (exchangeDescriptionText != null)
            {
                exchangeDescriptionText.text = recipe.description;
            }
            
            // Display costs
            ClearContainer(costsContainer);
            foreach (var cost in recipe.costs)
            {
                DisplayResource(costsContainer, cost.resourceType, cost.amount, true);
            }
            
            // Display rewards
            ClearContainer(rewardsContainer);
            foreach (var reward in recipe.rewards)
            {
                DisplayResource(rewardsContainer, reward.resourceType, reward.amount, false);
            }
            
            // Update confirm button
            bool canPerform = exchangeManager.CanPerformExchange(recipe);
            if (confirmExchangeButton != null)
            {
                confirmExchangeButton.interactable = canPerform;
            }
            
            if (confirmButtonText != null)
            {
                confirmButtonText.text = canPerform ? "CONFIRM EXCHANGE" : "CANNOT EXCHANGE";
            }
        }

        private void DisplayResource(Transform container, ResourceType type, float amount, bool isCost)
        {
            if (resourceDisplayPrefab == null || container == null) return;
            
            GameObject resourceObj = Instantiate(resourceDisplayPrefab, container);
            TextMeshProUGUI resourceText = resourceObj.GetComponentInChildren<TextMeshProUGUI>();
            
            if (resourceText != null)
            {
                string prefix = isCost ? "-" : "+";
                string color = isCost ? "red" : "green";
                resourceText.text = $"<color={color}>{prefix}{amount} {type}</color>";
            }
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }

        private void OnConfirmExchange()
        {
            if (selectedRecipe == null) return;
            
            exchangeManager.PerformExchange(selectedRecipe);
        }

        private void OnExchangeCompleted(ExchangeRecipe recipe)
        {
            Debug.Log($"Exchange completed: {recipe.exchangeName}");
            RefreshExchangeList();
            
            // Refresh details if the same recipe is still selected
            if (selectedRecipe == recipe)
            {
                DisplayExchangeDetails(recipe);
            }
        }

        private void OnExchangeFailed(ExchangeRecipe recipe, string reason)
        {
            Debug.Log($"Exchange failed: {reason}");
            // TODO: Show error message in UI
        }

        private void OnDestroy()
        {
            if (exchangeManager != null)
            {
                exchangeManager.OnExchangeCompleted -= OnExchangeCompleted;
                exchangeManager.OnExchangeFailed -= OnExchangeFailed;
            }
        }
    }
}