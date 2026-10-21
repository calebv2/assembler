using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Builder.Assembler.Editor
{
    public static class AssemblerLightingChecks
    {
        public static void Measure() { Check(Shader.Find("Assembler/StereoLit"), false); }

        public static void ValidateBundle()
        {
            var bundle = AssetBundle.LoadFromFile(Path.GetFullPath("Builds/AssemblerModel/arrowassembler-model"));
            if (!bundle) throw new Exception("Cannot load exported bundle.");
            try
            {
                var model = bundle.LoadAsset<GameObject>("ArrowAssemblerModel");
                Shader shader = null;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material && material.shader.name == "Assembler/StereoLit") shader = material.shader;
                Check(shader, true);
            }
            finally { bundle.Unload(true); }
        }

        static void Check(Shader shader, bool validate)
        {
            if (!shader || !shader.isSupported) throw new Exception("Assembler shader missing or unsupported.");
            var previousScene = SceneManager.GetActiveScene();
            if (Application.isBatchMode && string.IsNullOrEmpty(previousScene.path))
                previousScene = EditorSceneManager.OpenScene("Assets/Assembler/AssemblerBowlDemo.unity");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var oldPixels = QualitySettings.pixelLightCount;
            var oldMode = RenderSettings.ambientMode;
            var oldAmbient = RenderSettings.ambientLight;
            var oldProbe = RenderSettings.ambientProbe;
            var oldReflection = RenderSettings.reflectionIntensity;
            var oldFog = RenderSettings.fog;
            var oldRT = RenderTexture.active;
            var existingLights = UnityEngine.Object.FindObjectsOfType<Light>().Where(light => light.enabled).ToArray();
            foreach (var light in existingLights) light.enabled = false;
            Material material = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                QualitySettings.pixelLightCount = 4;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                RenderSettings.reflectionIntensity = 0;
                RenderSettings.fog = false;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.layer = 31;
                material = new Material(shader); material.color = new Color(.3f,.3f,.3f,1);
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                var camera = new GameObject("Lighting test camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0,0,-3);
                camera.orthographic = true; camera.orthographicSize = .65f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31; camera.renderingPath = RenderingPath.Forward;
                camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                camera.targetTexture = target;
                pixels = new Texture2D(128,128,TextureFormat.RGB24,false,true);
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.cullingMask = 1 << 31;
                sun.renderMode = LightRenderMode.ForcePixel; sun.enabled = false;
                var torch = new GameObject("Torch").AddComponent<Light>();
                torch.type = LightType.Point; torch.cullingMask = 1 << 31;
                torch.transform.position = new Vector3(0,0,-1.5f); torch.range = 5; torch.intensity = 2;
                torch.renderMode = LightRenderMode.ForcePixel; torch.enabled = false;
                Directory.CreateDirectory("Builds/LightingChecks");
                string prefix = validate ? "bundle" : "before";
                Func<string,float> sample = name =>
                {
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,128,128),0,0); pixels.Apply();
                    float sum = 0;
                    for (int y=48;y<80;y++) for(int x=48;x<80;x++) sum += pixels.GetPixel(x,y).grayscale;
                    File.WriteAllBytes("Builds/LightingChecks/"+prefix+"-"+name+".png",pixels.EncodeToPNG());
                    return sum/1024;
                };
                float dark = sample("dark");
                sun.enabled = true; sun.intensity = .8f;
                float sunlight = sample("sun");
                sun.transform.rotation = Quaternion.Euler(0,180,0);
                float away = sample("sun-away");
                sun.transform.rotation = Quaternion.identity; sun.intensity = .15f;
                float baseLight = sample("dim-sun");
                torch.enabled = true;
                float point = sample("torch-pixel");
                torch.transform.position = new Vector3(0,0,-20);
                float distant = sample("torch-distant");
                torch.transform.position = new Vector3(0,0,-1.5f);
                torch.renderMode = LightRenderMode.ForceVertex;
                float vertex = sample("torch-vertex");
                string result = string.Format("dark={0:F4} sun={1:F4} sunAway={2:F4} dimSun={3:F4} point={4:F4} distant={5:F4} vertex={6:F4}",dark,sunlight,away,baseLight,point,distant,vertex);
                File.WriteAllText("Builds/LightingChecks/"+prefix+".txt",result);
                Debug.Log("ASSEMBLER_LIGHTING_MEASURE "+result);
                if(validate && (dark>.01f || sunlight<dark+.1f || sunlight<away+.1f || point<baseLight+.05f || point<distant+.05f || vertex<baseLight+.05f))
                    throw new Exception("Lighting regression: "+result);
                if(validate) Debug.Log("ASSEMBLER_LIGHTING_PASS exported shader responds to sunlight, pixel/vertex torches, distance; no emission in darkness.");
            }
            finally
            {
                RenderTexture.active = oldRT;
                if(material) UnityEngine.Object.DestroyImmediate(material);
                if(target) UnityEngine.Object.DestroyImmediate(target);
                if(pixels) UnityEngine.Object.DestroyImmediate(pixels);
                QualitySettings.pixelLightCount = oldPixels;
                foreach (var light in existingLights) if(light) light.enabled = true;
                RenderSettings.ambientMode = oldMode; RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientProbe = oldProbe; RenderSettings.reflectionIntensity = oldReflection; RenderSettings.fog = oldFog;
                EditorSceneManager.CloseScene(scene,true);
                SceneManager.SetActiveScene(previousScene);
            }
        }
    }
}
