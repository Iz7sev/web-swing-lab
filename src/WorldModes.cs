using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace WebSwing
{
    public sealed class WorldModes : MonoBehaviour
    {
        public bool IsMR { get; private set; }
        public bool MenuVisible => menu && menu.activeSelf;
        public Transform city;
        DemoWorld world;
        Camera camera;
        ARCameraManager passthrough;
        ARSession session;
        GameObject room,menu;
        Material panelMaterial,buttonMaterial,hoverMaterial;
        readonly Dictionary<Collider,int> buttons=new Dictionary<Collider,int>();
        readonly LineRenderer[] rays=new LineRenderer[2];
        readonly Collider[] hovered=new Collider[2];
        TextMesh status,walkLabel;
        bool passthroughReported;
        float lastAction;
        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
                if(intent.Call<bool>("getBooleanExtra","web_mr",false)) SelectMode(true);
#endif
        }
        public void Initialize(DemoWorld demo)
        {
            world=demo; camera=demo.input.head.GetComponent<Camera>();
            var origin=demo.motor.gameObject.AddComponent<XROrigin>();
            var offset=new GameObject("Camera floor offset"); offset.transform.SetParent(demo.motor.transform,false);
            camera.transform.SetParent(offset.transform,false);
            origin.Camera=camera; origin.CameraFloorOffsetObject=offset;
            origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor; origin.CameraYOffset=0;
            var ar=new GameObject("AR Session"); session=ar.AddComponent<ARSession>(); session.enabled=false;
            passthrough=camera.gameObject.AddComponent<ARCameraManager>(); passthrough.enabled=false;
            camera.allowHDR=false;
            BuildMenu(); HideMenu();
            Debug.Log("WEB_MODES_READY: VR city / MR room. Either hand thumbs up for 1 second opens menu.");
        }
        void Update()
        {
            if(IsMR && passthrough.subsystem!=null && passthrough.subsystem.running && !passthroughReported)
            { passthroughReported=true; Debug.Log("WEB_PASSTHROUGH_READY: Meta camera subsystem running."); }
            if(status) status.text=(IsMR?"MR ROOM":"VR CITY")+" / POINT + PINCH\nCargo: pull hand / Buildings: auto reel";
        }
        public void SelectMode(bool mr)
        {
            world.input.ReleaseAll(); IsMR=mr; passthroughReported=false;
            if(city) city.gameObject.SetActive(!mr);
            world.motor.SetMovementLocked(mr); RenderSettings.fog=!mr;
            camera.clearFlags=mr?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;
            camera.backgroundColor=mr?Color.clear:new Color(.57f,.65f,.72f,1);
            if(XRSettings.isDeviceActive) { session.enabled=mr; passthrough.enabled=mr; }
            if(mr) SpawnRoomObjects(); else if(room) room.SetActive(false);
            HideMenu(); Debug.Log("WEB_MODE_CHANGED: "+(mr?"MR / physical-room coordinates":"VR / city locomotion"));
        }
        public void ResetPosition()
        {
            world.input.ReleaseAll(); world.motor.ResetPlayer();
            foreach(var cargo in FindObjectsByType<CargoReset>(FindObjectsSortMode.None))
            {
                var rb=cargo.GetComponent<Rigidbody>(); rb.position=cargo.spawn; rb.rotation=Quaternion.identity;
                rb.linearVelocity=rb.angularVelocity=Vector3.zero;
            }
            if(IsMR) SpawnRoomObjects(); HideMenu();
        }
        public void SpawnRoomObjects()
        {
            if(room) { room.SetActive(false); Destroy(room); }
            room=new GameObject("MR / room cargo");
            Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            Vector3 center=Vector3.ProjectOnPlane(camera.transform.position,Vector3.up)+forward*1.7f;
            for(int i=0;i<4;i++)
            {
                Vector3 p=center+right*((i%2-.5f)*.75f)+forward*(i/2*.7f); p.y=.24f;
                var crate=CityEnvironment.SpawnCrate(p,1+i,i);
                crate.transform.SetParent(room.transform,true); crate.transform.localScale=Vector3.one*.42f;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="MR virtual floor collider";
            floor.transform.SetParent(room.transform,false); floor.transform.position=new Vector3(center.x,-.06f,center.z);
            floor.transform.localScale=new Vector3(30,.1f,30); floor.GetComponent<Renderer>().enabled=false;
            Debug.Log("WEB_MR_OBJECTS_READY: four crates in front of the user, floor at tracking origin.");
        }
        public void ShowMenu()
        {
            world.input.ReleaseAll();
            Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
            menu.transform.SetPositionAndRotation(camera.transform.position+forward*.85f-Vector3.up*.04f,Quaternion.LookRotation(forward));
            menu.SetActive(true); Cursor.lockState=CursorLockMode.None;
        }
        public void HideMenu()
        {
            if(menu) menu.SetActive(false);
            foreach(var ray in rays) if(ray) ray.enabled=false;
            for(int i=0;i<2;i++) hovered[i]=null;
        }
        public void Point(int hand,Ray ray,bool click)
        {
            if(!MenuVisible) return;
            Collider hitButton=null; Vector3 end=ray.origin+ray.direction*2;
            if(Physics.Raycast(ray,out RaycastHit hit,3,1<<10,QueryTriggerInteraction.Collide)) { hitButton=hit.collider; end=hit.point; }
            hovered[hand]=hitButton;
            foreach(var item in buttons) item.Key.GetComponent<Renderer>().sharedMaterial=
                item.Key==hovered[0]||item.Key==hovered[1]?hoverMaterial:buttonMaterial;
            rays[hand].enabled=true; rays[hand].SetPosition(0,ray.origin); rays[hand].SetPosition(1,end);
            if(!click||!hitButton||Time.unscaledTime-lastAction<.35f) return;
            lastAction=Time.unscaledTime;
            switch(buttons[hitButton])
            {
                case 0: SelectMode(false); break;
                case 1: SelectMode(true); break;
                case 2: ResetPosition(); break;
                case 3: if(IsMR)SpawnRoomObjects();else ResetPosition(); break;
                case 4: world.input.WalkEnabled=!world.input.WalkEnabled; walkLabel.text="WALK: "+(world.input.WalkEnabled?"ON":"OFF"); break;
                default: HideMenu(); break;
            }
        }
        void BuildMenu()
        {
            panelMaterial=new Material(Shader.Find("Unlit/Color")){color=new Color(.025f,.045f,.065f,1)};
            buttonMaterial=new Material(Shader.Find("Unlit/Color")){color=new Color(.10f,.19f,.25f,1)};
            hoverMaterial=new Material(Shader.Find("Unlit/Color")){color=new Color(.12f,.53f,.65f,1)};
            menu=new GameObject("Hand menu");
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube); panel.transform.SetParent(menu.transform,false);
            panel.transform.localScale=new Vector3(.85f,.70f,.02f); Destroy(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial=panelMaterial;
            status=Text(menu.transform,new Vector3(0,.255f,-.025f),"WEB / MENU",.016f);
            string[] names={"VR CITY","MR ROOM","RESET","SPAWN","WALK: ON","CLOSE"};
            for(int i=0;i<6;i++)
            {
                var button=GameObject.CreatePrimitive(PrimitiveType.Cube); button.name=names[i]; button.layer=10;
                button.transform.SetParent(menu.transform,false);
                button.transform.localPosition=new Vector3(i%2==0?-.205f:.205f,.10f-i/2*.145f,-.025f);
                button.transform.localScale=new Vector3(.37f,.11f,.018f);
                button.GetComponent<Renderer>().sharedMaterial=buttonMaterial;
                button.GetComponent<Collider>().isTrigger=true; buttons.Add(button.GetComponent<Collider>(),i);
                var label=Text(menu.transform,button.transform.localPosition+new Vector3(0,0,-.014f),names[i],.023f);
                if(i==4) walkLabel=label;
            }
            Text(menu.transform,new Vector3(0,-.30f,-.025f),"Thumbs up 1 second: MENU",.012f);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Menu pointer"); go.transform.SetParent(transform,false);
                rays[i]=go.AddComponent<LineRenderer>(); rays[i].sharedMaterial=hoverMaterial;
                rays[i].positionCount=2; rays[i].startWidth=.002f; rays[i].endWidth=.001f; rays[i].enabled=false;
            }
        }
        static TextMesh Text(Transform parent,Vector3 p,string value,float size)
        {
            var go=new GameObject("Menu text"); go.transform.SetParent(parent,false); go.transform.localPosition=p;
            var text=go.AddComponent<TextMesh>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=64; text.characterSize=size*.25f; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
            text.color=new Color(.89f,.95f,1); text.text=value;
            go.GetComponent<Renderer>().sharedMaterial=text.font.material; return text;
        }
    }
}
