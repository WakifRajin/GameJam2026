using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Spawns collectible items around the level
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class SpawnConfig
        {
            public CollectibleItem itemData;
            public int count = 1;
            public GameObject prefab; // Optional override prefab
        }
        
        [Header("Spawn Configuration")]
        [SerializeField] private List<SpawnConfig> itemsToSpawn = new List<SpawnConfig>();
        [SerializeField] private GameObject defaultItemPrefab;
        
        [Header("Spawn Area")]
        [SerializeField] private Vector3 spawnAreaCenter = Vector3.zero;
        [SerializeField] private Vector3 spawnAreaSize = new Vector3(50f, 0f, 50f);
        [SerializeField] private LayerMask groundLayer = -1;
        [SerializeField] private float heightOffset = 1f;
        [SerializeField] private float spawnHeightCheck = 100f;
        
        [Header("Spawn Rules")]
        [SerializeField] private float minDistanceBetweenItems = 5f;
        [SerializeField] private int maxSpawnAttempts = 50;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool useTerrainHeight = true;
        
        private List<Vector3> spawnedPositions = new List<Vector3>();

        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnAllItems();
            }
        }

        public void SpawnAllItems()
        {
            spawnedPositions.Clear();
            
            // Validation checks
            if (itemsToSpawn == null || itemsToSpawn.Count == 0)
            {
                Debug.LogError("ItemSpawner: No items configured to spawn! Check Inspector.");
                return;
            }
            
            if (defaultItemPrefab == null)
            {
                Debug.LogError("ItemSpawner: No default item prefab assigned! Drag a prefab to 'Default Item Prefab' field.");
                return;
            }
            
            Debug.Log($"=== Starting Item Spawn Process ===");
            Debug.Log($"Total spawn configs: {itemsToSpawn.Count}");
            Debug.Log($"Default prefab: {defaultItemPrefab.name}");
            
            int totalItemsSpawned = 0;
            
            for (int configIndex = 0; configIndex < itemsToSpawn.Count; configIndex++)
            {
                var config = itemsToSpawn[configIndex];
                
                if (config == null)
                {
                    Debug.LogWarning($"ItemSpawner: Spawn config at index {configIndex} is null, skipping");
                    continue;
                }
                
                if (config.itemData == null)
                {
                    Debug.LogWarning($"ItemSpawner: Spawn config at index {configIndex} has null item data, skipping");
                    continue;
                }
                
                Debug.Log($"[{configIndex}] Attempting to spawn {config.count}x {config.itemData.itemName}");
                
                for (int i = 0; i < config.count; i++)
                {
                    if (SpawnItem(config, i))
                    {
                        totalItemsSpawned++;
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to spawn {config.itemData.itemName} #{i + 1}");
                    }
                }
            }
            
            Debug.Log($"✓ ItemSpawner: Successfully spawned {totalItemsSpawned} items total");
        }

        private bool SpawnItem(SpawnConfig config, int itemIndex)
        {
            // Additional null check
            if (config == null)
            {
                Debug.LogError("SpawnItem: config is null!");
                return false;
            }
            
            if (config.itemData == null)
            {
                Debug.LogError("SpawnItem: config.itemData is null!");
                return false;
            }
            
            Vector3 spawnPosition = FindValidSpawnPosition();
            
            if (spawnPosition == Vector3.zero)
            {
                Debug.LogWarning($"Could not find valid spawn position for {config.itemData.itemName} after {maxSpawnAttempts} attempts");
                return false;
            }
            
            // Choose prefab
            GameObject prefab = config.prefab != null ? config.prefab : defaultItemPrefab;
            
            if (prefab == null)
            {
                Debug.LogError("No prefab available for item spawning! Both config.prefab and defaultItemPrefab are null!");
                return false;
            }
            
            // Spawn item
            GameObject itemObj = null;
            try
            {
                itemObj = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to instantiate prefab: {e.Message}");
                return false;
            }
            
            if (itemObj == null)
            {
                Debug.LogError("Instantiate returned null!");
                return false;
            }
            
            itemObj.name = $"{config.itemData.itemName}_{itemIndex}";
            
            // Setup item data
            CollectibleItemObject itemComponent = itemObj.GetComponent<CollectibleItemObject>();
            if (itemComponent == null)
            {
                Debug.Log($"Adding CollectibleItemObject component to {itemObj.name}");
                itemComponent = itemObj.AddComponent<CollectibleItemObject>();
            }
            
            if (itemComponent == null)
            {
                Debug.LogError("Failed to get or add CollectibleItemObject component!");
                Destroy(itemObj);
                return false;
            }
            
            // Set the item data using the public property
            try
            {
                itemComponent.ItemData = config.itemData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to set ItemData: {e.Message}");
                Destroy(itemObj);
                return false;
            }
            
            spawnedPositions.Add(spawnPosition);
            Debug.Log($"✓ Spawned {config.itemData.itemName} at {spawnPosition}");
            
            return true;
        }

        private Vector3 FindValidSpawnPosition()
        {
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                // Random position in spawn area (XZ plane)
                Vector3 randomPos = spawnAreaCenter + new Vector3(
                    Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
                    0f,
                    Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f)
                );
                
                // Find ground height
                Vector3 groundPos = Vector3.zero;
                
                if (useTerrainHeight)
                {
                    // Raycast downward from high up to find ground
                    Vector3 rayStart = randomPos + Vector3.up * spawnHeightCheck;
                    RaycastHit hit;
                    
                    if (Physics.Raycast(rayStart, Vector3.down, out hit, spawnHeightCheck * 2f, groundLayer))
                    {
                        groundPos = hit.point + Vector3.up * heightOffset;
                    }
                    else
                    {
                        // No ground found, try next position
                        continue;
                    }
                }
                else
                {
                    // Use the spawn area center Y position
                    groundPos = new Vector3(randomPos.x, spawnAreaCenter.y + heightOffset, randomPos.z);
                }
                
                // Check distance from other items
                bool validDistance = true;
                foreach (var pos in spawnedPositions)
                {
                    if (Vector3.Distance(groundPos, pos) < minDistanceBetweenItems)
                    {
                        validDistance = false;
                        break;
                    }
                }
                
                if (validDistance)
                {
                    return groundPos;
                }
            }
            
            return Vector3.zero; // Failed to find valid position
        }

        private void OnDrawGizmosSelected()
        {
            // Draw spawn area
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
            
            // Draw spawned positions in play mode
            if (Application.isPlaying && spawnedPositions != null)
            {
                Gizmos.color = Color.green;
                foreach (var pos in spawnedPositions)
                {
                    Gizmos.DrawSphere(pos, 0.5f);
                    Gizmos.DrawLine(pos, pos + Vector3.up * 2f);
                }
                
                // Draw minimum distance circles
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                foreach (var pos in spawnedPositions)
                {
                    Gizmos.DrawWireSphere(pos, minDistanceBetweenItems);
                }
            }
        }

        [ContextMenu("Spawn Items Now")]
        private void DebugSpawnItems()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Can only spawn items in Play Mode!");
                return;
            }
            
            // Clear existing items first
            DebugClearItems();
            SpawnAllItems();
        }

        [ContextMenu("Clear All Items")]
        private void DebugClearItems()
        {
            // Clear spawned items
            List<GameObject> toDestroy = new List<GameObject>();
            
            if (transform.childCount > 0)
            {
                foreach (Transform child in transform)
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            
            foreach (var obj in toDestroy)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
            
            if (spawnedPositions != null)
            {
                spawnedPositions.Clear();
            }
            
            Debug.Log("All spawned items cleared");
        }

        [ContextMenu("Print Spawn Config")]
        private void DebugPrintConfig()
        {
            Debug.Log("=== ITEM SPAWNER CONFIG ===");
            
            if (itemsToSpawn == null)
            {
                Debug.LogError("itemsToSpawn list is NULL!");
                return;
            }
            
            Debug.Log($"Total spawn configs: {itemsToSpawn.Count}");
            
            int totalItems = 0;
            for (int i = 0; i < itemsToSpawn.Count; i++)
            {
                var config = itemsToSpawn[i];
                if (config == null)
                {
                    Debug.LogWarning($"[{i}] Config is NULL");
                }
                else if (config.itemData == null)
                {
                    Debug.LogWarning($"[{i}] Item Data is NULL");
                }
                else
                {
                    Debug.Log($"[{i}] {config.itemData.itemName}: {config.count} items (Prefab: {(config.prefab != null ? config.prefab.name : "using default")})");
                    totalItems += config.count;
                }
            }
            
            Debug.Log($"Total items to spawn: {totalItems}");
            Debug.Log($"Default Prefab: {(defaultItemPrefab != null ? defaultItemPrefab.name : "NOT ASSIGNED - ERROR!")}");
            Debug.Log($"Spawn Area Center: {spawnAreaCenter}");
            Debug.Log($"Spawn Area Size: {spawnAreaSize}");
            Debug.Log($"Min Distance: {minDistanceBetweenItems}");
            Debug.Log($"Use Terrain Height: {useTerrainHeight}");
            Debug.Log($"Ground Layer: {groundLayer.value}");
        }

        [ContextMenu("Validate Setup")]
        private void DebugValidateSetup()
        {
            Debug.Log("=== VALIDATING ITEM SPAWNER SETUP ===");
            
            bool isValid = true;
            
            // Check default prefab
            if (defaultItemPrefab == null)
            {
                Debug.LogError("❌ Default Item Prefab is NOT assigned!");
                isValid = false;
            }
            else
            {
                Debug.Log($"✓ Default Item Prefab: {defaultItemPrefab.name}");
                
                // Check if prefab has required components
                var itemComp = defaultItemPrefab.GetComponent<CollectibleItemObject>();
                if (itemComp == null)
                {
                    Debug.LogWarning("⚠ Default prefab doesn't have CollectibleItemObject (will be added at runtime)");
                }
                
                var collider = defaultItemPrefab.GetComponent<Collider>();
                if (collider == null)
                {
                    Debug.LogError("❌ Default prefab has NO COLLIDER!");
                    isValid = false;
                }
                else
                {
                    Debug.Log($"✓ Prefab has collider: {collider.GetType().Name}");
                }
            }
            
            // Check spawn configs
            if (itemsToSpawn == null || itemsToSpawn.Count == 0)
            {
                Debug.LogError("❌ No spawn configs set up!");
                isValid = false;
            }
            else
            {
                Debug.Log($"✓ Found {itemsToSpawn.Count} spawn configs");
            }
            
            // Check spawn area
            if (spawnAreaSize.magnitude < 1f)
            {
                Debug.LogWarning("⚠ Spawn area seems very small!");
            }
            else
            {
                Debug.Log($"✓ Spawn area size: {spawnAreaSize}");
            }
            
            Debug.Log(isValid ? "✓ Setup is valid!" : "❌ Setup has errors - fix them before spawning!");
        }
    }
}