using System.Collections.Generic;
using UnityEngine;
using System;

namespace GameJam2026
{
    /// <summary>
    /// Manages resource exchanges (Equivalent Exchange mechanic)
    /// </summary>
    public class ExchangeManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoverAttributeManager roverAttributes;
        [SerializeField] private InventoryManager inventoryManager;
        
        [Header("Available Exchanges")]
        [SerializeField] private List<ExchangeRecipe> availableExchanges = new List<ExchangeRecipe>();
        
        [Header("Settings")]
        [SerializeField] private bool allowExchangeWhileMoving = false;
        [SerializeField] private float exchangeCooldown = 1f;
        
        private float lastExchangeTime = 0f;
        private Dictionary<ExchangeRecipe, float> activeTemporaryEffects = new Dictionary<ExchangeRecipe, float>();
        
        // Events
        public event Action<ExchangeRecipe> OnExchangeCompleted;
        public event Action<ExchangeRecipe, string> OnExchangeFailed; // recipe, reason

        private void Start()
        {
            if (roverAttributes == null)
            {
                roverAttributes = GetComponent<RoverAttributeManager>();
            }
            
            if (inventoryManager == null)
            {
                inventoryManager = GetComponent<InventoryManager>();
            }
        }

        private void Update()
        {
            UpdateTemporaryEffects();
        }

        #region Exchange System

        public bool CanPerformExchange(ExchangeRecipe recipe)
        {
            if (recipe == null) return false;
            
            // Check cooldown
            if (Time.time - lastExchangeTime < exchangeCooldown)
            {
                return false;
            }
            
            // Check if moving (if not allowed)
            if (!allowExchangeWhileMoving && roverAttributes != null && roverAttributes.IsMoving)
            {
                return false;
            }
            
            // Check day/night requirements
            if (recipe.requiresDaytime && !roverAttributes.IsDaytime)
            {
                return false;
            }
            if (recipe.requiresNighttime && roverAttributes.IsDaytime)
            {
                return false;
            }
            
            // Check minimum power
            if (roverAttributes != null && roverAttributes.CurrentPower < recipe.minimumPowerRequired)
            {
                return false;
            }
            
            // Check if we have all the required resources
            foreach (var cost in recipe.costs)
            {
                if (!HasResource(cost.resourceType, cost.amount))
                {
                    return false;
                }
            }
            
            return true;
        }

        public bool PerformExchange(ExchangeRecipe recipe)
        {
            if (!CanPerformExchange(recipe))
            {
                string reason = GetExchangeFailureReason(recipe);
                OnExchangeFailed?.Invoke(recipe, reason);
                Debug.Log($"Exchange failed: {reason}");
                return false;
            }
            
            // Consume resources (the sacrifice)
            foreach (var cost in recipe.costs)
            {
                ConsumeResource(cost.resourceType, cost.amount);
            }
            
            // Grant rewards (the gain)
            foreach (var reward in recipe.rewards)
            {
                AddResource(reward.resourceType, reward.amount);
            }
            
            // Apply special effects
            if (recipe.hasSpecialEffect)
            {
                ApplySpecialEffect(recipe);
            }
            
            lastExchangeTime = Time.time;
            OnExchangeCompleted?.Invoke(recipe);
            
            Debug.Log($"Exchange completed: {recipe.exchangeName}");
            return true;
        }

        private string GetExchangeFailureReason(ExchangeRecipe recipe)
        {
            if (Time.time - lastExchangeTime < exchangeCooldown)
                return "On cooldown";
            
            if (!allowExchangeWhileMoving && roverAttributes.IsMoving)
                return "Cannot exchange while moving";
            
            if (recipe.requiresDaytime && !roverAttributes.IsDaytime)
                return "Requires daytime";
            
            if (recipe.requiresNighttime && roverAttributes.IsDaytime)
                return "Requires nighttime";
            
            if (roverAttributes.CurrentPower < recipe.minimumPowerRequired)
                return "Insufficient power";
            
            foreach (var cost in recipe.costs)
            {
                if (!HasResource(cost.resourceType, cost.amount))
                    return $"Insufficient {cost.resourceType}";
            }
            
            return "Unknown reason";
        }

        #endregion

        #region Resource Management

