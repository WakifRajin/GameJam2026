using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// One grid cell. Renders a stack and handles click, drag and drop.
    /// </summary>
    public class InventorySlot : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("UI Elements")]
        [SerializeField] private Image itemIconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [Tooltip("Stack count badge. Hidden for single items.")]
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private GameObject highlightOverlay;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color highlightColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.35f, 0.5f, 1f);
        [SerializeField] private Color dropTargetColor = new Color(0.2f, 0.6f, 0.3f, 1f);

        private CollectibleItem currentItem;
        private int currentQuantity;
        private int gridX;
        private int gridY;
        private InventoryUI inventoryUI;
        private Canvas canvas;
        private GameObject draggedObject;
        private bool isSelected;
        private bool isHovered;

        public bool HasItem => currentItem != null && currentQuantity > 0;
        public int GridX => gridX;
        public int GridY => gridY;
        public int Quantity => currentQuantity;

        public void Initialize(int x, int y, InventoryUI ui)
        {
            gridX = x;
            gridY = y;
            inventoryUI = ui;
            canvas = GetComponentInParent<Canvas>();

            ClearItem();

            if (highlightOverlay != null) highlightOverlay.SetActive(false);
        }

        /// <summary>Renders a stack. Passing null or an empty stack clears the slot.</summary>
        public void SetStack(ItemStack stack)
        {
            if (stack == null || stack.IsEmpty) ClearItem();
            else SetItem(stack.item, stack.quantity);
        }

        public void SetItem(CollectibleItem item, int quantity = 1)
        {
            if (item == null || quantity <= 0)
            {
                ClearItem();
                return;
            }

            currentItem = item;
            currentQuantity = quantity;

            if (itemIconImage != null)
            {
                itemIconImage.sprite = item.icon;
                itemIconImage.enabled = item.icon != null;
                itemIconImage.color = Color.white;
            }

            if (itemNameText != null)
            {
                itemNameText.text = item.itemName;
                itemNameText.enabled = true;
            }

            if (quantityText != null)
            {
                // A "x1" badge on every slot is just noise.
                quantityText.text = quantity > 1 ? $"x{quantity}" : string.Empty;
                quantityText.enabled = quantity > 1;
            }

            ApplyBackgroundColor();
        }

        public void ClearItem()
        {
            currentItem = null;
            currentQuantity = 0;

            if (itemIconImage != null)
            {
                itemIconImage.sprite = null;
                itemIconImage.enabled = false;
            }

            if (itemNameText != null)
            {
                itemNameText.text = string.Empty;
                itemNameText.enabled = false;
            }

            if (quantityText != null)
            {
                quantityText.text = string.Empty;
                quantityText.enabled = false;
            }

            ApplyBackgroundColor();
        }

        public CollectibleItem GetItem() => currentItem;

        private void ApplyBackgroundColor()
        {
            if (backgroundImage == null) return;

            if (isSelected) backgroundImage.color = highlightColor;
            else if (isHovered) backgroundImage.color = hoverColor;
            else backgroundImage.color = HasItem ? normalColor : emptyColor;
        }

        public void SetHighlight(bool highlighted)
        {
            isSelected = highlighted;
            if (highlightOverlay != null) highlightOverlay.SetActive(highlighted);
            ApplyBackgroundColor();
        }

        #region Pointer

        public void OnPointerClick(PointerEventData eventData)
        {
            if (inventoryUI == null) return;

            if (eventData.button == PointerEventData.InputButton.Right) inventoryUI.OnSlotRightClicked(this);
            else inventoryUI.OnSlotClicked(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            ApplyBackgroundColor();
            if (HasItem && inventoryUI != null) inventoryUI.OnSlotHovered(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            ApplyBackgroundColor();
            if (inventoryUI != null) inventoryUI.OnSlotHoverExit(this);
        }

        #endregion

        #region Drag and drop

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!HasItem || canvas == null) return;

            draggedObject = new GameObject("DraggedItem", typeof(RectTransform));
            draggedObject.transform.SetParent(canvas.transform, false);
            draggedObject.transform.SetAsLastSibling();

            Image dragImage = draggedObject.AddComponent<Image>();
            dragImage.sprite = itemIconImage != null ? itemIconImage.sprite : null;
            dragImage.raycastTarget = false;
            if (dragImage.sprite != null) dragImage.SetNativeSize();

            CanvasGroup canvasGroup = draggedObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (draggedObject != null) draggedObject.transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (draggedObject != null)
            {
                Destroy(draggedObject);
                draggedObject = null;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null) return;

            InventorySlot sourceSlot = eventData.pointerDrag.GetComponent<InventorySlot>();
            if (sourceSlot == null || sourceSlot == this) return;

            // Route through the UI, which holds the manager reference - no per-drop FindObjectOfType.
            inventoryUI?.RequestMove(sourceSlot, this);
        }

        #endregion
    }
}
