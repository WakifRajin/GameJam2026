using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// A quantity of one item type living in a single inventory slot.
    /// Stacks are mutable value holders owned by the grid - never share one between slots.
    /// </summary>
    [Serializable]
    public class ItemStack
    {
        public CollectibleItem item;
        public int quantity;

        public ItemStack(CollectibleItem item, int quantity = 1)
        {
            this.item = item;
            this.quantity = Mathf.Max(0, quantity);
        }

        public bool IsEmpty => item == null || quantity <= 0;

        /// <summary>Max units of this item per slot. Treats an unset stackSize as 1.</summary>
        public int MaxStack => item == null ? 0 : Mathf.Max(1, item.stackSize);

        public int FreeSpace => IsEmpty ? 0 : Mathf.Max(0, MaxStack - quantity);

        public float TotalWeight => IsEmpty ? 0f : item.weight * quantity;

        public bool CanStackWith(CollectibleItem other)
        {
            return !IsEmpty && other != null && other == item && FreeSpace > 0;
        }

        /// <summary>Adds up to <paramref name="amount"/> units. Returns how many actually fit.</summary>
        public int Add(int amount)
        {
            int accepted = Mathf.Clamp(amount, 0, FreeSpace);
            quantity += accepted;
            return accepted;
        }

        /// <summary>Removes up to <paramref name="amount"/> units. Returns how many were removed.</summary>
        public int Remove(int amount)
        {
            int removed = Mathf.Clamp(amount, 0, quantity);
            quantity -= removed;
            if (quantity <= 0)
            {
                quantity = 0;
                item = null;
            }
            return removed;
        }

        public ItemStack Clone() => new ItemStack(item, quantity);
    }

    /// <summary>
    /// Canonical resource keys. Item assets, upgrade recipes and level objectives all spell
    /// these differently ("Material" vs "Materials", "PowerCell" vs "Power"), so everything
    /// funnels through Normalize before it touches the ledger.
    /// </summary>
    public static class ResourceIds
    {
        public const string Materials = "Materials";
        public const string Tech = "Tech";
        public const string Power = "Power";
        public const string Cooling = "Cooling";

        public static readonly string[] All = { Materials, Tech, Power, Cooling };

        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "material", Materials },
            { "materials", Materials },
            { "scrap", Materials },
            { "scrapmetal", Materials },
            { "scrap metal", Materials },
            { "metal", Materials },

            { "tech", Tech },
            { "techcomponent", Tech },
            { "tech component", Tech },
            { "commmodule", Tech },
            { "electronics", Tech },

            { "power", Power },
            { "powercell", Power },
            { "power cell", Power },
            { "fuel", Power },
            { "energy", Power },

            { "cooling", Cooling },
            { "coolingunit", Cooling },
            { "cooling unit", Cooling },
            { "coolant", Cooling },
        };

        /// <summary>Maps any spelling onto a canonical key. Unknown keys pass through trimmed.</summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string trimmed = raw.Trim();
            return Aliases.TryGetValue(trimmed, out string canonical) ? canonical : trimmed;
        }

        public static string FromItemType(ItemType type)
        {
            switch (type)
            {
                case ItemType.Material: return Materials;
                case ItemType.PowerCell:
                case ItemType.Fuel: return Power;
                case ItemType.TechComponent:
                case ItemType.CommModule: return Tech;
                case ItemType.CoolingUnit: return Cooling;
                default: return type.ToString();
            }
        }

        /// <summary>The canonical resource an item contributes to, honouring its override field.</summary>
        public static string Of(CollectibleItem item)
        {
            if (item == null) return string.Empty;
            return Normalize(item.GetResourceType());
        }

        /// <summary>How much of its resource a single unit of this item is worth.</summary>
        public static float ValueOf(CollectibleItem item)
        {
            if (item == null) return 0f;
            switch (Of(item))
            {
                case Materials: return item.materialValue;
                case Tech: return item.techValue;
                case Power: return item.powerValue;
                case Cooling: return item.heatReduction;
                default: return 0f;
            }
        }
    }

    #region Save data

    [Serializable]
    public class SlotSaveData
    {
        public int x;
        public int y;
        public string itemName;
        public int quantity;
    }

    [Serializable]
    public class InventorySaveData
    {
        public int gridWidth;
        public int gridHeight;
        public List<SlotSaveData> slots = new List<SlotSaveData>();
        public List<string> bankedResourceKeys = new List<string>();
        public List<float> bankedResourceValues = new List<float>();
    }

    [Serializable]
    public class UpgradeLevelSaveData
    {
        public string upgradeId;
        public int level;
    }

    [Serializable]
    public class UpgradeSaveData
    {
        public List<UpgradeLevelSaveData> levels = new List<UpgradeLevelSaveData>();
    }

    #endregion
}
