using UnityEngine;
using UnityEngine.Rendering;

namespace WebSwing
{
    public sealed class WebRopeVisual : MonoBehaviour
    {
        readonly LineRenderer[] strands = new LineRenderer[5];
        readonly LineRenderer[] spokes = new LineRenderer[8];
        readonly LineRenderer[] rings = new LineRenderer[2];
        Material material;
        Texture2D threadTexture;
        void Awake()
        {
            material = new Material(Shader.Find("Standard"));
            material.color = new Color(.94f,.94f,.88f,.93f);
            material.SetFloat("_Glossiness",.28f);
            threadTexture=new Texture2D(64,8,TextureFormat.RGB24,true);
            var pixels=new Color[64*8];
            for(int y=0;y<8;y++) for(int x=0;x<64;x++)
            {
                float fibre=.72f+.23f*Mathf.Abs(Mathf.Sin(x*.7f+y*1.2f));
                pixels[y*64+x]=Color.white*fibre;
            }
            threadTexture.SetPixels(pixels); threadTexture.Apply(true,true); material.mainTexture=threadTexture;
            for (int i=0;i<5;i++) strands[i]=Line("Web silk filament",65,.0065f,.0035f);
            for (int i=0;i<8;i++) spokes[i]=Line("Web attachment spoke",2,.006f,.0025f);
            for (int i=0;i<2;i++) rings[i]=Line("Web attachment ring",17,.003f,.003f);
            Hide();
        }
        LineRenderer Line(string name,int count,float widthA,float widthB)
        {
            var child=new GameObject(name); child.transform.SetParent(transform,false);
            var line=child.AddComponent<LineRenderer>(); line.sharedMaterial=material;
            line.positionCount=count; line.startWidth=widthA; line.endWidth=widthB;
            line.numCapVertices=2; line.numCornerVertices=1;
            line.generateLightingData=true; line.textureMode=LineTextureMode.Tile;
            line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
            return line;
        }
        public void Draw(Vector3 start,Vector3 end,Vector3 normal,float tension,float age)
        {
            Vector3 delta=end-start; float distance=delta.magnitude;
            Vector3 forward=distance>.001f?delta/distance:Vector3.forward;
            Vector3 side=Vector3.Cross(forward,Vector3.up);
            if (side.sqrMagnitude<.01f) side=Vector3.Cross(forward,Vector3.right);
            side.Normalize(); Vector3 up=Vector3.Cross(side,forward).normalized;
            float progress=Mathf.Clamp01(age*90f/Mathf.Max(distance,.1f));
            float sag=Mathf.Min(distance*.025f,.65f)*(1f-Mathf.Clamp01(tension));
            for (int s=0;s<strands.Length;s++)
            {
                var line=strands[s]; line.enabled=true;
                for (int j=0;j<line.positionCount;j++)
                {
                    float t=j/(float)(line.positionCount-1)*progress;
                    float envelope=Mathf.Sin(t*Mathf.PI);
                    float spiral=t*distance*30f+s*Mathf.PI*2f/5f;
                    float launch=Mathf.Exp(-age*8f)*.075f;
                    Vector3 fibre=(side*Mathf.Cos(spiral)+up*Mathf.Sin(spiral))*.005f;
                    Vector3 ripple=side*Mathf.Sin(t*26f-age*30f)*launch*envelope;
                    line.SetPosition(j,Vector3.Lerp(start,end,t)+fibre*envelope+Vector3.down*(4f*t*(1f-t)*sag)+ripple);
                }
            }
            bool landed=progress>=1f;
            Quaternion rotation=Quaternion.FromToRotation(Vector3.forward,normal.normalized);
            Vector3 center=end+normal*.012f;
            for (int i=0;i<spokes.Length;i++)
            {
                var line=spokes[i]; line.enabled=landed;
                float angle=i*Mathf.PI/4f;
                float radius=.14f+(i%3)*.024f;
                line.SetPosition(0,center);
                line.SetPosition(1,center+rotation*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0));
            }
            for(int r=0;r<rings.Length;r++)
            {
                var line=rings[r]; line.enabled=landed;
                for(int j=0;j<17;j++)
                {
                    float angle=j*Mathf.PI/8f;
                    float radius=(.07f+r*.065f)*(j%2==0?1f:.88f);
                    line.SetPosition(j,center+rotation*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0));
                }
            }
        }
        public void Hide()
        {
            foreach(var line in strands) if(line) line.enabled=false;
            foreach(var line in spokes) if(line) line.enabled=false;
            foreach(var line in rings) if(line) line.enabled=false;
        }
        void OnDestroy() { if(material) Destroy(material); if(threadTexture) Destroy(threadTexture); }
    }
}
