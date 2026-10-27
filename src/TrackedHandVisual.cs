using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.Rendering;

namespace WebSwing
{
    public sealed class TrackedHandVisual : MonoBehaviour
    {
        static readonly XRHandJointID[][] chains={
            new[]{XRHandJointID.ThumbMetacarpal,XRHandJointID.ThumbProximal,XRHandJointID.ThumbDistal,XRHandJointID.ThumbTip},
            new[]{XRHandJointID.IndexProximal,XRHandJointID.IndexIntermediate,XRHandJointID.IndexDistal,XRHandJointID.IndexTip},
            new[]{XRHandJointID.MiddleProximal,XRHandJointID.MiddleIntermediate,XRHandJointID.MiddleDistal,XRHandJointID.MiddleTip},
            new[]{XRHandJointID.RingProximal,XRHandJointID.RingIntermediate,XRHandJointID.RingDistal,XRHandJointID.RingTip},
            new[]{XRHandJointID.LittleProximal,XRHandJointID.LittleIntermediate,XRHandJointID.LittleDistal,XRHandJointID.LittleTip}};
        readonly Transform[,] bones=new Transform[5,3];
        readonly Transform[] tips=new Transform[5];
        Mesh palm;
        MeshRenderer palmRenderer;
        Material skin;
        readonly Vector3[] vertices=new Vector3[12];
        readonly int[] triangles={0,1,2,0,2,3,0,3,4,0,4,5,6,8,7,6,9,8,6,10,9,6,11,10,
            0,6,1,1,6,7,1,7,2,2,7,8,2,8,3,3,8,9,3,9,4,4,9,10,4,10,5,5,10,11,5,11,0,0,11,6};
        void Awake()
        {
            skin=new Material(Shader.Find("Standard")); skin.color=new Color(.82f,.61f,.44f);
            skin.SetFloat("_Glossiness",.25f); skin.enableInstancing=true;
            var palmObject=new GameObject("Tracked palm"); palmObject.transform.SetParent(transform,false);
            palm=palmObject.AddComponent<MeshFilter>().mesh=new Mesh{name="Articulated palm"};
            palmRenderer=palmObject.AddComponent<MeshRenderer>(); palmRenderer.sharedMaterial=skin;
            palmRenderer.shadowCastingMode=ShadowCastingMode.Off;
            for(int f=0;f<5;f++)
            {
                for(int b=0;b<3;b++)
                {
                    var bone=GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    bone.name="Tracked finger segment"; Destroy(bone.GetComponent<Collider>());
                    bone.transform.SetParent(transform,false); bone.GetComponent<Renderer>().sharedMaterial=skin;
                    bone.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off; bones[f,b]=bone.transform;
                }
                var tip=GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(tip.GetComponent<Collider>());
                tip.name="Tracked fingertip"; tip.transform.SetParent(transform,false);
                tip.GetComponent<Renderer>().sharedMaterial=skin; tips[f]=tip.transform;
            }
            SetVisible(false);
        }
        static bool Point(XRHand hand,XRHandJointID id,out Vector3 p)
        { bool ok=hand.GetJoint(id).TryGetPose(out Pose pose); p=pose.position; return ok; }
        public void Draw(XRHand hand)
        {
            if(!hand.isTracked) { SetVisible(false); return; }
            for(int f=0;f<5;f++)
            {
                float width=f==0?.023f:f==4?.016f:.020f;
                for(int b=0;b<3;b++)
                {
                    bool valid=Point(hand,chains[f][b],out var a)&Point(hand,chains[f][b+1],out var z);
                    var bone=bones[f,b]; bone.gameObject.SetActive(valid);
                    if(!valid) continue;
                    bone.localPosition=(a+z)*.5f;
                    bone.localRotation=Quaternion.FromToRotation(Vector3.up,(z-a).normalized);
                    bone.localScale=new Vector3(width*(1-b*.12f),Vector3.Distance(a,z)*.5f,width*(1-b*.12f));
                }
                bool tipValid=Point(hand,chains[f][3],out var tip);
                tips[f].gameObject.SetActive(tipValid); tips[f].localPosition=tip;
                tips[f].localScale=Vector3.one*width*.68f;
            }
            bool palmValid=Point(hand,XRHandJointID.Wrist,out var wrist)&Point(hand,XRHandJointID.IndexProximal,out var index)&
                Point(hand,XRHandJointID.MiddleProximal,out var middle)&Point(hand,XRHandJointID.RingProximal,out var ring)&
                Point(hand,XRHandJointID.LittleProximal,out var little);
            palmRenderer.enabled=palmValid;
            if(!palmValid) return;
            Vector3 across=index-little;
            Vector3 normal=Vector3.Cross(index-wrist,little-wrist).normalized*.008f;
            vertices[0]=wrist+across*.30f; vertices[1]=index; vertices[2]=middle;
            vertices[3]=ring; vertices[4]=little; vertices[5]=wrist-across*.30f;
            for(int i=0;i<6;i++) { vertices[i+6]=vertices[i]-normal; vertices[i]+=normal; }
            palm.vertices=vertices; palm.triangles=triangles; palm.RecalculateNormals(); palm.RecalculateBounds();
        }
        public void SetVisible(bool value)
        {
            if(palmRenderer) palmRenderer.enabled=value;
            foreach(var bone in bones) if(bone) bone.gameObject.SetActive(value);
            foreach(var tip in tips) if(tip) tip.gameObject.SetActive(value);
        }
        void OnDestroy() { if(skin) Destroy(skin); if(palm) Destroy(palm); }
    }
}
