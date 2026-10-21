using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Builder.Assembler.Editor
{
    public static class AssemblerLipChecks
    {
        const string MeshPath = "Assets/Assembler/Polished/Mesh24.asset";
        // Explicit one-time repair; never runs automatically on project load.
        public static void Repair()
        {
            if(!Application.isBatchMode) throw new Exception("Run this asset repair in a separate batch editor.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            float before = Volume(mesh);
            Render(mesh, "lip-before");
            if(before < 0)
            {
                var triangles = mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                { int swap=triangles[i+1]; triangles[i+1]=triangles[i+2]; triangles[i+2]=swap; }
                mesh.triangles = triangles;
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
            }
            Validate(mesh);
            Render(mesh, "lip-after");
            Debug.Log("ASSEMBLER_LIP_REPAIRED signedVolumeBefore="+before+" after="+Volume(mesh));
        }
        public static void ValidateBundle()
        {
            var bundle=AssetBundle.LoadFromFile(Path.GetFullPath("Builds/AssemblerModel/arrowassembler-model"));
            if(!bundle)throw new Exception("Model bundle missing.");
            try
            {
                var model=bundle.LoadAsset<GameObject>("ArrowAssemblerModel");
                var lip=model.transform.Find("Rolled iron bowl lip");
                Validate(lip.GetComponent<MeshFilter>().sharedMesh);
                var material=lip.GetComponent<Renderer>().sharedMaterial;
                if(material.shader.name!="Assembler/StereoLit"||material.renderQueue>=3000)throw new Exception("Lip must use opaque lit material.");
                Debug.Log("ASSEMBLER_LIP_PASS exported lip faces outward, normals match winding, opaque material.");
            }
            finally{bundle.Unload(true);}
        }
        static float Volume(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;var center=mesh.bounds.center;
            double sum=0;
            for(int i=0;i<triangles.Length;i+=3)
                sum+=Vector3.Dot(vertices[triangles[i]]-center,Vector3.Cross(vertices[triangles[i+1]]-center,vertices[triangles[i+2]]-center))/6.0;
            return (float)sum;
        }
        static void Validate(Mesh mesh)
        {
            if(Volume(mesh)<=0)throw new Exception("Lip winding is inward.");
            var vertices=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=triangles[i];var b=triangles[i+1];var c=triangles[i+2];
                var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).normalized;
                if(Vector3.Dot(normal,normals[a])<.99f)throw new Exception("Lip normals disagree with winding.");
            }
        }
        static void Render(Mesh mesh,string name)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.3f,.3f,.3f);RenderSettings.fog=false;
            var lip=new GameObject("Lip");lip.AddComponent<MeshFilter>().sharedMesh=mesh;
            lip.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Assembler/Polished/ATT cast iron.mat");
            lip.transform.position=-mesh.bounds.center;
            var sun=new GameObject("Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.position=new Vector3(2,1.8f,-2);camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true;camera.orthographicSize=1.3f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.35f,.48f,.6f);
            var rt=new RenderTexture(640,480,24);var pixels=new Texture2D(640,480,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,640,480),0,0);pixels.Apply();
                Directory.CreateDirectory("Builds/LipChecks");File.WriteAllBytes("Builds/LipChecks/"+name+".png",pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
