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
        
        public float ProgressPercentage => Mathf.Clamp01(currentProgress / objective.targetValue);
        
        public void UpdateProgress(float newProgress)
        {
            if (isCompleted) return;
            
            currentProgress = newProgress;
            OnProgressChanged?.Invoke(this);
            
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
            isCompleted = true;
            OnCompleted?.Invoke(this);
            Debug.Log($"Objective Completed: {objective.objectiveTitle}");
        }
    }
}