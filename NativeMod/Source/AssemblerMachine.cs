using System;
using System.Linq;
using Alta.Crafting;
using Alta.Inventory;
using Alta.Networking;
using Alta.Networking.Scripts.Player;
using Alta.Serialization;
using MelonLoader;
using UnityEngine;

namespace ArrowAssemblerNative
{
    public sealed class AssemblerMachine : NetworkEntityBehaviour
    {
        public PickupDock[] Inputs;
        public PickupDock Output;
        public const float CycleSeconds = AssemblyCycle.Duration;
        private MethodSync stateSync;
        private bool cycling;
        private float elapsed;
        private MechanicalRhythm rhythm;
        private int completionSequence;
        public override void Initialize()
        {
            base.Initialize();
            stateSync = new MethodSync(Entity, EntityMessageType.SyncDebugCommandFull, SerializeState);
            rhythm = new MechanicalRhythm(gameObject);
            foreach (var input in Inputs)
            {
                var dock = input;
                dock.DockFilter += (Interactor interactor, Pickup pickup, ref bool allowed) =>
                    allowed = allowed && Recipe.CanDeposit(dock, pickup);
            }
            ConfigureSolidity();
        }
        private void ConfigureSolidity()
        {
            int layer = -1, playerLayer = LayerMask.NameToLayer("SmoothLocomotion");
            var local = PlayerController.Current as SmoothLocomotionPlayerController;
            if (local != null && local.SmoothLocomotion != null)
                playerLayer = local.SmoothLocomotion.CharacterController.gameObject.layer;
            foreach (var name in new[] { "Ground", "Wall", "Terrain" })
            {
                int candidate = LayerMask.NameToLayer(name);
                if (candidate >= 0 && (playerLayer < 0 || !Physics.GetIgnoreLayerCollision(candidate, playerLayer)))
                { layer = candidate; break; }
            }
            if (layer < 0) { MelonLogger.Warning("ASSEMBLER_COLLISION: no solid world layer available."); return; }
            int count = 0;
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                // Keep native hand ranges and the hopper's intake on their own layers.
                if (collider.isTrigger || collider.GetComponentInParent<Pickup>() != null) continue;
                collider.gameObject.layer = layer; collider.enabled = true; count++;
            }
            MelonLogger.Msg("ASSEMBLER_COLLISION solids=" + count + " layer=" + LayerMask.LayerToName(layer));
        }
        public override void OnConnected(Player player)
        {
            base.OnConnected(player);
            if (NetworkSceneManager.IsServer) stateSync.SendToPlayer(player);
        }
        private void SerializeState(IPlayer player, Stream stream)
        {
            var state = new Vector3(cycling ? 1 : 0, elapsed, completionSequence);
            stream.SerializeVector3(ref state);
            // Client messages never change authoritative production state.
            if (stream.IsReading && !NetworkSceneManager.IsServer &&
                (state.x == 0 || state.x == 1) && state.y >= 0 && state.y <= CycleSeconds &&
                state.z >= 0 && state.z <= 1000000 && state.z == Mathf.Floor(state.z))
            { cycling = state.x == 1; elapsed = state.y; completionSequence = (int)state.z; rhythm.ObserveCompletion(completionSequence); }
        }
#if SERVER
        private float nextState;
        private readonly AssemblyCycle cycle = new AssemblyCycle();
        private bool faulted;
#endif

