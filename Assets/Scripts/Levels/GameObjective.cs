using UnityEngine;
using System;

namespace GameJam2026
{
    /// <summary>
    /// Defines a single objective/goal in a level
    /// </summary>
    [CreateAssetMenu(fileName = "New Objective", menuName = "GameJam2026/Game Objective")]
    public class GameObjective : ScriptableObject
    {
        [Header("Objective Info")]
        public string objectiveTitle = "Objective";
        [TextArea(2, 4)]
        public string objectiveDescription = "Complete this objective";
        public ObjectiveType type;
        
        [Header("Requirements")]
        public float targetValue = 1f; // Amount needed (items, power, etc.)
        public string targetResourceType = ""; // For resource-based objectives
        
        [Header("Completion")]
        public bool isOptional = false;
        public GameObject targetObject; // For location-based objectives
        
        [Header("UI")]
        public Sprite icon;
    }

    public enum ObjectiveType
    {
        CollectItems,      // Collect X items
        ReachLocation,     // Get to a specific point
        RestorePower,      // Restore power to X amount
        RepairObject,      // Repair/activate an object
        SurviveUntil,      // Survive until time/condition
        UseExchange        // Perform specific exchange
    }
}