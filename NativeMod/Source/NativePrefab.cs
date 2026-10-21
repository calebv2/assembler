using System;
using System.Linq;
using System.Reflection;
using Alta.Crafting;
using Alta.Blacksmithing;
using Alta.Inventory;
using Alta.Networking;
using MelonLoader;
using SyncLib.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArrowAssemblerNative
{
    internal static class NativePrefab
    {
        internal static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null) { field.SetValue(target, value); return; }
            }
            throw new MissingFieldException(target.GetType().FullName, name);
        }
        private static T Get<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
        internal static GameObject Build(GameObject asset)
        {
            // Keep templates inactive in the scene; spawned copies retain activeSelf=true.
            var staging = new GameObject("ArrowAssembler prefab templates");
            staging.SetActive(false);
            Object.DontDestroyOnLoad(staging);
            var root = Object.Instantiate(asset, staging.transform, false);
            root.name = "ArrowAssembler";
            root.SetActive(true);
            // Already inside PrepareSpawnSetups/SyncLib registration. GetPrefab calls
            // PrepareSpawnSetups again and its string overload expects lowercase input.
            var prefabs = PrefabManager.AllPrefabs.Where(p => p != null).ToArray();
            var smelter = prefabs.Select(p => p.GetComponent<Smelter>()).FirstOrDefault(p => p != null);
            if (smelter == null) throw new InvalidOperationException("Native Smelter prefab unavailable.");
            var donor = Get<PickupDock[]>(smelter, "oreDocks").First();
            if (donor == null || donor.Settings == null) throw new InvalidOperationException("Smelter ore dock missing.");
            var shaftPrefab = prefabs.FirstOrDefault(p => p.Hash == Recipe.ShaftId);
            if (shaftPrefab == null) throw new InvalidOperationException("Native Arrow Shaft Wooden unavailable.");
            var shaft = shaftPrefab.GetComponent<CraftingPart>();
            if (shaft == null || shaft.Slots.Length < 2) throw new InvalidOperationException("Shaft has no compatible native crafting slots.");
            Recipe.Configure(prefabs, shaft);

            var rootEntity = root.AddComponent<NetworkEntity>();
            var networkPrefab = root.AddComponent<NetworkPrefab>();
            var machine = root.AddComponent<AssemblerMachine>();
            var anchors = new[] { "ATT_ShaftBubble", "ATT_TipBubble", "ATT_FeatherBubble", "ATT_OutputBagBubble" };
            var docks = new PickupDock[4];
            var candidates = prefabs.Select(p => p.GetComponent<Pickup>()).Where(p => p != null && p.Item != null).ToArray();
            for (int i = 0; i < docks.Length; i++)
            {
                var anchor = root.transform.Find(anchors[i]);
                if (anchor == null) throw new InvalidOperationException("Missing model anchor " + anchors[i]);
                var clone = Object.Instantiate(donor.gameObject, anchor, false);
                clone.name = "NativeDock";
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;
                clone.SetActive(true);
                var dock = clone.GetComponent<PickupDock>();
                var settings = Object.Instantiate(donor.Settings);
                settings.name = "ArrowAssembler dock " + i;
                settings.IncludedTags.Clear(); settings.ExcludedTags.Clear(); settings.IncludedItems.Clear();
                foreach (var pickup in candidates)
                    if (Recipe.Accepts(i, pickup, shaft)) settings.IncludedItems.Add(pickup.Item);
                if (i == 3) settings.IncludedItems.Add(CommonPrefabs.PouchItem);
                if (settings.IncludedItems.Count == 0) throw new InvalidOperationException("No native ingredients discovered for dock " + i);
                Set(settings, "maxContainedStacks", i == 3 ? 1 : 10);
                Set(settings, "isMultiStackingAllowed", false);
                Set(settings, "isStackingDisabled", i == 3); // Keep the output as an actual pouch.
                Set(settings, "isStackingEnforced", false);
                Set(settings, "isAllowingCraftedItems", i == 3);
                Set(settings, "isAllowingSubDocking", false);
                Set(settings, "minimumCraftingSize", -1);
                Set(settings, "maximumCraftingSize", -1);
                Set(settings, "isHandDockingAllowed", true);
                Set(settings, "isGrabbable", true);
                Set(settings, "allowContainerInteraction", true);
                Set(dock, "settings", settings);
                // The smelter consumes ore irreversibly; its donor has no hand collider.
                // Give the assembler a native dock range so stored parts are retrievable.
                var grabRange = clone.AddComponent<SphereCollider>();
                grabRange.isTrigger = true;
                grabRange.radius = .19f;
                Set(dock, "pickupRange", grabRange);
                Set(dock, "hasContentsRange", new System.Collections.Generic.List<Collider> { grabRange });
                var visual = Get<DockVisual>(dock, "dockVisual");
                // Forge ore docks deliberately leave pickupRange null. Their native
                // companion interaction component handles grabbing. Preserve that setup.
                // Only an assigned external collider is an unsafe copied reference.
                if (dock.GetComponent<NetworkEntity>() == null)
                    throw new InvalidOperationException("Native ore dock has no NetworkEntity.");
                if (visual == null || !visual.transform.IsChildOf(clone.transform))
                    throw new InvalidOperationException("Native ore dock visual is missing or outside its cloned hierarchy.");
                if (dock.SnapPoint == null || !dock.SnapPoint.IsChildOf(clone.transform))
                    throw new InvalidOperationException("Native ore dock snap point is missing or outside its cloned hierarchy.");
                if (dock.PickupRange != null && !dock.PickupRange.transform.IsChildOf(clone.transform))
                    throw new InvalidOperationException("Native ore dock pickup range refers outside its cloned hierarchy.");
                docks[i] = dock;
                MelonLogger.Msg("ASSEMBLER_DOCK index=" + i + " items=" + string.Join(", ", settings.IncludedItems.Select(item => item.name).ToArray()));
            }
            machine.Inputs = docks.Take(3).ToArray(); machine.Output = docks[3];
            var hopper = root.transform.Find("ATT_InputHopper").gameObject;
            var intakeColliders = hopper.GetComponents<Collider>();
            if (intakeColliders.Length == 0 || intakeColliders.Any(c => !c.isTrigger))
                throw new InvalidOperationException("Model is missing its top-loading trigger.");
            var nativeArea = smelter.GetComponentInChildren<AutomaticDockArea>(true);
            if (nativeArea == null) throw new InvalidOperationException("Smelter automatic docking area unavailable.");
            hopper.layer = nativeArea.gameObject.layer;
            var area = hopper.AddComponent<AutomaticDockArea>();
            area.Docks = machine.Inputs;
            Set(area, "colliders", intakeColliders);
            var previewNames = new[] { "SHAFTS bubble", "TIPS bubble", "FEATHERS bubble", "SHAFTS ingredient icon",
                "TIPS ingredient icon", "FEATHERS ingredient icon", "Output bubble", "Completed arrow stack preview", "ONE SHARED HOPPER" };
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (previewNames.Contains(t.name))
                    t.gameObject.SetActive(false);
            }
            foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
                if (text.text == "0" || text.text == "EMPTY") text.gameObject.SetActive(false);
            var collision = root.transform.Find("Machine collision");
            if (collision == null || collision.GetComponentsInChildren<Collider>().Length == 0)
                throw new InvalidOperationException("Model is missing full-machine collision.");
            int embeddedHash = 1;
            foreach (var entity in root.GetComponentsInChildren<NetworkEntity>(true))
            {
                if (entity == rootEntity) continue;
                if (entity.GetComponent<NetworkPrefab>() != null) throw new InvalidOperationException("Unexpected nested prefab in native dock.");
                Set(entity, "hash", embeddedHash++);
                networkPrefab.Embedded.Add(entity);
            }
            NetworkPrefabRegistry.ExecuteAllGetComponentAtts(root.GetComponentsInChildren<Component>(true));
            return root;
        }
    }
}