        private bool HasResource(ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Power:
                    return roverAttributes != null && roverAttributes.CurrentPower >= amount;
                    
                case ResourceType.Speed:
                    return roverAttributes != null && roverAttributes.SpeedMultiplier >= amount;
                    
                case ResourceType.Heat:
                    return roverAttributes != null && roverAttributes.CurrentHeat >= amount;
                    
                case ResourceType.CommunicationRange:
                    return roverAttributes != null && roverAttributes.CurrentCommunicationRange >= amount;
                    
                case ResourceType.Materials:
                    return inventoryManager != null && inventoryManager.StoredMaterials >= amount;
                    
                case ResourceType.Tech:
                    return inventoryManager != null && inventoryManager.StoredTech >= amount;
                    
                default:
                    return false;
            }
        }

        private void ConsumeResource(ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Power:
                    roverAttributes?.ModifyPower(-amount);
                    break;
                    
                case ResourceType.Speed:
                    roverAttributes?.ModifySpeedMultiplier(-amount);
                    break;
                    
                case ResourceType.Heat:
                    roverAttributes?.ModifyHeat(-amount);
                    break;
                    
                case ResourceType.CommunicationRange:
                    roverAttributes?.ModifyCommunicationRange(-amount);
                    break;
                    
                case ResourceType.Materials:
                    inventoryManager?.ConsumeResource("Materials", amount);
                    break;
                    
                case ResourceType.Tech:
                    inventoryManager?.ConsumeResource("Tech", amount);
                    break;
            }
        }

        private void AddResource(ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Power:
                    roverAttributes?.ModifyPower(amount);
                    break;
                    
                case ResourceType.MaxPower:
                    // TODO: Implement max power upgrade
                    Debug.Log($"Max Power increased by {amount}");
                    break;
                    
                case ResourceType.Speed:
                    roverAttributes?.ModifySpeedMultiplier(amount);
                    break;
                    
                case ResourceType.Heat:
                    roverAttributes?.ModifyHeat(amount);
                    break;
                    
                case ResourceType.CommunicationRange:
                    roverAttributes?.ModifyCommunicationRange(amount);
                    break;
                    
                case ResourceType.Materials:
                    inventoryManager?.AddResource("Materials", amount);
                    break;
                    
                case ResourceType.Tech:
                    inventoryManager?.AddResource("Tech", amount);
                    break;
                    
                case ResourceType.CargoCapacity:
                    // TODO: Implement cargo capacity upgrade
                    Debug.Log($"Cargo Capacity increased by {amount}");
                    break;
            }
        }

        #endregion

        #region Special Effects

        private void ApplySpecialEffect(ExchangeRecipe recipe)
        {
            switch (recipe.effectType)
            {
                case ExchangeEffectType.TemporarySpeedBoost:
                    if (recipe.effectDuration > 0)
                    {
                        activeTemporaryEffects[recipe] = Time.time + recipe.effectDuration;
                        roverAttributes?.ModifySpeedMultiplier(recipe.effectValue);
                    }
                    break;
                    
                case ExchangeEffectType.TemporaryCooling:
                    if (recipe.effectDuration > 0)
                    {
                        activeTemporaryEffects[recipe] = Time.time + recipe.effectDuration;
                    }
                    roverAttributes?.ModifyHeat(-recipe.effectValue);
                    break;
                    
                case ExchangeEffectType.InstantHeal:
                    roverAttributes?.ModifyPower(recipe.effectValue);
                    break;
                    
                case ExchangeEffectType.InstantCooling:
                    roverAttributes?.ModifyHeat(-recipe.effectValue);
                    break;
                    
                case ExchangeEffectType.PermanentUpgrade:
                    // Handle permanent upgrades
                    Debug.Log($"Permanent upgrade applied: {recipe.exchangeName}");
                    break;
            }
        }

        private void UpdateTemporaryEffects()
        {
            List<ExchangeRecipe> expiredEffects = new List<ExchangeRecipe>();
            
            foreach (var effect in activeTemporaryEffects)
            {
                if (Time.time >= effect.Value)
                {
                    expiredEffects.Add(effect.Key);
                    RemoveTemporaryEffect(effect.Key);
                }
            }
            
            foreach (var expired in expiredEffects)
            {
                activeTemporaryEffects.Remove(expired);
            }
        }

        private void RemoveTemporaryEffect(ExchangeRecipe recipe)
        {
            switch (recipe.effectType)
            {
                case ExchangeEffectType.TemporarySpeedBoost:
                    roverAttributes?.ModifySpeedMultiplier(-recipe.effectValue);
                    Debug.Log($"Speed boost expired: {recipe.exchangeName}");
                    break;
            }
        }

        #endregion

        #region Public Methods

        public List<ExchangeRecipe> GetAvailableExchanges()
        {
            return availableExchanges;
        }

        public void AddExchangeRecipe(ExchangeRecipe recipe)
        {
            if (!availableExchanges.Contains(recipe))
            {
                availableExchanges.Add(recipe);
            }
        }

        public void RemoveExchangeRecipe(ExchangeRecipe recipe)
        {
            availableExchanges.Remove(recipe);
        }

        #endregion
    }
}