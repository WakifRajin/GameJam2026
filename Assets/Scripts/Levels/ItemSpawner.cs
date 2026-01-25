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
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float heightOffset = 1f;
        
        [Header("Spawn Rules")]
        [SerializeField] private float minDistanceBetweenItems = 5f;
        [SerializeField] private int maxSpawnAttempts = 50;
        [SerializeField] private bool spawnOnStart = true;
        
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
            
            foreach (var config in itemsToSpawn)
            {
                for (int i = 0; i < config.count; i++)
                {
                    SpawnItem(config);
                }
            }
            
            Debug.Log($"Spawned {spawnedPositions.Count} items");
        }

        private void SpawnItem(SpawnConfig config)
        {
            Vector3 spawnPosition = FindValidSpawnPosition();
            
            if (spawnPosition == Vector3.zero)
            {
                Debug.LogWarning($"Could not find valid spawn position for {config.itemData.itemName}");
                return;
            }
            
            // Choose prefab
            GameObject prefab = config.prefab != null ? config.prefab : defaultItemPrefab;
            
            if (prefab == null)
            {
                Debug.LogError("No prefab assigned for item spawning!");
                return;
            }
            
            // Spawn item
            GameObject itemObj = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);
            
            // Setup item data
            CollectibleItemObject itemComponent = itemObj.GetComponent<CollectibleItemObject>();
            if (itemComponent == null)
            {
                itemComponent = itemObj.AddComponent<CollectibleItemObject>();
            }
            
            // Assign item data via reflection (since the field is private)
            var field = typeof(CollectibleItemObject).GetField("itemData", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(itemComponent, config.itemData);
            }
            
            itemObj.name = $"{config.itemData.itemName}_{spawnedPositions.Count}";
            
            spawnedPositions.Add(spawnPosition);
        }

        private Vector3 FindValidSpawnPosition()
        {
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                // Random position in spawn area
                Vector3 randomPos = spawnAreaCenter + new Vector3(
                    Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
                    0f,
                    Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f)
                );
                
                // Raycast to find ground
                RaycastHit hit;
                if (Physics.Raycast(randomPos + Vector3.up * 100f, Vector3.down, out hit, 200f, groundLayer))
                {
                    Vector3 groundPos = hit.point + Vector3.up * heightOffset;
                    
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
            }
            
            return Vector3.zero; // Failed to find valid position
        }

        private void OnDrawGizmosSelected()
        {
            // Draw spawn area
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
            
            // Draw spawned positions
            Gizmos.color = Color.green;
            foreach (var pos in spawnedPositions)
            {
                Gizmos.DrawSphere(pos, 0.5f);
            }
        }

        [ContextMenu("Spawn Items Now")]
        private void DebugSpawnItems()
        {
            SpawnAllItems();
        }

        [ContextMenu("Clear All Items")]
        private void DebugClearItems()
        {
            foreach (Transform child in transform)
            {
                DestroyImmediate(child.gameObject);
            }
            spawnedPositions.Clear();
        }
    }
}