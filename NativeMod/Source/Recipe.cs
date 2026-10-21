using System;
using System.Collections.Generic;
using System.Linq;
using Alta.Crafting;
using Alta.Inventory;
using Alta.Networking;

namespace ArrowAssemblerNative
{
    internal static class Recipe
    {
        private static readonly Dictionary<Item, NetworkPrefab> wholeFeathers = new Dictionary<Item, NetworkPrefab>();
        private static readonly HashSet<Item> featherItems = new HashSet<Item>();
        private static readonly HashSet<Item> tipItems = new HashSet<Item>();
        internal static readonly uint[] FeatherIds = { 50608, 24072 };
        internal static readonly uint[] TipIds = { 7092, 61488, 12492, 6934, 3558, 50412, 44360 };
        internal const uint ShaftId = 29772;
        private static Item shaftItem;
        internal static void Configure(IEnumerable<NetworkPrefab> prefabs, CraftingPart shaft)
        {
            shaftItem = shaft.Pickup.Item;
            wholeFeathers.Clear();
            featherItems.Clear(); tipItems.Clear();
            var all = prefabs.ToArray();
            foreach (var id in FeatherIds.Concat(TipIds))
            {
                var prefab = all.FirstOrDefault(p => p.Hash == id);
                var pickup = prefab == null ? null : prefab.GetComponent<Pickup>();
                var part = pickup == null ? null : pickup.CraftingPart;
                if (pickup == null || pickup.Item == null || part == null || part.Insert == null
                    || !shaft.Slots.Any(s => s.Supports(part.Insert.Type)))
                    throw new InvalidOperationException("Required assembler ingredient unavailable or incompatible: " + id);
                (FeatherIds.Contains(id) ? featherItems : tipItems).Add(pickup.Item);
            }
            foreach (var prefab in all)
            {
                var pickup = prefab.GetComponent<Pickup>();
                if (pickup == null || pickup.Item == null || !pickup.Item.name.StartsWith("Spriggull Feather ", StringComparison.OrdinalIgnoreCase)) continue;
                uint cutId = pickup.Item.name.Equals("Spriggull Feather Red", StringComparison.OrdinalIgnoreCase) ? 24072u
                    : pickup.Item.name.Equals("Spriggull Feather Blue", StringComparison.OrdinalIgnoreCase) ? 50608u : 0u;
                var cut = all.FirstOrDefault(p => cutId != 0 && p.Hash == cutId);
                var part = cut == null ? null : cut.GetComponent<CraftingPart>();
                if (part != null && part.Insert != null && shaft.Slots.Any(s => s.Supports(part.Insert.Type)))
                    wholeFeathers[pickup.Item] = cut;
            }
        }
        internal static NetworkPrefab Conversion(Pickup pickup)
        {
            return pickup != null && pickup.Item != null && wholeFeathers.TryGetValue(pickup.Item, out var prefab) ? prefab : null;
        }
        internal static bool Accepts(int index, Pickup pickup, CraftingPart shaft)
        {
            if (pickup == null || pickup.Item == null || pickup.IsCrafted) return false;
            if (index == 0) return pickup.Item == shaftItem;
            if (index != 1 && index != 2) return false;
            if (index == 1 && Conversion(pickup) != null) return true;
            var part = pickup.CraftingPart;
            if (part == null || part.Insert == null || part.ConnectedTo != null) return false;
            return (index == 1 ? featherItems : tipItems).Contains(pickup.Item)
                && shaft.Slots.Any(s => s.Supports(part.Insert.Type));
        }
        internal static bool Match(CraftingPart shaft, CraftingPart feather, CraftingPart tip, out JointSlot tipSlot, out JointSlot featherSlot)
        {
            tipSlot = null; featherSlot = null;
            if (shaft == null || feather == null || tip == null || !Accepts(0, shaft.Pickup, shaft)
                || !Accepts(1, feather.Pickup, shaft) || !Accepts(2, tip.Pickup, shaft)) return false;
            var conversion = Conversion(feather.Pickup);
            if (conversion != null) feather = conversion.GetComponent<CraftingPart>();
            if (feather.Insert == null || tip.Insert == null) return false;
            foreach (var a in shaft.Slots)
                foreach (var b in shaft.Slots)
                    if (a != b && !a.IsUsed && !b.IsUsed && a.IsAvailable() && b.IsAvailable()
                        && a.Supports(tip.Insert.Type) && b.Supports(feather.Insert.Type))
                    { tipSlot = a; featherSlot = b; return true; }
            return false;
        }
        internal static bool CanDeposit(PickupDock dock, Pickup incoming)
        {
            if (incoming == null) return false;
            if (incoming.IsPouch)
            {
                var bag = incoming.GetComponentInChildren<PickupDock>();
                if (bag == null || bag.QuantityStored < 1) return false;
                incoming = bag.DockedPickup;
            }
            return incoming != null && !incoming.IsCrafted && dock.Settings.IncludedItems.Contains(incoming.Item)
                && (!dock.HasDockedPickup || dock.dockedType == incoming.Item);
        }
    }
}
