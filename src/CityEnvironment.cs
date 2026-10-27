using UnityEngine;
using UnityEngine.Rendering;

namespace WebSwing
{
    // Shared procedural materials and batched architecture keep the Quest scene inexpensive.
    public static class CityEnvironment
    {
        static Transform architecture;
        public static Transform Root { get; private set; }
        static Material concrete, asphalt, metal, wood, painted, glass, stone, yellow, white;
        public static void Build()
        {
            Root=new GameObject("VR City").transform;
            architecture=new GameObject("City / static architecture").transform; architecture.SetParent(Root,false);
            var atlas=Resources.Load<Texture2D>("WebSwing/CitySurfaces");
            concrete=Surface(new Color(.78f,.77f,.73f),.08f,0,Tile(atlas,1,1,Grain(128,17,.16f)));
            asphalt=Surface(new Color(.52f,.55f,.56f),.03f,0,Tile(atlas,0,0,Grain(128,29,.28f)));
            metal=Surface(new Color(.5f,.55f,.58f),.38f,.55f,Tile(atlas,1,0,Grain(64,41,.06f)));
            wood=Surface(new Color(.9f,.83f,.69f),.12f,0,Tile(atlas,0,1,Wood()));
            if(atlas) Resources.UnloadAsset(atlas);
            painted=Surface(new Color(.82f,.33f,.08f),.28f,.25f,Grain(64,51,.08f));
            glass=Surface(new Color(.13f,.25f,.32f),.65f,.35f);
            stone=Surface(new Color(.65f,.63f,.57f),.08f,0,Grain(64,61,.12f));
            yellow=Surface(new Color(.94f,.65f,.17f),.12f,0);
            white=Surface(new Color(.84f,.84f,.77f),.1f,0);
            Lighting();
            Block("City ground",new Vector3(0,-1,65),new Vector3(220,2,260),asphalt);
            Block("Loading roof",new Vector3(0,5,0),new Vector3(14,1,18),concrete);
            Block("Roof structure",new Vector3(0,2.4f,-1),new Vector3(14,4.8f,16),concrete,true);
            Block("Roof edge / front",new Vector3(0,5.52f,8.7f),new Vector3(14,.03f,.12f),yellow);
            Block("Cargo instructions panel",new Vector3(4.3f,7.7f,6.5f),new Vector3(4.5f,1.9f,.12f),metal);
            for(int side=-1;side<=1;side+=2)
                Block("Panel support",new Vector3(4.3f+side*1.7f,6.2f,6.5f),new Vector3(.08f,1.5f,.08f),metal);
            for(int side=-1;side<=1;side+=2)
            {
                Block("Roof parapet",new Vector3(side*6.9f,5.9f,0),new Vector3(.22f,.8f,18),concrete,true);
                Block("Sidewalk",new Vector3(side*11.2f,.13f,72),new Vector3(3,.26f,160),stone);
                Block("Curb",new Vector3(side*9.75f,.2f,72),new Vector3(.16f,.4f,160),concrete);
                for(int i=0;i<6;i++)
                {
                    float z=22+i*25f;
                    float height=25+(i*7+(side+1)*3)%25;
                    Building(new Vector3(side*19,height*.5f,z),new Vector3(13,height,18),i+(side+1)*5);
                    if(i%2==0) StreetLamp(new Vector3(side*10.8f,0,z-9));
                }
                for(int i=0;i<5;i++)
                {
                    float h=30+(i*13)%35;
                    Block("Distant city",new Vector3(side*(43+i%2*16),h*.5f,20+i*35),new Vector3(15,h,20),concrete,true);
                }
            }
            for(int i=0;i<22;i++) Block("Lane marking",new Vector3(0,.014f,14+i*6),new Vector3(.13f,.018f,2.6f),white);
            for(int i=0;i<7;i++) Block("Crosswalk",new Vector3(-6+i*2,.018f,18),new Vector3(1.1f,.018f,3),white);
            ParkedCar(new Vector3(-7.3f,.65f,30),new Color(.19f,.26f,.32f));
            ParkedCar(new Vector3(7.3f,.65f,62),new Color(.52f,.18f,.12f));
            for(int i=0;i<6;i++)
            {
                Vector3 position=new Vector3(-3.9f+(i%3)*2.5f,6.08f,2.3f+(i/3)*3.1f);
                SpawnCrate(position,new[]{2f,4f,6f,8f,10f,12f}[i],i);
            }
            // A hinged object makes off-centre force and rotation easy to try.
            Block("Door post",new Vector3(4.8f,6.8f,-3),new Vector3(.2f,2.6f,.3f),metal);
            var door=Block("Pullable steel door",new Vector3(5.55f,6.8f,-3),new Vector3(1.3f,2.5f,.14f),painted,true,false);
            var body=door.AddComponent<Rigidbody>(); body.mass=9; body.angularDamping=2;
            body.interpolation=RigidbodyInterpolation.Interpolate;
            var hinge=door.AddComponent<HingeJoint>(); hinge.axis=Vector3.up; hinge.anchor=new Vector3(-.5f,0,0);
            hinge.useLimits=true; hinge.limits=new JointLimits{min=-100,max=100};
            // Batch only the immovable environment. Cargo and its details stay dynamic.
            StaticBatchingUtility.Combine(architecture.gameObject);
            Debug.Log("WEB_CITY_READY: textured city, six weighted crates and a hinged door.");
        }
        static void Lighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.46f,.58f,.72f);
            RenderSettings.ambientEquatorColor=new Color(.38f,.39f,.39f);
            RenderSettings.ambientGroundColor=new Color(.19f,.18f,.17f);
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Exponential;
            RenderSettings.fogColor=new Color(.57f,.65f,.72f); RenderSettings.fogDensity=.0035f;
            var sun=new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.type=LightType.Directional; sun.color=new Color(1f,.84f,.64f); sun.intensity=1.25f;
            sun.transform.rotation=Quaternion.Euler(28,-38,0); sun.shadows=LightShadows.Hard;
            sun.shadowStrength=.65f; sun.shadowBias=.045f;
            QualitySettings.shadowDistance=45; QualitySettings.shadowResolution=ShadowResolution.Low;
            QualitySettings.shadows=ShadowQuality.HardOnly;
            var skyShader=Shader.Find("Skybox/Procedural");
            if(skyShader!=null)
            {
                var sky=new Material(skyShader); sky.SetColor("_SkyTint",new Color(.53f,.63f,.77f));
                sky.SetFloat("_AtmosphereThickness",1.1f); sky.SetFloat("_Exposure",1.1f);
                sky.SetColor("_GroundColor",new Color(.38f,.36f,.32f));
                RenderSettings.skybox=sky; RenderSettings.sun=sun;
            }
        }
        static void Building(Vector3 center,Vector3 size,int seed)
        {
            var facade=Surface(Color.Lerp(new Color(.48f,.40f,.32f),new Color(.37f,.42f,.46f),(seed%4)/3f),.2f,.08f,Facade(seed));
            facade.mainTextureScale=new Vector2(size.x/8f,size.y/10f);
            Block("Office / brick facade",center,size,facade,true);
            float top=center.y+size.y*.5f;
            Block("Roof cap",new Vector3(center.x,top+.2f,center.z),new Vector3(size.x+.45f,.4f,size.z+.45f),concrete,true);
            for(int side=-1;side<=1;side+=2)
                Block("Facade corner",new Vector3(center.x+side*(size.x*.5f-.15f),center.y,center.z-size.z*.5f-.04f),new Vector3(.25f,size.y,.15f),stone,true);
            for(int i=0;i<2;i++)
            {
                Vector3 p=new Vector3(center.x-3+i*5,top+1,center.z+2);
                Block("Roof HVAC",p,new Vector3(2.8f,1.6f,3.5f),metal,true);
                for(int j=0;j<4;j++) Block("HVAC grille",p+new Vector3(-.95f+j*.6f,.84f,0),new Vector3(.13f,.08f,2.9f),stone);
            }
            // Ground floor storefront glazing and lintel.
            Block("Shop glazing",new Vector3(center.x,1.7f,center.z-size.z*.5f-.04f),new Vector3(size.x-1,2.8f,.13f),glass,true);
            Block("Shop lintel",new Vector3(center.x,3.4f,center.z-size.z*.5f-.15f),new Vector3(size.x,.4f,.4f),metal,true);
        }
        static void StreetLamp(Vector3 position)
        {
            Block("Street lamp pole",position+Vector3.up*3,new Vector3(.13f,6,.13f),metal);
            Block("Street lamp arm",position+new Vector3(position.x>0?-.7f:.7f,6,0),new Vector3(1.6f,.1f,.1f),metal);
            Block("Street lamp head",position+new Vector3(position.x>0?-1.3f:1.3f,5.93f,0),new Vector3(.65f,.12f,.38f),white);
        }
        static void ParkedCar(Vector3 p,Color color)
        {
            var paint=Surface(color,.52f,.3f);
            Block("Parked car body",p,new Vector3(1.9f,.8f,4.4f),paint,true);
            Block("Car windows",p+new Vector3(0,.65f,-.1f),new Vector3(1.65f,.65f,2.35f),glass,true);
            Block("Car roof",p+new Vector3(0,1.02f,-.1f),new Vector3(1.7f,.12f,2.4f),paint,true);
            for(int side=-1;side<=1;side+=2) for(int end=-1;end<=1;end+=2)
                Block("Tyre",p+new Vector3(side*.9f,-.3f,end*1.4f),new Vector3(.25f,.65f,.65f),asphalt);
        }
        public static GameObject SpawnCrate(Vector3 position,float mass,int index)
        {
            var crate=Block("Cargo crate / "+mass+" kg",position,Vector3.one,wood,true,false);
            var body=crate.AddComponent<Rigidbody>(); body.mass=mass;
            body.linearDamping=.16f; body.angularDamping=1.6f;
            body.interpolation=RigidbodyInterpolation.Interpolate; body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var physics=new PhysicsMaterial("Cargo friction"){dynamicFriction=.35f,staticFriction=.4f,bounciness=.08f};
            crate.GetComponent<Collider>().material=physics;
            for(int side=-1;side<=1;side+=2)
            {
                Detail(crate.transform,"Steel strap",new Vector3(side*.32f,0,0),new Vector3(.055f,1.01f,1.015f),metal);
                Detail(crate.transform,"Crate brace",new Vector3(0,side*.39f,-.51f),new Vector3(1,.12f,.08f),wood);
            }
            Detail(crate.transform,"Orange cargo tag",new Vector3(.12f,.06f,-.513f),new Vector3(.46f,.34f,.02f),painted);
            var label=new GameObject("Cargo weight label"); label.transform.SetParent(crate.transform,false);
            label.transform.localPosition=new Vector3(.12f,.07f,-.532f);
            var text=label.AddComponent<TextMesh>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.anchor=TextAnchor.MiddleCenter; text.fontSize=48; text.characterSize=.055f; text.color=Color.white;
            text.text=mass.ToString("F0")+" kg"; label.GetComponent<Renderer>().sharedMaterial=text.font.material;
            var reset=crate.AddComponent<CargoReset>(); reset.spawn=position;
            return crate;
        }
        static void Detail(Transform parent,string name,Vector3 p,Vector3 scale,Material material)
        {
            var detail=GameObject.CreatePrimitive(PrimitiveType.Cube); detail.name=name;
            Object.Destroy(detail.GetComponent<Collider>()); detail.transform.SetParent(parent,false);
            detail.transform.localPosition=p; detail.transform.localScale=scale;
            detail.GetComponent<Renderer>().sharedMaterial=material;
        }
        static GameObject Block(string name,Vector3 p,Vector3 scale,Material material,bool target=false,bool batch=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
            go.transform.SetPositionAndRotation(p,Quaternion.identity); go.transform.localScale=scale;
            go.layer=target?8:0; go.GetComponent<Renderer>().sharedMaterial=material;
            // Match texture scale to metres on the floor and platform instead of stretching one tile.
            if((material==concrete || material==asphalt) && scale.y<Mathf.Min(scale.x,scale.z)*.3f)
            {
                var tiled=new Material(material);
                tiled.mainTextureScale=new Vector2(Mathf.Max(1,scale.x*.25f),Mathf.Max(1,scale.z*.25f));
                go.GetComponent<Renderer>().sharedMaterial=tiled;
            }
            go.transform.SetParent(batch?architecture:Root,true);
            return go;
        }
        static Material Surface(Color color,float smooth,float metallic,Texture2D texture=null)
        {
            var mat=new Material(Shader.Find("Standard")); mat.color=color;
            mat.SetFloat("_Glossiness",smooth); mat.SetFloat("_Metallic",metallic);
            if(texture) mat.mainTexture=texture; mat.enableInstancing=true; return mat;
        }
        static Texture2D Grain(int size,int seed,float amount)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGB24,true);
            var random=new System.Random(seed); var pixels=new Color[size*size];
            for(int i=0;i<pixels.Length;i++) pixels[i]=Color.white*(1f-amount+(float)random.NextDouble()*amount);
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        static Texture2D Tile(Texture2D atlas,int column,int row,Texture2D fallback)
        {
            if(!atlas || !atlas.isReadable) return fallback;
            int width=atlas.width/2,height=atlas.height/2;
            // Drop a small border so neighbouring atlas materials cannot bleed into this surface.
            const int inset=3;
            var texture=new Texture2D(width-inset*2,height-inset*2,TextureFormat.RGB24,true);
            texture.SetPixels(atlas.GetPixels(column*width+inset,row*height+inset,width-inset*2,height-inset*2));
            texture.anisoLevel=4; texture.Apply(true,true);
            Object.Destroy(fallback); return texture;
        }
        static Texture2D Wood()
        {
            const int size=128; var texture=new Texture2D(size,size,TextureFormat.RGB24,true);
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float grain=Mathf.PerlinNoise(x*.35f,y*.035f)*.24f+.7f;
                if(x%32<2) grain*=.46f;
                pixels[y*size+x]=new Color(grain,grain*.94f,grain*.8f);
            }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        static Texture2D Facade(int seed)
        {
            const int size=128; var texture=new Texture2D(size,size,TextureFormat.RGB24,true);
            var pixels=new Color[size*size]; var random=new System.Random(seed+71);
            var lit=new bool[16]; for(int i=0;i<lit.Length;i++) lit[i]=random.NextDouble()>.78;
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                int cx=x%32,cy=y%32;
                float noise=(float)random.NextDouble()*.1f;
                Color c=Color.white*(.75f+noise);
                if(y%8==0 || (x+((y/8)%2)*8)%16==0) c*=.77f;
                if(cx>=7 && cx<=24 && cy>=6 && cy<=24)
                {
                    c=lit[(y/32)*4+x/32]?new Color(1.5f,1.32f,.85f):new Color(.22f,.36f,.46f);
                    c*=.8f+(cy/32f)*.35f;
                    if(cx==7||cx==24||cy==6||cy==24||cx==15) c=new Color(.22f,.23f,.23f);
                }
                pixels[y*size+x]=c;
            }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
    }
    public sealed class CargoReset : MonoBehaviour
    {
        public Vector3 spawn;
        void FixedUpdate()
        {
            if(transform.position.y>-12) return;
            var body=GetComponent<Rigidbody>(); body.position=spawn; body.rotation=Quaternion.identity;
            body.linearVelocity=body.angularVelocity=Vector3.zero;
        }
    }
}
