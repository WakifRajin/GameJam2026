using UnityEngine;
using System;

namespace GameJam2026
{
    /// <summary>
    /// Tracks progress of a single objective
    /// </summary>
    [System.Serializable]
    public class ObjectiveTracker
    {
        public GameObjective objective;
        public float currentProgress = 0f;
        public bool isCompleted = false;
        public bool isActive = true;
        
        public event Action<ObjectiveTracker> OnProgressChanged;
        public event Action<ObjectiveTracker> OnCompleted;
        
        public float ProgressPercentage => objective != null ? Mathf.Clamp01(currentProgress / objective.targetValue) : 0f;
        
        public void UpdateProgress(float newProgress)
        {
            if (isCompleted) 
            {
                Debug.LogWarning($"[ObjectiveTracker] UpdateProgress called on already completed objective: {objective?.objectiveTitle}");
                return;
            }
            
            // LOG EVERY PROGRESS UPDATE with stack trace
            if (objective.objectiveType == ObjectiveType.ActivateObject || 
                objective.objectiveType == ObjectiveType.RepairObject)
            {
                Debug.LogWarning($"=== TOWER OBJECTIVE PROGRESS UPDATE ===");
                Debug.LogWarning($"Objective: {objective.objectiveTitle}");
                Debug.LogWarning($"Old Progress: {currentProgress}");
                Debug.LogWarning($"New Progress: {newProgress}");
                Debug.LogWarning($"Target: {objective.targetValue}");
                Debug.LogWarning($"Will complete: {newProgress >= objective.targetValue}");
                Debug.LogWarning($"STACK TRACE:\n{System.Environment.StackTrace}");
            }
            
            float oldProgress = currentProgress;
            currentProgress = newProgress;
            OnProgressChanged?.Invoke(this);
            
            Debug.Log($"[ObjectiveTracker] {objective?.objectiveTitle}: {oldProgress} -> {currentProgress}/{objective?.targetValue}");
            
            if (currentProgress >= objective.targetValue)
            {
                CompleteObjective();
            }
        }
        
        public void AddProgress(float amount)
        {
            UpdateProgress(currentProgress + amount);
        }
        
        private void CompleteObjective()
        {
            if (isCompleted)
            {
                Debug.LogWarning($"[ObjectiveTracker] CompleteObjective called twice for: {objective?.objectiveTitle}");
                return;
            }
            
            isCompleted = true;
            
            // EXTRA LOGGING FOR TOWER OBJECTIVES
            if (objective.objectiveType == ObjectiveType.ActivateObject || 
                objective.objectiveType == ObjectiveType.RepairObject)
            {
                Debug.LogError($"=== TOWER OBJECTIVE COMPLETED ===");
                Debug.LogError($"Objective: {objective.objectiveTitle}");
                Debug.LogError($"Progress: {currentProgress}/{objective.targetValue}");
                Debug.LogError($"COMPLETION STACK TRACE:\n{System.Environment.StackTrace}");
            }
            
            OnCompleted?.Invoke(this);
            Debug.Log($"✓ Objective Completed: {objective?.objectiveTitle}");
        }
        
        public void ResetProgress()
        {
            currentProgress = 0f;
            isCompleted = false;
            Debug.Log($"[ObjectiveTracker] Reset: {objective?.objectiveTitle}");
        }
    }
}