using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Builder.Assembler
{
    public sealed class ArrowAssembler : MonoBehaviour
    {
        [Header("Recipe: one shaft + one tip + one feather")]
        [Min(1)] public int partsPerBag = 10;
        [Min(.2f)] public float secondsPerArrow = 3f;
        Vector3 pressRest;
        [SerializeField] Transform press;
        [SerializeField] Transform wheel;
        [SerializeField] Transform[] bubbles = new Transform[3];
        [SerializeField] TextMesh[] bubbleLabels = new TextMesh[3];

        [SerializeField] Camera viewCamera;
        [SerializeField] Material wood, metal, brass, cream;
        public readonly AssemblyStock Stock = new AssemblyStock();
        [SerializeField] Transform outputBag;
        [SerializeField] TextMesh outputLabel;
        int outputCount;
        static readonly string[] Names = { "SHAFTS", "TIPS", "FEATHERS" };
        static readonly Color[] Colors = { new Color(.95f,.63f,.22f), new Color(.3f,.75f,.87f), new Color(.7f,.83f,.48f) };
        int selected = -1, completed, collected;
        bool loading, assembling;
        float cycle;
        string message = "Select an ingredient, then click the bowl to load it.";
        GUIStyle heading, body, small, button;

        public void Build()
        {
            wood=Mat("Weathered walnut",new Color(.25f,.15f,.085f));
            metal=Mat("Forged iron",new Color(.12f,.17f,.18f),.35f);
            brass=Mat("Aged copper",new Color(.46f,.28f,.13f),.45f);
            cream=Mat("Ivory",new Color(.86f,.83f,.68f));
            var floor=Mat("Workshop stone",new Color(.2f,.25f,.26f));
            Part("Workshop",PrimitiveType.Cube,new Vector3(0,-.16f,0),new Vector3(12,.25f,10),floor);
            Part("Machine plinth",PrimitiveType.Cube,new Vector3(0,.14f,0),new Vector3(2.55f,.25f,1.85f),wood);
            for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            {
                Part("Oak upright",PrimitiveType.Cube,new Vector3(x*1.02f,.81f,z*.61f),new Vector3(.23f,1.42f,.23f),wood);
                Part("Forged foot",PrimitiveType.Cube,new Vector3(x*1.02f,.3f,z*.61f),new Vector3(.29f,.23f,.29f),metal);
            }
            Part("Assembly bed",PrimitiveType.Cube,new Vector3(0,1.04f,0),new Vector3(2.4f,.2f,1.6f),wood);
            Part("Front crossbeam",PrimitiveType.Cube,new Vector3(0,.92f,-.76f),new Vector3(2.3f,.3f,.13f),metal);
            for(int i=0;i<7;i++)
                Part("Cabinet plank",PrimitiveType.Cube,new Vector3((i-3)*.31f,.62f,-.62f),new Vector3(.29f,.48f,.1f),wood);
            for(int x=-1;x<=1;x+=2)
                for(int y=0;y<2;y++)
                    Part("Hand forged rivet",PrimitiveType.Sphere,new Vector3(x*.93f,.83f+y*.17f,-.84f),Vector3.one*.07f,brass);

            Part("Bowl feed neck",PrimitiveType.Cylinder,new Vector3(0,1.85f,.12f),new Vector3(.55f,.27f,.55f),metal);
            // Revolved cross-section forms a thick, genuinely hollow bowl with an open throat.
            Lathe("Hollow receiving bowl",new[]{new Vector2(.27f,1.92f),new Vector2(.51f,2.01f),new Vector2(.8f,2.22f),new Vector2(1.02f,2.55f),new Vector2(1.03f,2.63f),new Vector2(.93f,2.63f),new Vector2(.91f,2.54f),new Vector2(.69f,2.27f),new Vector2(.42f,2.09f),new Vector2(.22f,2.05f)},brass);
            Lathe("Rolled iron bowl lip",new[]{new Vector2(.94f,2.59f),new Vector2(.93f,2.66f),new Vector2(.99f,2.7f),new Vector2(1.06f,2.66f),new Vector2(1.06f,2.6f),new Vector2(.94f,2.59f)},metal);
            var hopper=new GameObject("ONE SHARED HOPPER"); hopper.transform.SetParent(transform,false); hopper.transform.localPosition=new Vector3(0,2.5f,.12f);
            hopper.AddComponent<SphereCollider>().radius=1.05f;
            hopper.AddComponent<AssemblerTarget>().type=AssemblerTarget.TargetType.Hopper;
            var bubbleMaterial=new Material(Shader.Find("Assembler/StorageBubble")){name="Soft storage bubble",color=new Color(.69f,.87f,.91f,.6f)};
            for(int i=0;i<3;i++)
            {
                float x=(i-1)*.72f;
                bubbles[i]=Part(Names[i]+" bubble",PrimitiveType.Sphere,new Vector3(x,1.51f,-.92f),Vector3.one*.57f,bubbleMaterial);
                IngredientIcon(i,new Vector3(x,1.51f,-.92f));
                bubbleLabels[i]=Label("0",new Vector3(x,1.24f,-1.12f),.065f,cream.color);

            }
            Part("Press bridge",PrimitiveType.Cube,new Vector3(0,1.83f,.53f),new Vector3(2.2f,.15f,.23f),metal);
            press=Part("Assembly press",PrimitiveType.Cube,new Vector3(0,1.52f,-.12f),new Vector3(.6f,.19f,.55f),brass);
            wheel=new GameObject("Spoked flywheel").transform; wheel.SetParent(transform,false); wheel.localPosition=new Vector3(-1.27f,1.13f,.07f);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                var spoke=Part("Wheel spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(.1f,.8f,.06f),wood,wheel); spoke.localRotation=Quaternion.Euler(i*45,0,0);
                var rim=Part("Iron wheel rim",PrimitiveType.Cube,new Vector3(0,Mathf.Sin(a)*.43f,Mathf.Cos(a)*.43f),new Vector3(.14f,.35f,.1f),metal,wheel); rim.localRotation=Quaternion.Euler(-i*45,0,0);
            }
            Part("Side discharge chute",PrimitiveType.Cube,new Vector3(1.51f,1.06f,-.08f),new Vector3(.86f,.08f,.45f),brass).localRotation=Quaternion.Euler(0,0,-12);
            for(int z=-1;z<=1;z+=2)
                Part("Chute guide",PrimitiveType.Cube,new Vector3(1.51f,1.14f,-.08f+z*.23f),new Vector3(.86f,.16f,.055f),metal).localRotation=Quaternion.Euler(0,0,-12);
            Part("Side collector bracket",PrimitiveType.Cube,new Vector3(1.75f,.76f,-.08f),new Vector3(1.2f,.13f,.73f),wood);
            Part("Collector support",PrimitiveType.Cube,new Vector3(1.33f,.48f,-.08f),new Vector3(.14f,.53f,.14f),metal).localRotation=Quaternion.Euler(0,0,-25);
            var output=Part("Output bubble",PrimitiveType.Sphere,new Vector3(2.05f,1.23f,-.08f),Vector3.one*.85f,bubbleMaterial);
            output.gameObject.AddComponent<AssemblerTarget>().type=AssemblerTarget.TargetType.Output;
            outputBag=new GameObject("Completed arrow stack preview").transform; outputBag.SetParent(transform,false); outputBag.localPosition=new Vector3(2.05f,1.23f,-.08f);
            for(int i=0;i<3;i++)
            {
                var shaft=Part("Finished arrow icon",PrimitiveType.Cylinder,new Vector3((i-1)*.09f,0,0),new Vector3(.02f,.25f,.02f),wood,outputBag); shaft.localRotation=Quaternion.Euler(0,0,-15);
                Part("Fletching icon",PrimitiveType.Cube,new Vector3((i-1)*.09f+.045f,.16f,0),new Vector3(.07f,.11f,.018f),cream,outputBag).localRotation=Quaternion.Euler(0,0,-15);
            }
            outputLabel=Label("EMPTY",new Vector3(2.05f,.78f,-.49f),.058f,cream.color);

            outputBag.gameObject.SetActive(false);
            var cam=new GameObject("Assembler camera"); cam.transform.SetParent(transform,false); cam.transform.localPosition=new Vector3(5.6f,4.1f,-8.5f); cam.transform.LookAt(new Vector3(.35f,1.25f,0));
            viewCamera=cam.AddComponent<Camera>(); viewCamera.fieldOfView=36; viewCamera.clearFlags=CameraClearFlags.SolidColor; viewCamera.backgroundColor=new Color(.075f,.11f,.13f); cam.tag="MainCamera"; cam.AddComponent<AudioListener>();
            var lamp=new GameObject("Workshop key light"); lamp.transform.SetParent(transform,false); lamp.transform.rotation=Quaternion.Euler(45,-35,0);
            var light=lamp.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.15f; light.shadows=LightShadows.Soft;
            RenderSettings.ambientLight=new Color(.48f,.52f,.55f); RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        }
        void IngredientIcon(int index,Vector3 center)
        {
            var root=new GameObject(Names[index]+" ingredient icon").transform; root.SetParent(transform,false); root.localPosition=center;
            if(index==0)
            {
                for(int i=0;i<3;i++) Part("Shaft",PrimitiveType.Cylinder,new Vector3((i-1)*.065f,0,0),new Vector3(.025f,.17f,.025f),wood,root).localRotation=Quaternion.Euler(0,0,-24);
            }
            else if(index==1)
            {
                var m=new Mesh(); m.name="Arrowhead icon mesh";
                m.vertices=new[]{new Vector3(0,.19f,0),new Vector3(-.12f,-.12f,-.035f),new Vector3(.12f,-.12f,-.035f),new Vector3(0,-.12f,.055f)};
                m.triangles=new[]{0,2,1,0,3,2,0,1,3,1,2,3}; m.RecalculateNormals();
                var tip=new GameObject("Arrowhead"); tip.transform.SetParent(root,false); tip.AddComponent<MeshFilter>().sharedMesh=m; tip.AddComponent<MeshRenderer>().sharedMaterial=metal;
            }
            else
            {
                for(int i=0;i<2;i++)
                {
                    var feather=Part("Feather vane",PrimitiveType.Sphere,new Vector3((i-.5f)*.13f,0,0),new Vector3(.1f,.34f,.025f),cream,root); feather.localRotation=Quaternion.Euler(0,0,-25+i*45);
                }
            }
        }
        void Lathe(string name,Vector2[] profile,Material material)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>(); const int sides=20;
            for(int j=0;j<profile.Length-1;j++) for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                int k=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*profile[j].x,profile[j].y,Mathf.Sin(a)*profile[j].x+.12f));
                vertices.Add(new Vector3(Mathf.Cos(b)*profile[j].x,profile[j].y,Mathf.Sin(b)*profile[j].x+.12f));
                vertices.Add(new Vector3(Mathf.Cos(b)*profile[j+1].x,profile[j+1].y,Mathf.Sin(b)*profile[j+1].x+.12f));
                vertices.Add(new Vector3(Mathf.Cos(a)*profile[j+1].x,profile[j+1].y,Mathf.Sin(a)*profile[j+1].x+.12f));
                triangles.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});
            }
            var mesh=new Mesh{name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go=new GameObject(name); go.transform.SetParent(transform,false); go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }

        Material Mat(string label,Color color,float metallic=0)
        {
            var m=new Material(Shader.Find("Standard")); m.name=label; m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Glossiness",.28f); return m;
        }
        Transform Part(string label,PrimitiveType shape,Vector3 position,Vector3 scale,Material material,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(shape); go.name=label; go.transform.SetParent(parent ? parent : transform,false);
            go.transform.localPosition=position; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material; return go.transform;
        }
        TextMesh Label(string text,Vector3 position,float size,Color color)
        {
            var go=new GameObject(text); go.transform.SetParent(transform,false); go.transform.localPosition=position;
            var label=go.AddComponent<TextMesh>(); label.text=text; label.fontSize=64; label.characterSize=size*.3f; label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.color=color; return label;
        }
        void Start() { Application.runInBackground=true; if (!wheel) Build(); if(press) pressRest=press.localPosition; UpdateBubbles(); }
        void Update()
        {
            Vector3 mousePosition=Input.mousePosition;
            if(Input.GetMouseButtonDown(0) && mousePosition.y>150)
            {
                if(Physics.Raycast(viewCamera.ScreenPointToRay(mousePosition),out var hit,100))
                {
                    var target=hit.collider.GetComponentInParent<AssemblerTarget>();
                    if(target) switch(target.type)
                    {
                        case AssemblerTarget.TargetType.Bag: SelectBag((int)target.ingredient); break;
                        case AssemblerTarget.TargetType.Hopper: LoadSelected(); break;
                        case AssemblerTarget.TargetType.Output: Collect(); break;
                    }
                }
            }
            if(!assembling && Stock.TryConsumeRecipe()) { assembling=true; cycle=0; UpdateBubbles(); }
            if(assembling)
            {
                cycle+=Time.deltaTime; float phase=Mathf.Clamp01(cycle/Mathf.Max(.2f,secondsPerArrow));
                if(press) press.localPosition=pressRest-Vector3.up*Mathf.Sin(phase*Mathf.PI)*.16f;
                wheel.Rotate(160*Time.deltaTime,0,0);
                if(phase>=1) { assembling=false; completed++; StartCoroutine(Eject()); }
            }
        }
        public void SelectBag(int index)
        {
            if(loading || index<0 || index>2) return;
            selected=index; message=Names[index]+" selected. Click the bowl or press Space.";
        }
        public void LoadSelected()
        {
            if(loading) return;
            if(selected<0) { message="Choose an ingredient: 1 shafts, 2 tips, 3 feathers."; return; }
            int quantity=Mathf.Max(1,partsPerBag);
            if(quantity > AssemblyStock.Capacity-Stock[(Ingredient)selected]) { message="That ingredient bubble is full. Wait for room for a whole bag."; return; }
            StartCoroutine(LoadBag(selected,quantity));
        }
        IEnumerator LoadBag(int index,int quantity)
        {
            loading=true;
            yield return new WaitForSeconds(.35f);
            Stock.TryLoad((Ingredient)index,quantity); UpdateBubbles();
            selected=-1; loading=false; message="Ingredients loaded into the bowl.";
        }
        void UpdateBubbles()
        {
            for(int i=0;i<3;i++)
            {
                int count=Stock[(Ingredient)i]; bubbleLabels[i].text=count.ToString();
                // Count is readable at a glance; bubble brightness also tracks stock.
                bubbles[i].GetComponent<Renderer>().material.color=new Color(.69f,.87f,.91f,count>0?.7f:.4f);
            }
        }
        IEnumerator Eject()
        {
            var arrow=new GameObject("Completed arrow"); arrow.transform.SetParent(transform,false);
            Part("Wooden shaft",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.035f,.43f,.035f),wood,arrow.transform).localRotation=Quaternion.Euler(90,0,0);
            var tip=Part("Metal arrow tip",PrimitiveType.Cube,new Vector3(0,0,-.49f),new Vector3(.1f,.025f,.17f),metal,arrow.transform); tip.localRotation=Quaternion.Euler(0,0,45);
            for(int i=0;i<3;i++)
            {
                var feather=Part("Feather",PrimitiveType.Cube,new Vector3(0,0,.32f),new Vector3(.14f,.015f,.2f),cream,arrow.transform);
                feather.localRotation=Quaternion.Euler(0,0,i*120);
            }
            Vector3 end=outputBag.localPosition;
            for(float t=0;t<1;t+=Time.deltaTime*1.7f)
            {
                if(!arrow) yield break;
                arrow.transform.localPosition=Vector3.Lerp(end+Vector3.left*.65f,end,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.2f;
                yield return null;
            }
            if(arrow) Destroy(arrow);
            outputCount++; outputBag.gameObject.SetActive(true); outputLabel.text=outputCount.ToString();
        }
        public void Collect()
        {
            collected+=outputCount; outputCount=0; outputBag.gameObject.SetActive(false); outputLabel.text="0";
            message="Collected arrows: "+collected;
        }
        void OnGUI()
        {
            var input=Event.current;
            if(input.type==EventType.KeyDown)
            {
                switch(input.keyCode)
                {
                    case KeyCode.Alpha1: SelectBag(0); input.Use(); break;
                    case KeyCode.Alpha2: SelectBag(1); input.Use(); break;
                    case KeyCode.Alpha3: SelectBag(2); input.Use(); break;
                    case KeyCode.Space: LoadSelected(); input.Use(); break;
                    case KeyCode.C: Collect(); input.Use(); break;
                }
            }
            if(heading==null)
            {
                heading=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold}; heading.normal.textColor=new Color(.96f,.84f,.6f);
                body=new GUIStyle(GUI.skin.label){fontSize=16}; body.normal.textColor=Color.white;
                small=new GUIStyle(body){fontSize=13}; button=new GUIStyle(GUI.skin.button){fontSize=15};
            }
            float s=Mathf.Min(Screen.width/1100f,Screen.height/720f); GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*s);
            float w=Screen.width/s,h=Screen.height/s;
            GUI.Box(new Rect(18,18,420,112),GUIContent.none);
            GUI.Label(new Rect(34,26,390,38),"THE ARROW ASSEMBLER",heading);
            GUI.Label(new Rect(34,65,390,24),"ONE HOPPER  /  THREE INGREDIENT BUBBLES",small);
            GUI.Label(new Rect(34,92,390,26),assembling ? "ASSEMBLING  " + Mathf.RoundToInt(cycle/Mathf.Max(.2f,secondsPerArrow)*100)+"%" : "WAITING FOR SHAFT + TIP + FEATHER",body);
            GUI.Box(new Rect(18,h-142,w-36,124),GUIContent.none);
            for(int i=0;i<3;i++)
            {
                GUI.backgroundColor=Colors[i];
                if(GUI.Button(new Rect(32+i*171,h-128,161,38),(i+1)+"  "+Names[i]+" +10",button)) SelectBag(i);
            }
            GUI.backgroundColor=Color.white;
            if(GUI.Button(new Rect(555,h-128,230,38),loading?"LOADING...":"SPACE  /  LOAD HOPPER",button)) LoadSelected();
            if(GUI.Button(new Rect(797,h-128,w-829,38),"C  /  COLLECT ("+outputCount+")",button)) Collect();
            GUI.Label(new Rect(34,h-83,w-68,26),message,body);
            GUI.Label(new Rect(34,h-52,w-68,24),"Preview loads: "+partsPerBag+" parts   |   Recipe: 1 shaft + 1 tip + 1 feather   |   Made: "+completed+"   |   Collected: "+collected,small);
        }
    }
}
