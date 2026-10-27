using UnityEngine;

namespace WebSwing
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SwingMotor : MonoBehaviour
    {
        public Transform head;
        public float gravity=9.81f,maxSpeed=22f,reelSpeed=7f,reelAcceleration=12f,minimumRopeLength=2.5f;
        public Vector3 Velocity { get; private set; }
        public bool Attached => attached[0] || attached[1];
        public Vector3 Anchor => GetAnchor(attached[1]?1:0);
        public float RopeLength => GetLength(attached[1]?1:0);
        public float Tension01 => Mathf.Max(GetTension(0),GetTension(1));
        public Vector3 BodyPoint => transform.position+body.center;
        public bool MovementLocked { get; private set; }
        readonly bool[] attached=new bool[2];
        readonly Vector3[] anchors=new Vector3[2];
        readonly float[] lengths=new float[2],reelSpeeds=new float[2];
        CharacterController body;
        Vector3 spawn;
        Vector2 move;
        bool jump;
        float walkSpeed;
        public void SetDesktopMove(Vector2 value,bool jumpPressed) { move=value; jump|=jumpPressed; }
        public void SetArmWalkSpeed(float speed) { walkSpeed=Mathf.Clamp(speed,0,4.5f); }
        public bool IsAttached(int slot) => attached[Mathf.Clamp(slot,0,1)];
        public Vector3 GetAnchor(int slot) => anchors[Mathf.Clamp(slot,0,1)];
        public float GetLength(int slot) => lengths[Mathf.Clamp(slot,0,1)];
        public float GetTension(int slot) => attached[slot]?Mathf.Clamp01((Vector3.Distance(BodyPoint,anchors[slot])/Mathf.Max(.1f,lengths[slot])-.9f)*10):0;
        void Awake()
        {
            body=GetComponent<CharacterController>(); spawn=transform.position;
            Debug.Log("WEB_MOTION_READY: independent two-hand ropes, gravity and arm walking.");
        }
        public void Attach(Vector3 point,int slot=1)
        {
            if(MovementLocked) return;
            anchors[slot]=point; lengths[slot]=Mathf.Max(minimumRopeLength,Vector3.Distance(BodyPoint,point));
            attached[slot]=true; reelSpeeds[slot]=0;
        }
        public void ReleaseTether(int slot) { attached[slot]=false; reelSpeeds[slot]=0; }
        public void Release() { ReleaseTether(0); ReleaseTether(1); }
        public void SetMovementLocked(bool value)
        {
            Release(); MovementLocked=value; Velocity=Vector3.zero; walkSpeed=0;
            body.enabled=!value;
            transform.position=value?Vector3.zero:spawn;
        }
        public void ResetPlayer()
        {
            Release(); Velocity=Vector3.zero; walkSpeed=0;
            body.enabled=false; transform.position=MovementLocked?Vector3.zero:spawn;
            body.enabled=!MovementLocked;
        }
        void FixedUpdate()
        {
            if(MovementLocked) { Velocity=Vector3.zero; return; }
            float dt=Time.fixedDeltaTime;
            float height=Mathf.Clamp(head.localPosition.y,.8f,2.2f);
            body.height=height; body.center=new Vector3(head.localPosition.x,height*.5f,head.localPosition.z);
            Vector3 v=Velocity;
            if(body.isGrounded && v.y<0) v.y=-1;
            if(jump && body.isGrounded) v.y=6;
            jump=false;
            Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            if(!Attached && body.isGrounded && move.sqrMagnitude<.01f)
            {
                Vector3 planar=Vector3.MoveTowards(new Vector3(v.x,0,v.z),forward*walkSpeed,8*dt);
                v.x=planar.x; v.z=planar.z;
            }
            v+=(forward*move.y+right*move.x)*(Attached?5:12)*dt;
            v+=Vector3.down*gravity*dt; v=Vector3.ClampMagnitude(v,maxSpeed);
            float pairBudget=attached[0]&&attached[1]?Mathf.Max(0,lengths[0]+lengths[1]-Vector3.Distance(anchors[0],anchors[1])-.15f)*.5f:float.MaxValue;
            var shortening=Vector2.zero;
            for(int s=0;s<2;s++) if(attached[s])
            {
                float remaining=Mathf.Max(0,lengths[s]-minimumRopeLength);
                float target=Mathf.Min(reelSpeed,Mathf.Sqrt(2*reelAcceleration*remaining));
                reelSpeeds[s]=Mathf.MoveTowards(reelSpeeds[s],target,reelAcceleration*dt);
                shortening[s]=Mathf.Min(Mathf.Min(remaining,reelSpeeds[s]*dt),pairBudget);
                lengths[s]-=shortening[s];
            }
            Vector3 desired=BodyPoint+v*dt;
            for(int iteration=0;iteration<4;iteration++) for(int s=0;s<2;s++) if(attached[s])
                desired=WebPhysics.Constrain(desired,anchors[s],lengths[s],ref v,shortening[s]/dt);
            v=Vector3.ClampMagnitude(v,maxSpeed);
            Vector3 before=transform.position;
            CollisionFlags flags=body.Move(desired-BodyPoint);
            if((flags&CollisionFlags.Above)!=0 && v.y>0) v.y=0;
            if((flags&CollisionFlags.Below)!=0 && v.y<0) v.y=-1;
            if((flags&CollisionFlags.Sides)!=0)
            { var actual=(transform.position-before)/dt; v.x=actual.x; v.z=actual.z; }
            Velocity=v;
            if(transform.position.y< -20) ResetPlayer();
        }
    }
}
