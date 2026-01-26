using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// World object that can be collected by the player
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CollectibleItemObject : MonoBehaviour
    {
        [Header("Item Data")]
        [SerializeField] private CollectibleItem itemData;
        
        [Header("Visual Settings")]
        [SerializeField] private GameObject visualModel;
        [SerializeField] private Light glowLight;
        [SerializeField] private bool rotateItem = true;
        [SerializeField] private float rotationSpeed = 50f;
        [SerializeField] private bool bobUpDown = true;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.3f;
        
        [Header("Collection Settings")]
        [SerializeField] private float collectionRadius = 3f;
        [SerializeField] private AudioClip collectSound;
        [SerializeField] private GameObject collectEffectPrefab;
        
        private Vector3 startPosition;
        private float bobTime;
        private bool isCollected = false;

        /// <summary>
        /// Public property to allow ItemSpawner to set item data
        /// </summary>
        public CollectibleItem ItemData
        {
            get { return itemData; }
            set 
            { 
                itemData = value;
                if (Application.isPlaying && itemData != null)
                {
                    SetupVisual();
                }
            }
        }

        private void Start()
        {
            startPosition = transform.position;
            
            // Setup collider as trigger
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
            else
            {
                Debug.LogWarning($"CollectibleItemObject '{gameObject.name}' has no collider!");
            }
            
            // Setup visual
            if (itemData != null)
            {
                SetupVisual();
            }
            else
            {
                Debug.LogWarning($"CollectibleItemObject '{gameObject.name}' has no item data assigned!");
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // Rotate item
            if (rotateItem)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }

            // Bob up and down
            if (bobUpDown)
            {
                bobTime += Time.deltaTime * bobSpeed;
                float newY = startPosition.y + Mathf.Sin(bobTime) * bobHeight;
                transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            }
        }

        private void SetupVisual()
        {
            // Setup glow light color
            if (glowLight != null && itemData != null)
            {
                glowLight.color = itemData.glowColor;
                glowLight.intensity = 2f;
                glowLight.range = 5f;
            }
            
            // Instantiate model if specified and not already present
            if (itemData != null && itemData.worldModelPrefab != null && visualModel == null)
            {
                visualModel = Instantiate(itemData.worldModelPrefab, transform);
                visualModel.transform.localPosition = Vector3.zero;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;

            // Check if player/rover entered
            if (other.CompareTag("Player") || other.GetComponent<RoverAttributeManager>() != null)
            {
                TryCollect(other.gameObject);
            }
        }

        private void TryCollect(GameObject collector)
        {
            if (itemData == null)
            {
                Debug.LogError($"Cannot collect item - no item data on {gameObject.name}");
                return;
            }

            // Get the inventory from the collector
            InventoryManager inventory = collector.GetComponent<InventoryManager>();
            
            if (inventory != null)
            {
                // Check if there's space (weight limit)
                if (inventory.CanAddItem(itemData))
                {
                    // Add to inventory
                    bool added = inventory.AddItem(itemData);
                    
                    if (added)
                    {
                        // Mark as collected
                        Collect();
                    }
                }
                else
                {
                    Debug.Log($"Cannot collect {itemData.itemName} - inventory full!");
                }
            }
            else
            {
                Debug.LogWarning($"Collector {collector.name} has no InventoryManager!");
            }
        }

        private void Collect()
        {
            isCollected = true;
            
            // Play sound
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position);
            }
            
            // Spawn effect
            if (collectEffectPrefab != null)
            {
                Instantiate(collectEffectPrefab, transform.position, Quaternion.identity);
            }
            
            Debug.Log($"Collected: {itemData.itemName}");
            
            // Destroy the object
            Destroy(gameObject, 0.1f);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw collection radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, collectionRadius);
        }

        public CollectibleItem GetItemData()
        {
            return itemData;
        }
    }
}