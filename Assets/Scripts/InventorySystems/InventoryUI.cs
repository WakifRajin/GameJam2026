using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace GameJam2026
{
    /// <summary>
    /// Visual grid-based inventory UI with drag and drop
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GridInventoryManager inventoryManager;
        [SerializeField] private UpgradeManager upgradeManager;
        
        [Header("Main Panel (Contains everything)")]
        [SerializeField] private GameObject inventoryPanel; // The whole UI panel
        
        [Header("Sub-Containers (Inside main panel)")]
        [SerializeField] private GameObject inventoryContainer; // The inventory tab content
        [SerializeField] private GameObject upgradeContainer; // The upgrade tab content
        [SerializeField] private Transform gridContainer;
        [SerializeField] private GameObject inventorySlotPrefab;
        
        [Header("Info Display")]
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private Image itemIconImage;
        [SerializeField] private GameObject itemInfoPanel;
        
        [Header("Action Buttons")]
        [SerializeField] private Button consumeButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private GameObject actionButtonsPanel;
        
        [Header("Resource Display")]
        [SerializeField] private TextMeshProUGUI materialsText;
        [SerializeField] private TextMeshProUGUI techText;
        [SerializeField] private TextMeshProUGUI powerText;
        
        [Header("Tabs")]
        [SerializeField] private Button inventoryTabButton;
        [SerializeField] private Button upgradeTabButton;
        
        [Header("Pause Settings")]
        [SerializeField] private bool pauseGameWhenOpen = true;
        [SerializeField] private CanvasGroup gameplayUIGroup;
        
        private InventorySlot[,] inventorySlots;
        private InventorySlot selectedSlot;
        private bool isOpen = false;
        
        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Find managers
            if (inventoryManager == null)
            {
                inventoryManager = FindObjectOfType<GridInventoryManager>();
            }
            
            if (upgradeManager == null)
            {
                upgradeManager = FindObjectOfType<UpgradeManager>();
            }
            
            // Verify EventSystem exists
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                Debug.LogError("InventoryUI: No EventSystem found in scene! UI buttons won't work. Add one via: GameObject → UI → Event System");
            }
            
            // Subscribe to events
            if (inventoryManager != null)
            {
                inventoryManager.OnItemAddedToGrid += OnItemAddedToGrid;
                inventoryManager.OnItemRemovedFromGrid += OnItemRemovedFromGrid;
                inventoryManager.OnResourceChanged += OnResourceChanged;
                inventoryManager.OnInventoryOpened += OpenInventory;
                inventoryManager.OnInventoryClosed += CloseInventory;
            }
            else
            {
                Debug.LogError("InventoryUI: GridInventoryManager not found!");
            }
            
            // Setup buttons
            if (consumeButton != null)
            {
                consumeButton.onClick.AddListener(OnConsumeButtonClicked);
                Debug.Log("Consume button listener added");
            }
            else
            {
                Debug.LogWarning("InventoryUI: Consume button not assigned!");
            }
            
            if (discardButton != null)
            {
                discardButton.onClick.AddListener(OnDiscardButtonClicked);
                Debug.Log("Discard button listener added");
            }
            else
            {
                Debug.LogWarning("InventoryUI: Discard button not assigned!");
            }
            
            if (inventoryTabButton != null)
                inventoryTabButton.onClick.AddListener(() => ShowTab(true));
            
            if (upgradeTabButton != null)
                upgradeTabButton.onClick.AddListener(() => ShowTab(false));
            
            // Initialize
            CreateInventoryGrid();
            
            // Close inventory at start
            if (inventoryPanel != null)
                inventoryPanel.SetActive(false);
            
            // Show inventory tab by default when opened
            ShowTab(true);
            
            if (actionButtonsPanel != null)
                actionButtonsPanel.SetActive(false);
            
            if (itemInfoPanel != null)
                itemInfoPanel.SetActive(false);
            
            // Make sure cursor is available for editor testing
            #if UNITY_EDITOR
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            #endif
        }

        private void CreateInventoryGrid()
        {
            if (gridContainer == null)
            {
                Debug.LogError("InventoryUI: Grid Container not assigned!");
                return;
            }
            
            if (inventorySlotPrefab == null)
            {
                Debug.LogError("InventoryUI: Inventory Slot Prefab not assigned!");
                return;
            }
            
            if (inventoryManager == null)
            {
                Debug.LogError("InventoryUI: Inventory Manager not found!");
                return;
            }
            
            int width = inventoryManager.GridWidth;
            int height = inventoryManager.GridHeight;
            
            inventorySlots = new InventorySlot[width, height];
            
            // Set up GridLayoutGroup
            GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
            if (gridLayout == null)
            {
                gridLayout = gridContainer.gameObject.AddComponent<GridLayoutGroup>();
            }
            
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = width;
            gridLayout.cellSize = new Vector2(80, 80);
            gridLayout.spacing = new Vector2(5, 5);
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            
            // Create slots
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GameObject slotObj = Instantiate(inventorySlotPrefab, gridContainer);
                    slotObj.name = $"Slot_{x}_{y}";
                    
                    InventorySlot slot = slotObj.GetComponent<InventorySlot>();
                    if (slot != null)
                    {
                        slot.Initialize(x, y, this);
                        inventorySlots[x, y] = slot;
                    }
                    else
                    {
                        Debug.LogError($"InventorySlot component not found on prefab at ({x}, {y})");
                    }
                }
            }
            
            Debug.Log($"Created {width}x{height} inventory grid ({width * height} slots)");
        }

        private void OnItemAddedToGrid(CollectibleItem item, int x, int y)
        {
            if (inventorySlots != null && inventorySlots[x, y] != null)
            {
                inventorySlots[x, y].SetItem(item);
            }
            
            UpdateResourceDisplay();
        }

        private void OnItemRemovedFromGrid(int x, int y)
        {
            if (inventorySlots != null && inventorySlots[x, y] != null)
            {
                inventorySlots[x, y].ClearItem();
            }
            
            UpdateResourceDisplay();
        }

        private void OnResourceChanged(string resourceType, float amount)
        {
            UpdateResourceDisplay();
        }

        private void UpdateResourceDisplay()
        {
            if (inventoryManager == null) return;
            
            if (materialsText != null)
            {
                materialsText.text = $"Materials: {inventoryManager.GetResource("Materials"):F0}";
            }
            
            if (techText != null)
            {
                techText.text = $"Tech: {inventoryManager.GetResource("Tech"):F0}";
            }
            
            if (powerText != null)
            {
                powerText.text = $"Power: {inventoryManager.GetResource("Power"):F0}";
            }
        }

        public void OnSlotClicked(InventorySlot slot)
        {
            selectedSlot = slot;
            
            if (slot.HasItem)
            {
                ShowItemInfo(slot.GetItem());
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(true);
            }
            else
            {
                HideItemInfo();
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(false);
            }
        }

        private void ShowItemInfo(CollectibleItem item)
        {
            if (itemInfoPanel != null)
                itemInfoPanel.SetActive(true);
            
            if (itemNameText != null)
                itemNameText.text = item.itemName;
            
            if (itemDescriptionText != null)
                itemDescriptionText.text = item.description;
            
            if (itemIconImage != null && item.icon != null)
            {
                itemIconImage.sprite = item.icon;
                itemIconImage.enabled = true;
            }
            else if (itemIconImage != null)
            {
                itemIconImage.enabled = false;
            }
        }

        private void HideItemInfo()
        {
            if (itemInfoPanel != null)
                itemInfoPanel.SetActive(false);
        }

        private void OnConsumeButtonClicked()
        {
            Debug.Log("Consume button clicked!");
            
            if (selectedSlot != null && selectedSlot.HasItem)
            {
                inventoryManager.ConsumeItem(selectedSlot.GridX, selectedSlot.GridY);
                selectedSlot = null;
                HideItemInfo();
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(false);
            }
            else
            {
                Debug.Log("No item selected to consume");
            }
        }

        private void OnDiscardButtonClicked()
        {
            Debug.Log("Discard button clicked!");
            
            if (selectedSlot != null && selectedSlot.HasItem)
            {
                inventoryManager.DiscardItem(selectedSlot.GridX, selectedSlot.GridY);
                selectedSlot = null;
                HideItemInfo();
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(false);
            }
            else
            {
                Debug.Log("No item selected to discard");
            }
        }

        private void ShowTab(bool showInventory)
        {
            // Toggle between inventory and upgrade containers
            if (inventoryContainer != null)
                inventoryContainer.SetActive(showInventory);
            
            if (upgradeContainer != null)
                upgradeContainer.SetActive(!showInventory);
            
            Debug.Log($"Showing tab: {(showInventory ? "Inventory" : "Upgrades")}");
        }

        public void OpenInventory()
        {
            Debug.Log("Opening inventory...");
            
            if (inventoryPanel != null)
            {
                // Show the whole panel
                inventoryPanel.SetActive(true);
                
                // Make sure we're on inventory tab by default
                ShowTab(true);
                
                isOpen = true;
                
                if (pauseGameWhenOpen)
                {
                    Time.timeScale = 0f;
                }
                
                // Disable gameplay UI
                if (gameplayUIGroup != null)
                {
                    gameplayUIGroup.alpha = 0.3f;
                    gameplayUIGroup.interactable = false;
                }
                
                // IMPORTANT: Make cursor visible and unlocked
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                
                UpdateResourceDisplay();
                
                Debug.Log("Inventory opened. Cursor visible: " + Cursor.visible);
            }
        }

        public void CloseInventory()
        {
            Debug.Log("Closing inventory...");
            
            if (inventoryPanel != null)
            {
                // Hide the whole panel (including both containers and tabs)
                inventoryPanel.SetActive(false);
                
                isOpen = false;
                
                if (pauseGameWhenOpen)
                {
                    Time.timeScale = 1f;
                }
                
                // Re-enable gameplay UI
                if (gameplayUIGroup != null)
                {
                    gameplayUIGroup.alpha = 1f;
                    gameplayUIGroup.interactable = true;
                }
                
                // Lock cursor back for gameplay
                #if !UNITY_EDITOR
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                #endif
                
                HideItemInfo();
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(false);
                
                Debug.Log("Inventory closed");
            }
        }

        private void OnDestroy()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnItemAddedToGrid -= OnItemAddedToGrid;
                inventoryManager.OnItemRemovedFromGrid -= OnItemRemovedFromGrid;
                inventoryManager.OnResourceChanged -= OnResourceChanged;
                inventoryManager.OnInventoryOpened -= OpenInventory;
                inventoryManager.OnInventoryClosed -= CloseInventory;
            }
        }

        #region Debug Methods

        [ContextMenu("Test Button Click")]
        private void DebugTestButton()
        {
            Debug.Log("=== BUTTON TEST ===");
            
            if (consumeButton != null)
            {
                Debug.Log($"Consume Button: {consumeButton.name}");
                Debug.Log($"- Interactable: {consumeButton.interactable}");
                Debug.Log($"- GameObject Active: {consumeButton.gameObject.activeInHierarchy}");
                Debug.Log($"- Parent Active: {consumeButton.transform.parent.gameObject.activeInHierarchy}");
                
                Image img = consumeButton.GetComponent<Image>();
                if (img != null)
                {
                    Debug.Log($"- Image Raycast Target: {img.raycastTarget}");
                }
                else
                {
                    Debug.LogWarning("- No Image component found!");
                }
                
                // Check listeners
                var listenerCount = consumeButton.onClick.GetPersistentEventCount();
                Debug.Log($"- Persistent Listeners: {listenerCount}");
                
                // Simulate click
                Debug.Log("Attempting to invoke button click...");
                consumeButton.onClick.Invoke();
            }
            else
            {
                Debug.LogError("Consume button is NULL!");
            }
            
            // Check EventSystem
            UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null)
            {
                Debug.LogError("NO EVENT SYSTEM FOUND IN SCENE!");
            }
            else
            {
                Debug.Log($"✓ EventSystem found: {es.name}");
                Debug.Log($"- Current Selected GameObject: {(es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "None")}");
                Debug.Log($"- EventSystem Enabled: {es.enabled}");
            }
            
            // Check cursor
            Debug.Log($"Cursor visible: {Cursor.visible}");
            Debug.Log($"Cursor lock state: {Cursor.lockState}");
        }

        [ContextMenu("Force Open Inventory")]
        private void DebugForceOpen()
        {
            OpenInventory();
        }

        [ContextMenu("Force Close Inventory")]
        private void DebugForceClose()
        {
            CloseInventory();
        }

        [ContextMenu("Toggle Tab")]
        private void DebugToggleTab()
        {
            bool showInventory = inventoryContainer != null && !inventoryContainer.activeSelf;
            ShowTab(showInventory);
        }

        [ContextMenu("Print UI Status")]
        private void DebugPrintStatus()
        {
            Debug.Log("=== INVENTORY UI STATUS ===");
            Debug.Log($"Is Open: {isOpen}");
            Debug.Log($"Inventory Panel (Main): {(inventoryPanel != null ? inventoryPanel.name : "NULL")}");
            Debug.Log($"- Active: {(inventoryPanel != null ? inventoryPanel.activeSelf : false)}");
            Debug.Log($"Inventory Container: {(inventoryContainer != null ? inventoryContainer.name : "NULL")}");
            Debug.Log($"- Active: {(inventoryContainer != null ? inventoryContainer.activeSelf : false)}");
            Debug.Log($"Upgrade Container: {(upgradeContainer != null ? upgradeContainer.name : "NULL")}");
            Debug.Log($"- Active: {(upgradeContainer != null ? upgradeContainer.activeSelf : false)}");
            Debug.Log($"Grid Container: {(gridContainer != null ? gridContainer.name : "NULL")}");
            Debug.Log($"Slot Prefab: {(inventorySlotPrefab != null ? inventorySlotPrefab.name : "NULL")}");
            Debug.Log($"Inventory Manager: {(inventoryManager != null ? "Found" : "NULL")}");
            Debug.Log($"Slots Created: {(inventorySlots != null ? $"{inventorySlots.GetLength(0)}x{inventorySlots.GetLength(1)}" : "Not created")}");
            Debug.Log($"Consume Button: {(consumeButton != null ? consumeButton.name : "NULL")}");
            Debug.Log($"Discard Button: {(discardButton != null ? discardButton.name : "NULL")}");
            Debug.Log($"Time Scale: {Time.timeScale}");
        }

        #endregion
    }
}