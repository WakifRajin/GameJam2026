using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// Defines a single objective for a level
    /// </summary>
    [CreateAssetMenu(fileName = "New Objective", menuName = "GameJam2026/Game Objective")]
    public class GameObjective : ScriptableObject
    {
        [Header("Objective Info")]
        public string objectiveTitle = "Objective";
        [TextArea(2, 4)]
        public string objectiveDescription = "Complete this objective";
        public Sprite objectiveIcon;
        
        [Header("Objective Type")]
        public ObjectiveType objectiveType = ObjectiveType.CollectItems;
        
        [Header("Target")]
        public float targetValue = 1f;
        public string targetResourceType = ""; // For CollectItems: "Material", "PowerCell", etc.
        public GameObject targetObject; // For RepairObject, ActivateObject
        public Transform targetLocation; // For ReachLocation
        
        [Header("Settings")]
        public bool isOptional = false;
        public bool trackProgress = true;
    }

    public enum ObjectiveType
    {
        CollectItems,       // Collect X items of a type
        ReachLocation,      // Get to a location
        Survival,           // Survive for X seconds
        RepairObject,       // Repair a specific object
        ActivateObject,     // Activate something
        DefeatEnemies,      // Future use
        Custom              // Custom logic
    }
}