using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GameJam2026
{
    /// <summary>
    /// Individual inventory slot with drag and drop support
    /// </summary>
    public class InventorySlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [Header("UI Elements")]
        [SerializeField] private Image itemIconImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private GameObject highlightOverlay;
        
        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color highlightColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        
        private CollectibleItem currentItem;
        private int gridX;
        private int gridY;
        private InventoryUI inventoryUI;
        private Canvas canvas;
        private GameObject draggedObject;
        
        public bool HasItem => currentItem != null;
        public int GridX => gridX;
        public int GridY => gridY;

        public void Initialize(int x, int y, InventoryUI ui)
        {
            gridX = x;
            gridY = y;
            inventoryUI = ui;
            canvas = GetComponentInParent<Canvas>();
            
            ClearItem();
            
            if (highlightOverlay != null)
                highlightOverlay.SetActive(false);
        }

        public void SetItem(CollectibleItem item)
        {
            currentItem = item;
            
            if (item != null)
            {
                if (itemIconImage != null && item.icon != null)
                {
                    itemIconImage.sprite = item.icon;
                    itemIconImage.enabled = true;
                    itemIconImage.color = Color.white;
                }
                
                if (itemNameText != null)
                {
                    itemNameText.text = item.itemName;
                    itemNameText.enabled = true;
                }
                
                if (backgroundImage != null)
                {
                    backgroundImage.color = normalColor;
                }
            }
            else
            {
                ClearItem();
            }
        }

        public void ClearItem()
        {
            currentItem = null;
            
            if (itemIconImage != null)
            {
                itemIconImage.sprite = null;
                itemIconImage.enabled = false;
            }
            
            if (itemNameText != null)
            {
                itemNameText.text = "";
                itemNameText.enabled = false;
            }
            
            if (backgroundImage != null)
            {
                backgroundImage.color = emptyColor;
            }
        }

        public CollectibleItem GetItem()
        {
            return currentItem;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (inventoryUI != null)
            {
                inventoryUI.OnSlotClicked(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (currentItem == null) return;
            
            // Create visual representation of dragged item
            draggedObject = new GameObject("DraggedItem");
            draggedObject.transform.SetParent(canvas.transform, false);
            draggedObject.transform.SetAsLastSibling();
            
            Image dragImage = draggedObject.AddComponent<Image>();
            dragImage.sprite = itemIconImage.sprite;
            dragImage.raycastTarget = false;
            dragImage.SetNativeSize();
            
            CanvasGroup canvasGroup = draggedObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (draggedObject != null)
            {
                draggedObject.transform.position = eventData.position;
            }
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
            InventorySlot sourceSlot = eventData.pointerDrag?.GetComponent<InventorySlot>();
            
            if (sourceSlot != null && sourceSlot != this)
            {
                // Attempt to move item
                GridInventoryManager inventory = FindObjectOfType<GridInventoryManager>();
                if (inventory != null)
                {
                    inventory.MoveItem(sourceSlot.gridX, sourceSlot.gridY, gridX, gridY);
                }
            }
        }

        public void SetHighlight(bool highlighted)
        {
            if (highlightOverlay != null)
            {
                highlightOverlay.SetActive(highlighted);
            }
            
            if (backgroundImage != null && currentItem != null)
            {
                backgroundImage.color = highlighted ? highlightColor : normalColor;
            }
        }
    }
}