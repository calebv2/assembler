using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Builder.Assembler.Editor
{
    public static class AssemblerExport
    {
        const string PrefabPath="Assets/Assembler/ArrowAssemblerModel.prefab";
        [MenuItem("Assembler/Export Model Bundle (not game logic)")]
        public static void Export()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Assembler/AssemblerBowlDemo.unity");
            bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene("Assets/Assembler/AssemblerBowlDemo.unity",OpenSceneMode.Additive);
            GameObject copy=null;
            try
            {
                var original=scene.GetRootGameObjects()[0];
                copy=UnityEngine.Object.Instantiate(original);
                copy.name="ArrowAssemblerModel";
                // Only built-in components enter the bundle. ATT behaviors are attached by the mod at registration time.
                foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach(var childName in new[]{"Workshop","Bag supply bench","SHAFTS bag","TIPS bag","FEATHERS bag","Assembler camera","Workshop key light"})
                {
                    var child=copy.transform.Find(childName);
                    if(child) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                foreach(var text in copy.GetComponentsInChildren<TextMesh>(true))
                    UnityEngine.Object.DestroyImmediate(text.gameObject);
                foreach(var light in copy.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light);
                foreach(var camera in copy.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera);
                foreach(var t in copy.GetComponentsInChildren<Transform>(true))
                    if(t && t.name.StartsWith("Workshop")) UnityEngine.Object.DestroyImmediate(t.gameObject);
                var oldCollision=copy.transform.Find("Machine collision");
                if(oldCollision) UnityEngine.Object.DestroyImmediate(oldCollision.gameObject);
                var collision=new GameObject("Machine collision").transform;
                collision.SetParent(copy.transform,false);
                foreach(var mesh in copy.GetComponentsInChildren<MeshFilter>())
                {
                    bool skip=false;
                    for(var t=mesh.transform;t!=copy.transform;t=t.parent)
                        if(t.name.IndexOf("bubble",StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("icon",StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("preview",StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("ingredient",StringComparison.OrdinalIgnoreCase)>=0) skip=true;
                    var renderer=mesh.GetComponent<Renderer>();
                    if(skip || !renderer || !renderer.enabled || !mesh.sharedMesh) continue;
                    foreach(var c in mesh.GetComponents<Collider>()) if(!c.isTrigger) UnityEngine.Object.DestroyImmediate(c);
                    var proxy=new GameObject(mesh.name+" solid").transform;
                    proxy.SetParent(mesh.transform,false);proxy.SetParent(collision,true);
                    proxy.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
                }
                var lip=copy.transform.Find("Rolled iron bowl lip").GetComponent<Renderer>().bounds;
                var intake=Anchor(copy,"ATT_InputHopper",copy.transform.InverseTransformPoint(new Vector3(lip.center.x,lip.max.y-.12f,lip.center.z)));
                var catchArea=intake.AddComponent<BoxCollider>();
                catchArea.isTrigger=true;
                catchArea.size=new Vector3(lip.size.x*.78f,.24f,lip.size.z*.78f);
                string[] anchors={"ATT_ShaftBubble","ATT_TipBubble","ATT_FeatherBubble","ATT_OutputBagBubble"};
                string[] visuals={"SHAFTS bubble","TIPS bubble","FEATHERS bubble","Output bubble"};
                for(int i=0;i<anchors.Length;i++)
                    Anchor(copy,anchors[i],copy.transform.InverseTransformPoint(copy.transform.Find(visuals[i]).position));
                PrefabUtility.SaveAsPrefabAsset(copy,PrefabPath);
                AssetDatabase.SaveAssets();
                string output=Path.GetFullPath("Builds/AssemblerModel"); Directory.CreateDirectory(output);
                // Unity strips stereo variants from bundles built with VR disabled,
                // even when a shader explicitly declares the stereo keywords.
                PlayerSettings.virtualRealitySupported=true;
                PlayerSettings.stereoRenderingPath=StereoRenderingPath.Instancing;
                var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="arrowassembler-model",assetNames=new[]{PrefabPath}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
                if(manifest==null) throw new Exception("Assembler bundle build failed.");
                File.WriteAllText(Path.Combine(output,"STATUS.txt"),"Model bundle for ArrowAssembler native mod. Install the matching packaged Client/Server DLLs, which embed this bundle.");
                Debug.Log("Assembler model bundle exported to "+output);
            }
            finally
            {
                if(copy) UnityEngine.Object.DestroyImmediate(copy);
                if(opened) EditorSceneManager.CloseScene(scene,true);
            }
        }
        static GameObject Anchor(GameObject root,string name,Vector3 position)
        {
            var child=new GameObject(name); child.transform.SetParent(root.transform,false); child.transform.localPosition=position;
            return child;
        }
    }
}
