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
        
        [Header("UI Panels")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject upgradePanel;
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
            if (inventoryManager == null)
            {
                inventoryManager = FindObjectOfType<GridInventoryManager>();
            }
            
            if (upgradeManager == null)
            {
                upgradeManager = FindObjectOfType<UpgradeManager>();
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
            
            // Setup buttons
            if (consumeButton != null)
                consumeButton.onClick.AddListener(OnConsumeButtonClicked);
            
            if (discardButton != null)
                discardButton.onClick.AddListener(OnDiscardButtonClicked);
            
            if (inventoryTabButton != null)
                inventoryTabButton.onClick.AddListener(() => ShowTab(true));
            
            if (upgradeTabButton != null)
                upgradeTabButton.onClick.AddListener(() => ShowTab(false));
            
            // Initialize
            CreateInventoryGrid();
            
            if (inventoryPanel != null)
                inventoryPanel.SetActive(false);
            
            if (actionButtonsPanel != null)
                actionButtonsPanel.SetActive(false);
            
            if (itemInfoPanel != null)
                itemInfoPanel.SetActive(false);
        }

        private void CreateInventoryGrid()
        {
            if (gridContainer == null || inventorySlotPrefab == null || inventoryManager == null)
            {
                Debug.LogError("InventoryUI: Missing references for grid creation!");
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
                }
            }
            
            Debug.Log($"Created {width}x{height} inventory grid");
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
            if (selectedSlot != null && selectedSlot.HasItem)
            {
                inventoryManager.ConsumeItem(selectedSlot.GridX, selectedSlot.GridY);
                selectedSlot = null;
                HideItemInfo();
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(false);
            }
        }

        private void OnDiscardButtonClicked()
        {
            if (selectedSlot != null && selectedSlot.HasItem)
            {
                inventoryManager.DiscardItem(selectedSlot.GridX, selectedSlot.GridY);
                selectedSlot = null;
                HideItemInfo();
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(false);
            }
        }

        private void ShowTab(bool showInventory)
        {
            if (inventoryPanel != null)
                inventoryPanel.SetActive(showInventory);
            
            if (upgradePanel != null)
                upgradePanel.SetActive(!showInventory);
        }

        public void OpenInventory()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(true);
                isOpen = true;
                Time.timeScale = 0f; // Pause game
                UpdateResourceDisplay();
            }
        }

        public void CloseInventory()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
                isOpen = false;
                Time.timeScale = 1f; // Unpause game
                HideItemInfo();
                
                if (actionButtonsPanel != null)
                    actionButtonsPanel.SetActive(false);
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
    }
}