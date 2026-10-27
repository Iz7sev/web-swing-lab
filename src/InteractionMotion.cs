using UnityEngine;

namespace WebSwing
{
    public static class InteractionMotion
    {
        public static bool ThumbsUp(float thumbExtension,float index,float middle,float ring,float little,Vector3 thumbDirection)
        {
            return thumbExtension>.8f && index<.78f && middle<.78f && ring<.78f && little<.78f &&
                thumbDirection.magnitude>.03f && Vector3.Dot(thumbDirection.normalized,Vector3.up)>.6f;
        }
        public static float PullStep(Vector3 previous,Vector3 current,Vector3 towardObject,float dt)
        {
            float displacement=Vector3.Dot(previous-current,towardObject.normalized);
            return displacement>.0012f?Mathf.Min(displacement,3f*dt):0f;
        }
        public static float WalkTarget(Vector3 leftVelocity,Vector3 rightVelocity,Vector3 forward)
        {
            float left=Vector3.Dot(leftVelocity,forward),right=Vector3.Dot(rightVelocity,forward);
            if(left*right>=0 || Mathf.Min(Mathf.Abs(left),Mathf.Abs(right))<.08f) return 0;
            return Mathf.Clamp((Mathf.Abs(left)+Mathf.Abs(right)-.25f)*1.8f,0,4.5f);
        }
    }
}
