using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Debug helper to see what the rover is touching
    /// </summary>
    public class CollectionDebugger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"Rover touched: {other.name} (Tag: {other.tag})");
            
            CollectibleItemObject item = other.GetComponent<CollectibleItemObject>();
            if (item != null)
            {
                Debug.Log($"- Found CollectibleItemObject!");
                Debug.Log($"- Item Data: {(item.ItemData != null ? item.ItemData.itemName : "NULL")}");
            }
        }
    }
}