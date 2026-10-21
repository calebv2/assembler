using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Builder.Assembler.Editor
{
    public static class AssemblerRivetLayout
    {
        [MenuItem("Assembler/Center Bolts Around Lip")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play mode before laying out bolts.");
            var lips=UnityEngine.Object.FindObjectsOfType<MeshFilter>().Where(m=>m.name=="Rolled iron bowl lip" && m.gameObject.scene.IsValid()).ToArray();
            if(lips.Length!=1)throw new Exception("Open the assembler scene with exactly one rolled iron bowl lip.");
            var lip=lips[0];var mesh=lip.sharedMesh;var center=mesh.bounds.center;
            var vertices=mesh.vertices;
            float radius=vertices.Max(v=>new Vector2(v.x-center.x,v.z-center.z).magnitude);
            var outer=vertices.Where(v=>Mathf.Abs(new Vector2(v.x-center.x,v.z-center.z).magnitude-radius)<.0001f).ToArray();
            var angles=outer.Select(v=>Mathf.Atan2(v.z-center.z,v.x-center.x)).OrderBy(a=>a).ToList();
            var unique=angles.Where((a,i)=>i==0||Mathf.Abs(a-angles[i-1])>.001f).ToList();
            if(unique.Count>1 && (unique[0]+2*Mathf.PI-unique[unique.Count-1])<.001f)unique.RemoveAt(unique.Count-1);
            if(unique.Count!=20)throw new Exception("Expected 20 lip faces; found "+unique.Count);
            float y=(outer.Min(v=>v.y)+outer.Max(v=>v.y))*.5f;
            var root=lip.transform.root;
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Assembler/Polished/ATT warm metal.mat");
            if(!material)throw new Exception("Original bolt material missing.");
            Directory.CreateDirectory("UserSettings");
            EditorSceneManager.SaveScene(lip.gameObject.scene,"UserSettings/BeforeCenteredRivets.unity",true);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Center bowl bolts");
            foreach(var old in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Bowl rivet"||t.name=="Bowl rivet solid").ToArray())Undo.DestroyObjectImmediate(old.gameObject);
            for(int i=0;i<unique.Count;i++)
            {
                float a=unique[i],b=i+1<unique.Count?unique[i+1]:unique[0]+Mathf.PI*2;
                float mid=(a+b)*.5f;
                float faceRadius=radius*Mathf.Cos((b-a)*.5f);
                // Embed the rear of the spherical head into the flat metal face.
                var local=new Vector3(center.x+Mathf.Cos(mid)*(faceRadius-.008f),y,center.z+Mathf.Sin(mid)*(faceRadius-.008f));
                var bolt=GameObject.CreatePrimitive(PrimitiveType.Sphere);bolt.name="Bowl rivet";
                Undo.RegisterCreatedObjectUndo(bolt,"Add centered bolt");
                bolt.transform.SetParent(lip.transform.parent,false);
                bolt.transform.position=lip.transform.TransformPoint(local);
                bolt.transform.localRotation=lip.transform.localRotation;
                bolt.transform.localScale=Vector3.Scale(lip.transform.localScale,Vector3.one*.045f);
                bolt.GetComponent<Renderer>().sharedMaterial=material;
                bolt.layer=lip.gameObject.layer;
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(lip.gameObject.scene);
            if(!EditorSceneManager.SaveScene(lip.gameObject.scene))throw new Exception("Could not save scene.");
            Selection.activeGameObject=lip.gameObject;
            File.WriteAllText("UserSettings/CenteredRivets.result","PASS: 20 bolts centered on lip facets; original material; scene saved; Undo available.");
            Debug.Log("ASSEMBLER_RIVETS_PASS: 20 centered bolts.");
        }
    }
}
