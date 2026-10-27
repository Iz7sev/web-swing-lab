using UnityEngine;

namespace WebSwing
{
    public sealed class WebShooter : MonoBehaviour
    {
        public SwingMotor motor;
        public Transform hand;
        public int handIndex=1;
        public float range = 65f;
        public LayerMask targetLayers = 1 << 8;
        public float pullSpring = 85f, pullDamping = 20f, maxPullAcceleration = 28f;
        public float maxPullForce = 150f, pullGain = 2.2f;
        public float maxPullMass = 30f, holdDistance = 1.25f, breakDistance = 75f;
        public bool Active => motor.IsAttached(handIndex) || pulled != null;
        public bool PullingObject => pulled!=null;
        public float ObjectRopeLength => objectRopeLength;
        public string State => pulled != null ? "PULL HAND: " + pulled.mass.ToString("F0") + " kg" : motor.IsAttached(handIndex) ? "REEL / SWING" : "READY";
        public float Tension01 => pulled != null ? pullTension : motor.GetTension(handIndex);
        public Vector3 ForceDirection => Active ? (EndPoint - hand.position).normalized : Vector3.zero;
        public int Owner { get; private set; } = -1;
        Rigidbody pulled;
        Vector3 localHit, localNormal, staticNormal, previousHand, previousLocalHand;
        float objectRopeLength, pullTension;
        WebRopeVisual web;
        Material markerMaterial;
        GameObject marker;
        float firedAt;
        Vector3 EndPoint => pulled != null ? pulled.transform.TransformPoint(localHit) : motor.GetAnchor(handIndex);
        Vector3 HitNormal => pulled != null ? pulled.transform.TransformDirection(localNormal) : staticNormal;

        void Awake()
        {
            web = gameObject.AddComponent<WebRopeVisual>();
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Web target"; Destroy(marker.GetComponent<Collider>());
            marker.transform.localScale = Vector3.one * .075f;
            markerMaterial = new Material(Shader.Find("Sprites/Default"));
            marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            marker.SetActive(false);
        }
        public bool Aim(out RaycastHit hit)
        {
            if (!Physics.Raycast(hand.position, hand.forward, out hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            bool ground = hit.rigidbody == null && hit.normal.y > .5f;
            return (ground || ((1 << hit.collider.gameObject.layer) & targetLayers.value) != 0) &&
                (hit.rigidbody == null || hit.rigidbody.isKinematic || hit.rigidbody.mass <= maxPullMass);
        }
        public bool Fire(int owner)
        {
            if (!Aim(out RaycastHit hit)) return false;
            Rigidbody target = hit.rigidbody;
            if (target != null && (!target.isKinematic && target.mass > maxPullMass)) return false;
            Release(); Owner = owner; firedAt = Time.time;
            if (target != null && !target.isKinematic)
            {
                pulled = target; localHit = target.transform.InverseTransformPoint(hit.point);
                localNormal = target.transform.InverseTransformDirection(hit.normal);
                objectRopeLength = Vector3.Distance(hand.position, hit.point);
                previousHand = hand.position; previousLocalHand=motor.transform.InverseTransformPoint(hand.position); target.WakeUp();
            }
            else
            {
                if(motor.MovementLocked) { Owner=-1; return false; }
                staticNormal = hit.normal; motor.Attach(hit.point,handIndex);
            }
            return true;
        }
        public void Release(int owner = -1)
        {
            if (owner != -1 && Owner != owner) return;
            pulled = null; motor.ReleaseTether(handIndex); Owner = -1; pullTension = 0;
            if (web) web.Hide();
        }
        void FixedUpdate()
        {
            if (Owner >= 0 && pulled == null && !motor.IsAttached(handIndex)) { Release(); return; }
            if (!Active) return;
            if (Vector3.Distance(hand.position, EndPoint) > breakDistance) { Release(); return; }
            if (pulled == null) return;
            float dt = Time.fixedDeltaTime;
            Vector3 localHand=motor.transform.InverseTransformPoint(hand.position);
            Vector3 direction=motor.transform.InverseTransformDirection(EndPoint-hand.position);
            float pull=InteractionMotion.PullStep(previousLocalHand,localHand,direction,dt);
            previousLocalHand=localHand;
            objectRopeLength=Mathf.Max(holdDistance,objectRopeLength-pull*pullGain);
            Vector3 handVelocity = Vector3.ClampMagnitude((hand.position - previousHand) / dt, 12f);
            previousHand = hand.position;
            float forceLimit = Mathf.Min(maxPullForce, pulled.mass * maxPullAcceleration);
            Vector3 point = EndPoint;
            Vector3 force = WebPhysics.TetherForce(point, pulled.GetPointVelocity(point), hand.position,
                handVelocity, objectRopeLength, pullSpring, pullDamping, forceLimit);
            pullTension = Mathf.Clamp01(force.magnitude / forceLimit);
            pulled.AddForceAtPosition(force, point, ForceMode.Force);
        }
        void LateUpdate()
        {
            if (Active)
            {
                web.Draw(hand.position, EndPoint, HitNormal, Tension01, Time.time - firedAt);
                marker.SetActive(false);
            }
            else
            {
                web.Hide();
                bool valid = Aim(out RaycastHit hit); marker.SetActive(valid);
                if (valid)
                {
                    marker.transform.position = hit.point + hit.normal * .03f;
                    markerMaterial.color = hit.rigidbody != null ? new Color(1f,.65f,.25f) : new Color(.55f,.85f,1f);
                }
            }
        }
        void OnDisable() { if (motor) Release(); if (marker) marker.SetActive(false); }
        void OnDestroy() { if (marker) Destroy(marker); if (markerMaterial) Destroy(markerMaterial); }
    }
}
