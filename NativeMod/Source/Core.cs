using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Alta.Networking;
using MelonLoader;
using SyncLib.Items;
using UnityEngine;

[assembly: MelonInfo(typeof(ArrowAssemblerNative.Core), "ArrowAssembler", "0.1.13", "caleb")]
[assembly: MelonGame("Alta", "A Township Tale")]
[assembly: MelonAdditionalDependencies("SyncLib")]
namespace ArrowAssemblerNative
{
    public sealed class Core : MelonMod
    {
        internal const string PrefabId = "ArrowAssembler";
        private bool registrationAttempted, preloadStarted;
        private GameObject model;
        private string assetState = "not-started";
#if SERVER
        private const bool ServerBuild = true;
#else
        private const bool ServerBuild = false;
#endif
        public override void OnInitializeMelon()
        {
            NetworkPrefabRegistry.RegisterPrefabs += RegisterAssembler;
            LoggerInstance.Msg("ASSEMBLER_STARTUP version=0.1.13 role=" + (ServerBuild ? "server" : "client"));
        }
        public override void OnLateInitializeMelon()
        {
            if (preloadStarted) return;
            preloadStarted = true;
            MelonCoroutines.Start(PreloadModel());
        }
        private IEnumerator PreloadModel()
        {
            AssetBundleCreateRequest request;
            assetState = "bundle-request";
            LoggerInstance.Msg("ASSEMBLER_ASSET_BEGIN asynchronous-memory-load outside SyncLib registration");
            try
            {
                byte[] bytes;
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArrowAssembler.Model"))
                using (var buffer = new MemoryStream())
                {
                    if (resource == null) throw new InvalidOperationException("Embedded model resource missing.");
                    resource.CopyTo(buffer);
                    bytes = buffer.ToArray();
                }
                // Unity receives owned bytes, not a managed stream it must call back into.
                request = AssetBundle.LoadFromMemoryAsync(bytes);
                if (request == null) throw new InvalidOperationException("Unity returned no bundle request.");
            }
            catch (Exception error) { AssetFailure(error); yield break; }
            while (!request.isDone) yield return null;
            AssetBundle bundle = null;
            AssetBundleRequest assetRequest;
            try
            {
                bundle = request.assetBundle;
                if (bundle == null) throw new InvalidOperationException("Unity could not load the model bundle.");
                LoggerInstance.Msg("ASSEMBLER_BUNDLE_READY");
                assetState = "model-request";
                LoggerInstance.Msg("ASSEMBLER_MODEL_BEGIN");
                assetRequest = bundle.LoadAssetAsync<GameObject>("ArrowAssemblerModel");
                if (assetRequest == null) throw new InvalidOperationException("Unity returned no model request.");
            }
            catch (Exception error) { AssetFailure(error); Release(bundle); yield break; }
            while (!assetRequest.isDone) yield return null;
            try
            {
                model = assetRequest.asset as GameObject;
                if (model == null) throw new InvalidOperationException("Assembler model asset missing.");
                assetState = "ready";
                LoggerInstance.Msg("ASSEMBLER_MODEL_READY; waiting for SyncLib registration");
            }
            catch (Exception error) { AssetFailure(error); }
            finally { Release(bundle); }
        }
        private void AssetFailure(Exception error)
        {
            LoggerInstance.Error("ASSEMBLER_ASSET_FAILED stage=" + assetState + ": " + error);
            assetState = "failed";
        }
        private void Release(AssetBundle bundle)
        {
            try { if (bundle != null) bundle.Unload(false); }
            catch (Exception error) { LoggerInstance.Warning("ASSEMBLER_BUNDLE_RELEASE_FAILED: " + error.Message); }
        }
        private void RegisterAssembler()
        {
            if (registrationAttempted) return;
            registrationAttempted = true;
            string stage = "role-check";
            try
            {
                LoggerInstance.Msg("ASSEMBLER_REGISTRATION_BEGIN version=0.1.13 role=" + (ServerBuild ? "server" : "client"));
                if (NetworkSceneManager.IsServer != ServerBuild)
                    throw new InvalidOperationException("Install the matching ArrowAssembler Client/Server DLL, not both.");
                // Never wait for Unity asset I/O inside SyncLib's shared registration callback.
                if (model == null)
                {
                    LoggerInstance.Error("ASSEMBLER_DISABLED assets=" + assetState + "; model was not ready. SyncLib continues without this machine. Restart after investigating ASSEMBLER_ASSET logs.");
                    return;
                }
                stage = "native-docks";
                LoggerInstance.Msg("ASSEMBLER_STAGE native-docks");
                var template = NativePrefab.Build(model);
                stage = "synclib-register";
                LoggerInstance.Msg("ASSEMBLER_STAGE synclib-register");
                var prefab = this.RegisterCustomPrefab(template, PrefabId);
                if (prefab == null) throw new InvalidOperationException("SyncLib rejected assembler registration.");
                LoggerInstance.Msg("ASSEMBLER_REGISTERED name=" + prefab.name + " hash=" + prefab.Hash
                    + " role=" + (ServerBuild ? "server" : "client") + " embedded=" + prefab.Embedded.Count);
            }
            catch (Exception error)
            {
                LoggerInstance.Error("ASSEMBLER_REGISTRATION_FAILED stage=" + stage + ": " + error);
                LoggerInstance.Warning("ASSEMBLER_DISABLED: registration failed; allowing SyncLib to continue. No automatic retry this session.");
            }
        }
    }
}
