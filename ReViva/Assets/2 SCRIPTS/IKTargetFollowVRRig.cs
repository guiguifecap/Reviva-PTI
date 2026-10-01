using UnityEngine;

[System.Serializable]
public class VRMap
{
    public Transform vrTarget;
    public Transform ikTarget;
    public Vector3 trackingPositionOffset;
    public Vector3 trackingRotationOffset;
    public void Map()
    {
        ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
        ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
    }
}

public class IKTargetFollowVRRig : MonoBehaviour
{
    [Range(0,1)]
    public float turnSmoothness = 0.1f;
    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    public Vector3 headBodyPositionOffset;
    public float headBodyYawOffset;

    [Header("Colisão da mão (evita atravessar a montanha)")]
    public bool preventHandClipping = true;
    [Tooltip("Raio aproximado da mão, usado no SphereCast.")]
    public float handRadius = 0.045f;
    [Tooltip("Pequeno afastamento da superfície para a mão não ficar encravada na parede.")]
    public float surfaceSkin = 0.01f;
    [Tooltip("Se o alvo pular mais que isso em um frame (teleporte, reset de posição), a colisão é ignorada e a mão apenas acompanha.")]
    public float teleportThreshold = 1.5f;
    // Por padrão colide com tudo, exceto as camadas de mão/jogador/UI/itens agarráveis,
    // pra não travar a mão em si mesma ou estragar o sistema de pegar/menu.
    public LayerMask collisionMask = ~LayerMask.GetMask("Mão", "Player", "UI", "MenuPC", "interectable ", "climable", "TelaCalibragem ");

    Vector3 leftSafePosition;
    Vector3 rightSafePosition;
    bool leftInitialized;
    bool rightInitialized;

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = head.ikTarget.position + headBodyPositionOffset;
        float yaw = head.vrTarget.eulerAngles.y;
        transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.Euler(transform.eulerAngles.x, yaw, transform.eulerAngles.z),turnSmoothness);

        head.Map();
        MapHand(leftHand, ref leftSafePosition, ref leftInitialized);
        MapHand(rightHand, ref rightSafePosition, ref rightInitialized);
    }

    void MapHand(VRMap hand, ref Vector3 safePosition, ref bool initialized)
    {
        Vector3 desiredPosition = hand.vrTarget.TransformPoint(hand.trackingPositionOffset);
        Quaternion desiredRotation = hand.vrTarget.rotation * Quaternion.Euler(hand.trackingRotationOffset);

        if (!preventHandClipping)
        {
            hand.ikTarget.position = desiredPosition;
            hand.ikTarget.rotation = desiredRotation;
            return;
        }

        if (!initialized)
        {
            safePosition = desiredPosition;
            initialized = true;
        }

        Vector3 delta = desiredPosition - safePosition;
        float distance = delta.magnitude;

        if (distance > teleportThreshold)
        {
            // Pulo grande demais pra ser deslocamento real da mão (reset/teleporte do jogador): não trava, apenas acompanha.
            safePosition = desiredPosition;
        }
        else if (distance > 0.0001f)
        {
            if (Physics.SphereCast(safePosition, handRadius, delta / distance, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                safePosition = hit.point + hit.normal * (handRadius + surfaceSkin);
            }
            else
            {
                safePosition = desiredPosition;
            }
        }

        hand.ikTarget.position = safePosition;
        hand.ikTarget.rotation = desiredRotation;
    }
}
