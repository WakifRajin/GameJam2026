using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// Grid inventory UI. Redraws from the manager rather than tracking state of its own, so a
    /// resize, a load, or items picked up before the UI woke can never leave it out of sync.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public static InventoryUI Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GridInventoryManager inventoryManager;
        [SerializeField] private UpgradeManager upgradeManager;

        [Header("Main Panel (Contains everything)")]
        [SerializeField] private GameObject inventoryPanel;

        [Header("Sub-Containers (Inside main panel)")]
        [SerializeField] private GameObject inventoryContainer;
        [SerializeField] private GameObject upgradeContainer;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private GameObject inventorySlotPrefab;

        [Header("Grid Layout")]
        [SerializeField] private Vector2 slotSize = new Vector2(80f, 80f);
        [SerializeField] private Vector2 slotSpacing = new Vector2(5f, 5f);

        [Header("Info Display")]
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private Image itemIconImage;
        [SerializeField] private GameObject itemInfoPanel;
        [Tooltip("Optional line for weight / stack / resource value.")]
        [SerializeField] private TextMeshProUGUI itemStatsText;

        [Header("Action Buttons")]
        [SerializeField] private Button consumeButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Button sortButton;
        [SerializeField] private GameObject actionButtonsPanel;

        [Header("Resource Display")]
        [SerializeField] private TextMeshProUGUI materialsText;
        [SerializeField] private TextMeshProUGUI techText;
        [SerializeField] private TextMeshProUGUI powerText;
        [Tooltip("Optional '12.5 / 50 kg' cargo readout.")]
        [SerializeField] private TextMeshProUGUI weightText;
        [Tooltip("Optional '7 / 24 slots' readout.")]
        [SerializeField] private TextMeshProUGUI slotsText;

        [Header("Tabs")]
        [SerializeField] private Button inventoryTabButton;
        [SerializeField] private Button upgradeTabButton;

        [Header("Pause Settings")]
        [SerializeField] private bool pauseGameWhenOpen = true;
        [SerializeField] private CanvasGroup gameplayUIGroup;

        private InventorySlot[,] inventorySlots;
        private InventorySlot selectedSlot;
        private bool isOpen;

        // Restored on close instead of hardcoding 1, so closing the inventory from inside the
        // pause menu does not resume the game.
        private float timeScaleBeforeOpen = 1f;
        private bool cursorVisibleBeforeOpen;
        private CursorLockMode cursorLockBeforeOpen;

        public bool IsOpen => isOpen;

        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (inventoryManager == null) inventoryManager = FindObjectOfType<GridInventoryManager>();
            if (upgradeManager == null) upgradeManager = FindObjectOfType<UpgradeManager>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                Debug.LogError("InventoryUI: no EventSystem in the scene - UI clicks will not register. Add GameObject > UI > Event System.");
            }

            if (inventoryManager != null)
            {
                inventoryManager.OnSlotChanged += HandleSlotChanged;
                inventoryManager.OnInventoryRebuilt += RebuildGrid;
                inventoryManager.OnResourceChanged += HandleResourceChanged;
                inventoryManager.OnWeightChanged += HandleWeightChanged;
                inventoryManager.OnPickupRejected += HandlePickupRejected;
                inventoryManager.OnInventoryOpened += OpenInventory;
                inventoryManager.OnInventoryClosed += CloseInventory;
            }
            else
            {
                Debug.LogError("InventoryUI: GridInventoryManager not found.");
            }

            if (consumeButton != null) consumeButton.onClick.AddListener(OnConsumeButtonClicked);
            if (discardButton != null) discardButton.onClick.AddListener(OnDiscardButtonClicked);
            if (sortButton != null) sortButton.onClick.AddListener(OnSortButtonClicked);
            if (inventoryTabButton != null) inventoryTabButton.onClick.AddListener(() => ShowTab(true));
            if (upgradeTabButton != null) upgradeTabButton.onClick.AddListener(() => ShowTab(false));

            RebuildGrid();

            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            ShowTab(true);
            ClearSelection();
        }

        private void OnDestroy()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnSlotChanged -= HandleSlotChanged;
                inventoryManager.OnInventoryRebuilt -= RebuildGrid;
                inventoryManager.OnResourceChanged -= HandleResourceChanged;
                inventoryManager.OnWeightChanged -= HandleWeightChanged;
                inventoryManager.OnPickupRejected -= HandlePickupRejected;
                inventoryManager.OnInventoryOpened -= OpenInventory;
                inventoryManager.OnInventoryClosed -= CloseInventory;
            }

            if (Instance == this) Instance = null;
        }

        #endregion

        #region Grid construction

        /// <summary>Destroys and recreates every slot, then repaints from the manager.</summary>
        private void RebuildGrid()
        {
            if (gridContainer == null || inventorySlotPrefab == null || inventoryManager == null)
            {
                if (gridContainer == null) Debug.LogError("InventoryUI: Grid Container not assigned.");
                if (inventorySlotPrefab == null) Debug.LogError("InventoryUI: Inventory Slot Prefab not assigned.");
                return;
            }

            int width = inventoryManager.GridWidth;
            int height = inventoryManager.GridHeight;

            bool sizeMatches = inventorySlots != null
                && inventorySlots.GetLength(0) == width
                && inventorySlots.GetLength(1) == height;

            if (!sizeMatches)
            {
                for (int i = gridContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(gridContainer.GetChild(i).gameObject);
                }

                inventorySlots = new InventorySlot[width, height];
                ConfigureGridLayout(width);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        GameObject slotObj = Instantiate(inventorySlotPrefab, gridContainer);
                        slotObj.name = $"Slot_{x}_{y}";

                        InventorySlot slot = slotObj.GetComponent<InventorySlot>();
                        if (slot == null)
                        {
                            Debug.LogError("InventoryUI: slot prefab has no InventorySlot component.");
                            continue;
                        }

                        slot.Initialize(x, y, this);
                        inventorySlots[x, y] = slot;
                    }
                }
            }

            RefreshAllSlots();
            ClearSelection();
            UpdateResourceDisplay();
        }

        private void ConfigureGridLayout(int columns)
        {
            GridLayoutGroup gridLayout = gridContainer.GetComponent<GridLayoutGroup>();
            if (gridLayout == null) gridLayout = gridContainer.gameObject.AddComponent<GridLayoutGroup>();

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columns;
            gridLayout.cellSize = slotSize;
            gridLayout.spacing = slotSpacing;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
        }

        private void RefreshAllSlots()
        {
            if (inventorySlots == null || inventoryManager == null) return;

            for (int y = 0; y < inventorySlots.GetLength(1); y++)
            {
                for (int x = 0; x < inventorySlots.GetLength(0); x++)
                {
                    inventorySlots[x, y]?.SetStack(inventoryManager.GetStackAt(x, y));
                }
            }
        }

        #endregion

        #region Manager events

        private void HandleSlotChanged(int x, int y)
        {
            if (inventorySlots == null || inventoryManager == null) return;
            if (x < 0 || x >= inventorySlots.GetLength(0) || y < 0 || y >= inventorySlots.GetLength(1)) return;

            inventorySlots[x, y]?.SetStack(inventoryManager.GetStackAt(x, y));

            // The selected slot may have just been emptied by a spend elsewhere.
            if (selectedSlot != null && selectedSlot.GridX == x && selectedSlot.GridY == y && !selectedSlot.HasItem)
            {
                ClearSelection();
            }

            UpdateResourceDisplay();
        }

        private void HandleResourceChanged(string resourceType, float amount) => UpdateResourceDisplay();

        private void HandleWeightChanged(float current, float capacity) => UpdateResourceDisplay();

        private void HandlePickupRejected(CollectibleItem item, string reason)
        {
            Debug.Log($"Could not pick up {item.itemName}: {reason}");
        }

        #endregion

        #region Display

        private void UpdateResourceDisplay()
        {
            if (inventoryManager == null) return;

            if (materialsText != null)
                materialsText.text = $"Materials: {inventoryManager.GetResource(ResourceIds.Materials):F0}";

            if (techText != null)
                techText.text = $"Tech: {inventoryManager.GetResource(ResourceIds.Tech):F0}";

            if (powerText != null)
                powerText.text = $"Power: {inventoryManager.GetResource(ResourceIds.Power):F0}";

            if (weightText != null)
                weightText.text = $"{inventoryManager.GetTotalWeight():F1} / {inventoryManager.CargoCapacity:F0} kg";

            if (slotsText != null)
                slotsText.text = $"{inventoryManager.UsedSlots} / {inventoryManager.TotalSlots} slots";
        }

        private void ShowItemInfo(InventorySlot slot)
        {
            CollectibleItem item = slot.GetItem();
            if (item == null)
            {
                HideItemInfo();
                return;
            }

            if (itemInfoPanel != null) itemInfoPanel.SetActive(true);
            if (itemNameText != null) itemNameText.text = slot.Quantity > 1 ? $"{item.itemName} x{slot.Quantity}" : item.itemName;
            if (itemDescriptionText != null) itemDescriptionText.text = item.description;

            if (itemIconImage != null)
            {
                itemIconImage.sprite = item.icon;
                itemIconImage.enabled = item.icon != null;
            }

            if (itemStatsText != null)
            {
                string resource = ResourceIds.Of(item);
                float value = ResourceIds.ValueOf(item) * slot.Quantity;
                itemStatsText.text =
                    $"Weight: {item.weight * slot.Quantity:F1} kg\n" +
                    $"{resource}: {value:F1}\n" +
                    $"Stack: {slot.Quantity} / {Mathf.Max(1, item.stackSize)}";
            }
        }

        private void HideItemInfo()
        {
            if (itemInfoPanel != null) itemInfoPanel.SetActive(false);
            if (itemStatsText != null) itemStatsText.text = string.Empty;
        }

        private void ClearSelection()
        {
            selectedSlot?.SetHighlight(false);
            selectedSlot = null;

            HideItemInfo();
            if (actionButtonsPanel != null) actionButtonsPanel.SetActive(false);
        }

        #endregion

        #region Slot callbacks

        public void OnSlotClicked(InventorySlot slot)
        {
            if (selectedSlot != null && selectedSlot != slot) selectedSlot.SetHighlight(false);

            if (!slot.HasItem)
            {
                ClearSelection();
                return;
            }

            selectedSlot = slot;
            slot.SetHighlight(true);

            ShowItemInfo(slot);
            if (actionButtonsPanel != null) actionButtonsPanel.SetActive(true);
        }

        /// <summary>Right click splits a stack in half, or uses a single item.</summary>
        public void OnSlotRightClicked(InventorySlot slot)
        {
            if (!slot.HasItem || inventoryManager == null) return;

            if (slot.Quantity > 1) inventoryManager.SplitStack(slot.GridX, slot.GridY, slot.Quantity / 2);
            else inventoryManager.ConsumeItem(slot.GridX, slot.GridY);
        }

        public void OnSlotHovered(InventorySlot slot)
        {
            // Only preview while nothing is pinned, so hovering does not stomp a selection.
            if (selectedSlot == null) ShowItemInfo(slot);
        }

        public void OnSlotHoverExit(InventorySlot slot)
        {
            if (selectedSlot == null) HideItemInfo();
        }

        public void RequestMove(InventorySlot from, InventorySlot to)
        {
            if (inventoryManager == null || from == null || to == null) return;
            inventoryManager.MoveItem(from.GridX, from.GridY, to.GridX, to.GridY);
        }

        #endregion

        #region Buttons

        private void OnConsumeButtonClicked()
        {
            if (selectedSlot == null || !selectedSlot.HasItem) return;

            inventoryManager.ConsumeItem(selectedSlot.GridX, selectedSlot.GridY);

            if (selectedSlot != null && selectedSlot.HasItem) ShowItemInfo(selectedSlot);
            else ClearSelection();
        }

        private void OnDiscardButtonClicked()
        {
            if (selectedSlot == null || !selectedSlot.HasItem) return;

            inventoryManager.DiscardItem(selectedSlot.GridX, selectedSlot.GridY);
            ClearSelection();
        }

        private void OnSortButtonClicked()
        {
            inventoryManager?.CompactAndSort();
        }

        private void ShowTab(bool showInventory)
        {
            if (inventoryContainer != null) inventoryContainer.SetActive(showInventory);
            if (upgradeContainer != null) upgradeContainer.SetActive(!showInventory);
        }

        #endregion

        #region Open / close

        public void OpenInventory()
        {
            if (isOpen || inventoryPanel == null) return;

            isOpen = true;
            inventoryPanel.SetActive(true);
            ShowTab(true);

            RefreshAllSlots();
            UpdateResourceDisplay();
            ClearSelection();

            if (pauseGameWhenOpen)
            {
                timeScaleBeforeOpen = Time.timeScale;
                Time.timeScale = 0f;
            }

            if (gameplayUIGroup != null)
            {
                gameplayUIGroup.alpha = 0.3f;
                gameplayUIGroup.interactable = false;
            }

            cursorVisibleBeforeOpen = Cursor.visible;
            cursorLockBeforeOpen = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void CloseInventory()
        {
            if (!isOpen || inventoryPanel == null) return;

            isOpen = false;
            inventoryPanel.SetActive(false);

            // Restore rather than force to 1 - the pause menu may legitimately still be paused.
            if (pauseGameWhenOpen) Time.timeScale = timeScaleBeforeOpen;

            if (gameplayUIGroup != null)
            {
                gameplayUIGroup.alpha = 1f;
                gameplayUIGroup.interactable = true;
            }

            Cursor.visible = cursorVisibleBeforeOpen;
            Cursor.lockState = cursorLockBeforeOpen;

            ClearSelection();

            // Keep the manager's open flag in step when the UI closed itself.
            inventoryManager?.CloseInventory();
        }

        /// <summary>Hook for a close button in the panel.</summary>
        public void Close() => inventoryManager?.CloseInventory();

        #endregion

        #region Debug

        [ContextMenu("Force Open Inventory")]
        private void DebugForceOpen() => OpenInventory();

        [ContextMenu("Force Close Inventory")]
        private void DebugForceClose() => CloseInventory();

        [ContextMenu("Rebuild Grid")]
        private void DebugRebuild() => RebuildGrid();

        [ContextMenu("Print UI Status")]
        private void DebugPrintStatus()
        {
            Debug.Log(
                $"=== INVENTORY UI ===\n" +
                $"Open: {isOpen}   TimeScale: {Time.timeScale}\n" +
                $"Panel: {(inventoryPanel != null ? inventoryPanel.name : "NULL")}\n" +
                $"Grid Container: {(gridContainer != null ? gridContainer.name : "NULL")}\n" +
                $"Slot Prefab: {(inventorySlotPrefab != null ? inventorySlotPrefab.name : "NULL")}\n" +
                $"Manager: {(inventoryManager != null ? "found" : "NULL")}\n" +
                $"Slots: {(inventorySlots != null ? $"{inventorySlots.GetLength(0)}x{inventorySlots.GetLength(1)}" : "not built")}\n" +
                $"EventSystem: {(UnityEngine.EventSystems.EventSystem.current != null ? "present" : "MISSING")}");
        }

        #endregion
    }
}