        private void Update()
        {
#if SERVER
            if (!NetworkSceneManager.IsServer || Entity == null || !Entity.IsInitialized || faulted) return;
            bool wasCycling = cycling;
            try
            {
                if (cycle.Tick(CanAssemble(), Time.deltaTime)) Assemble();
                cycling = cycle.Active; elapsed = cycle.Elapsed;
            }
            catch (Exception error)
            {
                faulted = true;
                cycling = false; elapsed = 0;
                MelonLogger.Error("ASSEMBLER_STOPPED: " + error + " Ingredients were not intentionally destroyed. Inspect machine before retrying.");
            }
            if (wasCycling != cycling || Time.time >= nextState)
            { stateSync.SendToChunks(); nextState = Time.time + .25f; }
#else
            if (cycling) elapsed = Mathf.Min(CycleSeconds, elapsed + Time.deltaTime);
#endif
            if (rhythm != null) rhythm.Pose(cycling, elapsed);
        }
#if SERVER
        private bool CanAssemble()
        {
            if (Inputs == null || Inputs.Length != 3 || Inputs.Any(d => !d.HasDockedPickup || d.QuantityStored < 1)) return false;
            if (!Recipe.Match(Inputs[0].DockedPickup.CraftingPart, Inputs[1].DockedPickup.CraftingPart,
                Inputs[2].DockedPickup.CraftingPart, out _, out _)) return false;
            if (!Output.HasDockedPickup) return true;
            if (!Output.DockedPickup.IsPouch) return false;
            var contents = Output.DockedPickup.GetComponentInChildren<PickupDock>();
            return contents != null && contents.QuantityStored < contents.MaxQuantityCurrentlyPossible;
        }
        private void Assemble()
        {
            if (!Recipe.Match(Inputs[0].DockedPickup.CraftingPart, Inputs[1].DockedPickup.CraftingPart,
                Inputs[2].DockedPickup.CraftingPart, out _, out _)) return;
            PickupDock contents = null;
            if (Output.HasDockedPickup)
            {
                if (!Output.DockedPickup.IsPouch) return;
                contents = Output.DockedPickup.GetComponentInChildren<PickupDock>();
                if (contents == null || contents.QuantityStored >= contents.MaxQuantityCurrentlyPossible) return;
            }
            var parts = new Pickup[3];
            JointSlot tipSlot = null, featherSlot = null;
            bool committed = false;
            Pickup newBag = null;
            Pickup convertedFeather = null;
            Pickup assemblyFeather = null;
            try
            {
                for (int i = 0; i < 3; i++)
                    if (!Inputs[i].UndockQuantity(1, out parts[i])) return;
                if (!Recipe.Match(parts[0].CraftingPart, parts[1].CraftingPart, parts[2].CraftingPart, out tipSlot, out featherSlot)) return;
                assemblyFeather = parts[1];
                var conversion = Recipe.Conversion(parts[1]);
                if (conversion != null)
                {
                    convertedFeather = SpawnHelper.Spawn(conversion, SpawnData.Default, Entity.Chunk,
                        Output.SnapPoint.position, Output.SnapPoint.rotation).CommonPickup;
                    assemblyFeather = convertedFeather;
                }
                if (assemblyFeather == null || !tipSlot.TryConnect(parts[2].CraftingPart, true)
                    || !featherSlot.TryConnect(assemblyFeather.CraftingPart, true)) return;
                if (contents == null)
                {
                    newBag = SpawnHelper.Spawn(CommonPrefabs.PouchPrefab, SpawnData.Default, Entity.Chunk,
                        Output.SnapPoint.position, Output.SnapPoint.rotation).CommonPickup;
                    contents = newBag.GetComponentInChildren<PickupDock>();
                }
                if (contents == null || contents.CheckAllowed(null, parts[0]) != PickupDock.AllowedResult.Ok) return;
                if (newBag != null && newBag.DockInto(null, Output, 1, true) != PickupDock.AllowedResult.Ok) return;
                // DockInto is the commit: native storage serializes the entire crafted build.
                if (parts[0].DockInto(null, contents, 1, true) != PickupDock.AllowedResult.Ok) return;
                committed = true;
                completionSequence = completionSequence >= 1000000 ? 0 : completionSequence + 1;
                // A full feather yields exactly one fletching, never its chopping yield.
                if (convertedFeather != null) parts[1].Entity.SceneDestroy();
            }
            finally
            {
                if (!committed)
                {
                    if (featherSlot != null && featherSlot.ConnectedPart == assemblyFeather?.CraftingPart) featherSlot.TryDisconnect(false);
                    if (tipSlot != null && tipSlot.ConnectedPart == parts[2]?.CraftingPart) tipSlot.TryDisconnect(false);
                    if (convertedFeather != null && !convertedFeather.Entity.IsBeingDestroyed) convertedFeather.Entity.SceneDestroy();
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i];
                        if (part == null || part.Entity.IsBeingDestroyed) continue;
                        if (part.DockInto(null, Inputs[i], 1, true) != PickupDock.AllowedResult.Ok)
                        {
                            // Keep a recoverable world item if the native dock rejects rollback.
                            part.transform.position = transform.TransformPoint(new Vector3(i * .3f - .3f, 1.4f, -1f));
                            MelonLogger.Warning("ASSEMBLER_RECOVER_ITEM: returned ingredient above bowl.");
                        }
                    }
                    if (newBag != null && contents != null && contents.QuantityStored == 0) newBag.Entity.SceneDestroy();
                }
            }
        }
#endif
    }
}
