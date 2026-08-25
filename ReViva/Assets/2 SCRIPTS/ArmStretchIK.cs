using UnityEngine;

public class ArmStretchIK_Both : MonoBehaviour
{
    [System.Serializable]
    public class Arm
    {
        public Transform upperArm;
        public Transform lowerArm;
        public Transform handTarget;

        // 👉 NOVO (physics hand)
        public Rigidbody handRb;
        public float followSpeed;

        [HideInInspector] public float originalUpperLength;
        [HideInInspector] public float originalLowerLength;

        [HideInInspector] public Vector3 upperOriginalScale;
        [HideInInspector] public Vector3 lowerOriginalScale;
    }

    public Arm leftArm;
    public Arm rightArm;

    public float maxStretch = 1.3f;
    public float stretchStart = 0.9f;

    void Start()
    {
        SetupArm(leftArm);
        SetupArm(rightArm);
    }

    void FixedUpdate()
    {
        UpdatePhysicsHand(leftArm);
        UpdatePhysicsHand(rightArm);
    }

    void LateUpdate()
    {
        UpdateArm(leftArm);
        UpdateArm(rightArm);
    }

    void SetupArm(Arm arm)
    {
        arm.originalUpperLength = Vector3.Distance(arm.upperArm.position, arm.lowerArm.position);
        arm.originalLowerLength = Vector3.Distance(arm.lowerArm.position, arm.handTarget.position);

        arm.upperOriginalScale = arm.upperArm.localScale;
        arm.lowerOriginalScale = arm.lowerArm.localScale;
    }

    // 👉 PHYSICS FOLLOW POR MÃO
    void UpdatePhysicsHand(Arm arm)
    {
        if (arm.handRb == null || arm.handTarget == null) return;

        Vector3 dir = arm.handTarget.position - arm.handRb.position;
        arm.handRb.linearVelocity = dir * arm.followSpeed;
    }

    void UpdateArm(Arm arm)
    {
        float currentDistance = Vector3.Distance(arm.upperArm.position, arm.handTarget.position);
        float totalLength = arm.originalUpperLength + arm.originalLowerLength;

        float stretchRatio = currentDistance / totalLength;

        if (stretchRatio > stretchStart)
        {
            float stretch = Mathf.Clamp(stretchRatio, 1f, maxStretch);

            Vector3 targetScale = new Vector3(1, stretch, 1);

            arm.upperArm.localScale = Vector3.Lerp(arm.upperArm.localScale, targetScale, Time.deltaTime * 10f);
            arm.lowerArm.localScale = Vector3.Lerp(arm.lowerArm.localScale, targetScale, Time.deltaTime * 10f);
        }
        else
        {
            arm.upperArm.localScale = Vector3.Lerp(arm.upperArm.localScale, arm.upperOriginalScale, Time.deltaTime * 10f);
            arm.lowerArm.localScale = Vector3.Lerp(arm.lowerArm.localScale, arm.lowerOriginalScale, Time.deltaTime * 10f);
        }
    }
}