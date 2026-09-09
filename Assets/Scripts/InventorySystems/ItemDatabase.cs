using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Name -> CollectibleItem lookup. Saves store item names, so a save can only be restored
    /// if every referenced item is reachable through this database.
    /// Create one via Assets > Create > GameJam2026 > Item Database and hit "Find All Items".
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "GameJam2026/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<CollectibleItem> items = new List<CollectibleItem>();

        private Dictionary<string, CollectibleItem> lookup;

        public IReadOnlyList<CollectibleItem> Items => items;

        private void BuildLookup()
        {
            lookup = new Dictionary<string, CollectibleItem>();
            foreach (var item in items)
            {
                if (item == null) continue;
                string key = string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
                if (!lookup.ContainsKey(key))
                {
                    lookup.Add(key, item);
                }
                else
                {
                    Debug.LogWarning($"ItemDatabase: duplicate item name '{key}'. Saves may restore the wrong asset.");
                }
            }
        }

        public CollectibleItem Find(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            if (lookup == null || lookup.Count != items.Count) BuildLookup();
            return lookup.TryGetValue(itemName, out CollectibleItem item) ? item : null;
        }

        public void Register(CollectibleItem item)
        {
            if (item == null || items.Contains(item)) return;
            items.Add(item);
            lookup = null;
        }

#if UNITY_EDITOR
        [ContextMenu("Find All Items")]
        private void FindAllItems()
        {
            items.Clear();
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(CollectibleItem)}");
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<CollectibleItem>(path);
                if (item != null) items.Add(item);
            }
            lookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"ItemDatabase: found {items.Count} items.");
        }
#endif
    }
}
