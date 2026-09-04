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

        [Tooltip(
            "Velocidade máxima (m/s) que a mão física pode atingir ao perseguir o controle. " +
            "Evita picos de velocidade absurdos quando a mão real se move muito rápido, o que " +
            "também ajuda a colisão contínua a funcionar de forma estável."
        )]
        public float maxSpeed = 8f;

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

        // ─────────────────────────────────────────────
        // CORREÇÃO: mão atravessando a montanha ("tunneling")
        //
        // Por padrão o Rigidbody usa Collision Detection = Discrete, que só
        // verifica colisão no ponto final de cada passo de física — não no
        // trajeto percorrido. Como a mão física se move rápido (segue o
        // controle do VR por velocidade), um movimento rápido da mão real
        // pode fazer o Rigidbody "pular" para dentro/atrás do collider da
        // montanha num único FixedUpdate, sem nunca detectar a colisão no
        // meio do caminho. ContinuousDynamic faz o motor de física testar
        // a trajetória inteira do Rigidbody, evitando que ele atravesse a
        // rocha. É forçado aqui por código para não depender de ninguém
        // lembrar de configurar isso certo no Inspector.
        // ─────────────────────────────────────────────
        if (arm.handRb != null)
        {
            arm.handRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    // 👉 PHYSICS FOLLOW POR MÃO
    void UpdatePhysicsHand(Arm arm)
    {
        if (arm.handRb == null || arm.handTarget == null) return;

        Vector3 dir = arm.handTarget.position - arm.handRb.position;
        Vector3 velocidadeDesejada = dir * arm.followSpeed;

        // Limita a velocidade máxima: além de evitar espasmos físicos quando
        // a mão real se move muito rápido, mantém a colisão contínua estável
        // (velocidades absurdamente altas ainda podem causar instabilidade
        // mesmo com ContinuousDynamic).
        arm.handRb.linearVelocity = Vector3.ClampMagnitude(velocidadeDesejada, arm.maxSpeed);
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