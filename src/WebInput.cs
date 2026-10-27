using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace WebSwing
{
    public sealed class WebInput : MonoBehaviour
    {
        public Transform head,aim;
        public SwingMotor motor;
        public WebShooter shooter,leftShooter;
        public WorldModes modes;
        public float poseHoldTime=.12f,poseReleaseTime=.16f,trackingGrace=.25f;
        public string TrackingStatus { get; private set; }="DESKTOP";
        public bool WalkEnabled=true;
        readonly List<XRHandSubsystem> systems=new List<XRHandSubsystem>();
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();
        readonly float[] held=new float[2],absent=new float[2];
        readonly bool[] thumbsUp=new bool[2];
        bool menuReady=true;
        readonly bool[] latched=new bool[2],blocked=new bool[2],pinched=new bool[2],tracked=new bool[2],open=new bool[2];
        readonly Vector3[] wrists=new Vector3[2],previousWrists=new Vector3[2];
        readonly TrackedHandVisual[] visuals=new TrackedHandVisual[2];
        XRHandSubsystem hands;
        bool wasXR,floorSet,walkSample;
        float yaw,pitch,menuHold,walkFiltered;
        WebShooter ForHand(int index) => index==0?leftShooter:shooter;
        public bool AnyWeb => shooter.Active || (leftShooter && leftShooter.Active);
        void Start()
        {
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Left tracked hand":"Right tracked hand");
                go.transform.SetParent(transform,false); visuals[i]=go.AddComponent<TrackedHandVisual>();
            }
        }
        public void ReleaseAll()
        {
            menuHold=0;
            if(shooter) shooter.Release(); if(leftShooter) leftShooter.Release(); motor.SetArmWalkSpeed(0);
            for(int i=0;i<2;i++) { blocked[i]=true; latched[i]=false; held[i]=0; }
        }
        void Update()
        {
            bool xr=XRSettings.isDeviceActive;
            if(xr!=wasXR) { ReleaseAll(); wasXR=xr; walkSample=false; Cursor.lockState=CursorLockMode.None; }
            if(xr) UpdateXR(); else UpdateDesktop();
        }
        void UpdateXR()
        {
            if(!floorSet)
            {
                inputs.Clear(); SubsystemManager.GetSubsystems(inputs);
                foreach(var subsystem in inputs) if(subsystem.running) floorSet|=subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
            motor.SetDesktopMove(Vector2.zero,false);
            var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 position)) head.localPosition=position;
            if(device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation,out Quaternion rotation)) head.localRotation=rotation;
            if(hands==null || !hands.running) { systems.Clear(); SubsystemManager.GetSubsystems(systems); hands=systems.Find(s=>s.running); }
            if(hands==null) { menuHold=0; TrackingStatus="XR: WAITING FOR HANDS"; LoseHand(0); LoseHand(1); StopWalk(); return; }
            UpdateHand(hands.leftHand,0); UpdateHand(hands.rightHand,1);
            bool menuGesture=(tracked[0]&&thumbsUp[0])||(tracked[1]&&thumbsUp[1]);
            if(!menuGesture) menuReady=true;
            menuHold=menuGesture&&menuReady?menuHold+Time.deltaTime:0;
            if(menuHold>1f && modes && !modes.MenuVisible)
            { modes.ShowMenu(); menuReady=false; menuHold=0; }
            UpdateWalk();
            TrackingStatus="HANDS: "+(tracked[0]?"L ":"")+(tracked[1]?"R ":"")+" | WALK "+walkFiltered.ToString("F1")+" m/s";
        }
        void UpdateHand(XRHand hand,int index)
        {
            var web=ForHand(index);
            if(!web) return;
            if(!hand.isTracked || !Point(hand,XRHandJointID.Wrist,out var wrist) ||
                !Point(hand,XRHandJointID.IndexProximal,out var knuckleA) || !Point(hand,XRHandJointID.LittleProximal,out var knuckleB))
            { LoseHand(index); return; }
            visuals[index].Draw(hand); tracked[index]=true; wrists[index]=wrist;
            bool valid=Finger(hand,XRHandJointID.IndexProximal,XRHandJointID.IndexIntermediate,XRHandJointID.IndexDistal,XRHandJointID.IndexTip,out float a);
            valid&=Finger(hand,XRHandJointID.MiddleProximal,XRHandJointID.MiddleIntermediate,XRHandJointID.MiddleDistal,XRHandJointID.MiddleTip,out float b);
            valid&=Finger(hand,XRHandJointID.RingProximal,XRHandJointID.RingIntermediate,XRHandJointID.RingDistal,XRHandJointID.RingTip,out float c);
            valid&=Finger(hand,XRHandJointID.LittleProximal,XRHandJointID.LittleIntermediate,XRHandJointID.LittleDistal,XRHandJointID.LittleTip,out float d);
            if(!valid) { LoseHand(index); return; }
            bool pose=a>.82f&&d>.78f&&b<.72f&&c<.72f;
            open[index]=a>.82f&&b>.82f&&c>.82f&&d>.78f;
            if(open[index]) blocked[index]=false;
            web.hand.position=transform.TransformPoint(wrist);
            Vector3 direction=transform.TransformDirection((knuckleA+knuckleB)*.5f-wrist).normalized;
            if(direction.sqrMagnitude>.1f) web.hand.rotation=Quaternion.LookRotation(direction,transform.up);
            bool fingerTips=Point(hand,XRHandJointID.IndexTip,out var tip)&Point(hand,XRHandJointID.ThumbTip,out var thumb);
            bool thumbValid=Finger(hand,XRHandJointID.ThumbMetacarpal,XRHandJointID.ThumbProximal,
                XRHandJointID.ThumbDistal,XRHandJointID.ThumbTip,out float extension);
            thumbValid &= Point(hand,XRHandJointID.ThumbProximal,out var thumbBase);
            thumbsUp[index]=fingerTips&&thumbValid&&InteractionMotion.ThumbsUp(extension,a,b,c,d,thumb-thumbBase);
            bool pinch=fingerTips&&Vector3.Distance(tip,thumb)<.028f;
            if(modes && modes.MenuVisible)
            {
                web.Release(); held[index]=0; latched[index]=false; blocked[index]=true;
                Vector3 rayDirection=transform.TransformDirection(tip-knuckleA).normalized;
                modes.Point(index,new Ray(transform.TransformPoint(knuckleA),rayDirection),pinch&&!pinched[index]);
                pinched[index]=pinch; return;
            }
            pinched[index]=pinch;
            if(pose&&!blocked[index])
            {
                absent[index]=0; held[index]+=Time.deltaTime;
                if(!latched[index]&&held[index]>=poseHoldTime) { latched[index]=true; web.Fire(index); }
            }
            else
            {
                held[index]=0; absent[index]+=Time.deltaTime;
                if(absent[index]>poseReleaseTime) { web.Release(); latched[index]=false; }
            }
        }
        void UpdateWalk()
        {
            float dt=Mathf.Max(Time.deltaTime,.001f);
            bool allowed=WalkEnabled&&!thumbsUp[0]&&!thumbsUp[1]&&tracked[0]&&tracked[1]&&!AnyWeb&&(!modes||(!modes.IsMR&&!modes.MenuVisible))&&
                wrists[0].y<head.localPosition.y-.3f&&wrists[1].y<head.localPosition.y-.3f;
            if(!walkSample || !allowed)
            {
                previousWrists[0]=wrists[0]; previousWrists[1]=wrists[1]; walkSample=allowed; StopWalk(); return;
            }
            Vector3 left=(wrists[0]-previousWrists[0])/dt,right=(wrists[1]-previousWrists[1])/dt;
            previousWrists[0]=wrists[0]; previousWrists[1]=wrists[1];
            Vector3 forward=Vector3.ProjectOnPlane(head.localRotation*Vector3.forward,Vector3.up).normalized;
            float target=InteractionMotion.WalkTarget(left,right,forward);
            walkFiltered=Mathf.MoveTowards(walkFiltered,target,Time.deltaTime*(target>walkFiltered?6f:7f));
            motor.SetArmWalkSpeed(walkFiltered);
        }
        void StopWalk() { walkFiltered=0; motor.SetArmWalkSpeed(0); }
        void LoseHand(int index)
        {
            thumbsUp[index]=false; tracked[index]=false; open[index]=false; pinched[index]=false;
            if(visuals[index]) visuals[index].SetVisible(false);
            held[index]=0; absent[index]+=Time.deltaTime;
            if(absent[index]>trackingGrace) { if(ForHand(index)) ForHand(index).Release(); latched[index]=false; blocked[index]=true; }
        }
        static bool Point(XRHand hand,XRHandJointID id,out Vector3 p)
        { bool valid=hand.GetJoint(id).TryGetPose(out Pose pose); p=pose.position; return valid; }
        static bool Finger(XRHand hand,XRHandJointID a,XRHandJointID b,XRHandJointID c,XRHandJointID d,out float ratio)
        {
            ratio=0;
            if(!Point(hand,a,out var p)||!Point(hand,b,out var q)||!Point(hand,c,out var r)||!Point(hand,d,out var s)) return false;
            float length=Vector3.Distance(p,q)+Vector3.Distance(q,r)+Vector3.Distance(r,s);
            if(length<.001f) return false; ratio=Vector3.Distance(p,s)/length; return true;
        }
        void UpdateDesktop()
        {
            foreach(var visual in visuals) if(visual) visual.SetVisible(false);
            var mouse=Mouse.current; var keyboard=Keyboard.current;
            if(mouse==null||keyboard==null) return;
            TrackingStatus="LMB: RIGHT / RMB: LEFT / F: PULL / M: MENU";
            if(keyboard.mKey.wasPressedThisFrame && modes) { if(modes.MenuVisible)modes.HideMenu();else modes.ShowMenu(); }
            if(keyboard.digit1Key.wasPressedThisFrame && modes) modes.SelectMode(false);
            if(keyboard.digit2Key.wasPressedThisFrame && modes) modes.SelectMode(true);
            if(keyboard.escapeKey.wasPressedThisFrame) { Cursor.lockState=CursorLockMode.None; ReleaseAll(); }
            if(modes && modes.MenuVisible)
            {
                Cursor.lockState=CursorLockMode.None;
                modes.Point(1,head.GetComponent<Camera>().ScreenPointToRay(mouse.position.ReadValue()),mouse.leftButton.wasPressedThisFrame);
                motor.SetDesktopMove(Vector2.zero,false); return;
            }
            if(mouse.leftButton.wasPressedThisFrame && Cursor.lockState!=CursorLockMode.Locked) Cursor.lockState=CursorLockMode.Locked;
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                Vector2 delta=mouse.delta.ReadValue()*.1f; yaw+=delta.x; pitch=Mathf.Clamp(pitch-delta.y,-85,85);
                head.localRotation=Quaternion.Euler(pitch,yaw,0);
            }
            head.localPosition=new Vector3(0,1.65f,0);
            float pullOffset=keyboard.fKey.isPressed?.65f:0;
            aim.SetPositionAndRotation(head.position+head.right*.18f-head.up*.2f-head.forward*pullOffset,head.rotation);
            if(leftShooter) leftShooter.hand.SetPositionAndRotation(head.position-head.right*.18f-head.up*.2f-head.forward*pullOffset,head.rotation);
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                if(mouse.leftButton.wasPressedThisFrame) shooter.Fire(2);
                if(mouse.rightButton.wasPressedThisFrame && leftShooter) leftShooter.Fire(3);
            }
            if(mouse.leftButton.wasReleasedThisFrame) shooter.Release(2);
            if(mouse.rightButton.wasReleasedThisFrame && leftShooter) leftShooter.Release(3);
            Vector2 move=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            motor.SetDesktopMove(Vector2.ClampMagnitude(move,1),keyboard.spaceKey.wasPressedThisFrame);
            if(keyboard.rKey.wasPressedThisFrame) { ReleaseAll(); motor.ResetPlayer(); }
        }
        void OnApplicationFocus(bool focus) { if(!focus) ReleaseAll(); }
        void OnDisable() { if(motor) ReleaseAll(); Cursor.lockState=CursorLockMode.None; }
    }
}

