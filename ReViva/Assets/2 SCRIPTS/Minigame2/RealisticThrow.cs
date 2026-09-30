using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Arremesso consistente para VR. Substitui o arremesso padrão do XRGrabInteractable:
/// mede o movimento da mão no espaço do XR Origin, escolhe a velocidade e a direção
/// do arremesso com regras previsíveis e aplica a velocidade no Rigidbody.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class RealisticThrow : MonoBehaviour
{
    [Header("Medição do movimento da mão")]
    [Tooltip("Janela (s) de movimento antes da soltura usada para calcular o arremesso.")]
    [SerializeField] private float sampleWindow = 0.15f;

    [Header("Velocidade do arremesso")]
    [Tooltip("Abaixo desta velocidade da mão (m/s) a bola só é solta, sem arremesso.")]
    [SerializeField] private float dropSpeed = 0.6f;
    [Tooltip("Acima desta velocidade da mão (m/s) o movimento conta como arremesso de verdade.")]
    [SerializeField] private float deliberateSpeed = 2f;
    [Tooltip("Multiplicador da velocidade da mão em arremessos deliberados.")]
    [SerializeField] private float throwMultiplier = 1.5f;
    [Tooltip("Velocidade mínima (m/s) de um arremesso deliberado: evita a bola cair na frente do player.")]
    [SerializeField] private float minThrowSpeed = 5f;
    [Tooltip("Velocidade máxima (m/s): evita o efeito de míssil.")]
    [SerializeField] private float maxThrowSpeed = 14f;

    [Header("Direção")]
    [Tooltip("Corrige a direção lateral do arremesso usando a direção do olhar.")]
    [SerializeField] private bool correctAim = true;
    [Tooltip("O quanto o erro lateral é corrigido (0 = nada, 1 = sempre na direção do olhar).")]
    [Range(0f, 1f)][SerializeField] private float aimBlend = 0.5f;
    [Tooltip("Erro lateral máximo (graus) permitido em relação ao olhar em arremessos deliberados.")]
    [SerializeField] private float maxAimError = 25f;
    [Tooltip("Inclinação extra para cima (graus): arremessos naturais saem levemente para cima.")]
    [SerializeField] private float upwardBias = 6f;

    [Header("Ajuda de mira nos alvos (WaterTarget)")]
    [SerializeField] private bool aimAssist = true;
    [Tooltip("Quanto a direção é puxada para o acerto (0 = nada, 1 = acerta sempre).")]
    [Range(0f, 1f)][SerializeField] private float assistStrength = 0.5f;
    [Tooltip("Só ajuda se o arremesso já estiver a esta distância angular (graus) do acerto.")]
    [SerializeField] private float assistCone = 15f;
    [SerializeField] private float assistMaxDistance = 25f;
    [Tooltip("Altura mínima (m) acima da água para o alvo contar como alvo.")]
    [SerializeField] private float minTargetHeight = 0.15f;

    [Header("Física da bola solta")]
    [Tooltip("1 = gravidade normal. Menor que 1 deixa o arco mais aberto e perdoador.")]
    [SerializeField] private float gravityScale = 0.85f;
    [Tooltip("Quanto da velocidade do player (andando) é somada ao arremesso.")]
    [Range(0f, 1f)][SerializeField] private float inheritPlayerVelocity = 0f;

    [Header("Diagnóstico")]
    [SerializeField] private bool debugLogs = true;

    private struct Sample
    {
        public float time;
        public Vector3 localPos;
    }

    private Rigidbody rb;
    private XRGrabInteractable grab;

    private IXRSelectInteractor holder;
    private Transform attach;
    private XROrigin origin;
    private Transform trackingSpace;
    private readonly List<Sample> samples = new List<Sample>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        // O arremesso passa a ser feito por este script
        grab.throwOnDetach = false;
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    // ---------------------------------------------------------------------
    // Segurar e soltar
    // ---------------------------------------------------------------------

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        attach = holder.GetAttachTransform(grab);
        if (attach == null) attach = holder.transform;

        origin = holder.transform.GetComponentInParent<XROrigin>();
        trackingSpace = origin != null ? origin.transform : holder.transform.root;

        samples.Clear();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (args.isCanceled) return;
        if (holder == null || args.interactorObject != holder) return;

        Vector3 throwVelocity = BuildThrowVelocity(rb.position);

        StopAllCoroutines();
        StartCoroutine(ApplyThrow(throwVelocity));
    }

    // Grava a posição da mão (no espaço do XR Origin) a cada frame enquanto segura
    private void LateUpdate()
    {
        if (holder == null || attach == null || trackingSpace == null || !grab.isSelected) return;

        float now = Time.time;
        samples.Add(new Sample
        {
            time = now,
            localPos = trackingSpace.InverseTransformPoint(attach.position)
        });

        while (samples.Count > 0 && now - samples[0].time > sampleWindow * 2f)
            samples.RemoveAt(0);
    }

    private void FixedUpdate()
    {
        if (Mathf.Approximately(gravityScale, 1f)) return;
        if (grab.isSelected || rb.isKinematic || !rb.useGravity) return;

        // Gravidade extra (ou reduzida) só com a bola solta
        rb.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);
    }

    // O XRI termina o "soltar" nos primeiros FixedUpdates. Esperamos para que
    // a velocidade calculada aqui seja sempre a última a ser aplicada.
    private IEnumerator ApplyThrow(Vector3 velocity)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        int tries = 0;
        while (rb.isKinematic && tries < 6)
        {
            yield return new WaitForFixedUpdate();
            tries++;
        }

        if (grab.isSelected || rb.isKinematic) yield break; // pegou de novo

        SetVelocity(velocity);
        SetAngularVelocity(Vector3.zero);
    }

    // ---------------------------------------------------------------------
    // Cálculo do arremesso
    // ---------------------------------------------------------------------

    private Vector3 BuildThrowVelocity(Vector3 startPos)
    {
        if (!EstimateHandMotion(out Vector3 dir, out float handSpeed))
        {
            Log("Sem movimento da mão: a bola só cai.");
            return Vector3.zero;
        }

        // 0 = só soltou, 1 = arremesso deliberado (transição suave)
        float factor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dropSpeed, deliberateSpeed, handSpeed));

        // Velocidade
        float outSpeed = handSpeed * Mathf.Lerp(1f, throwMultiplier, factor);
        outSpeed = Mathf.Max(outSpeed, minThrowSpeed * factor);
        outSpeed = Mathf.Min(outSpeed, maxThrowSpeed);

        // Direção
        Transform head = GetHead();
        float yawBefore = head != null ? YawOffset(dir, head) : 0f;

        if (correctAim && head != null)
            dir = CorrectYaw(dir, head, factor);

        float yawAfter = head != null ? YawOffset(dir, head) : 0f;

        if (upwardBias > 0f && factor > 0f)
            dir = ApplyUpwardBias(dir, upwardBias * factor);

        string assistName = null;
        if (aimAssist && factor > 0.5f)
            dir = ApplyAimAssist(startPos, dir, outSpeed, out assistName);

        Vector3 velocity = dir * outSpeed;

        if (inheritPlayerVelocity > 0f && origin != null)
        {
            CharacterController cc = origin.GetComponent<CharacterController>();
            if (cc != null) velocity += cc.velocity * inheritPlayerVelocity;
        }

        Log("mão " + handSpeed.ToString("F1") + " m/s -> bola " + outSpeed.ToString("F1") + " m/s" +
            " | erro lateral " + yawBefore.ToString("F0") + "° -> " + yawAfter.ToString("F0") + "°" +
            " | ajuda de mira: " + (assistName != null ? assistName : "nenhuma"));

        return velocity;
    }

    // Velocidade e direção da mão, pesando mais as amostras rápidas e as mais recentes.
    private bool EstimateHandMotion(out Vector3 dirWorld, out float speed)
    {
        dirWorld = Vector3.zero;
        speed = 0f;
        if (trackingSpace == null || samples.Count < 2) return false;

        float now = Time.time;
        Vector3 sumVec = Vector3.zero;
        float sumSpeed = 0f;
        float sumW = 0f;

        for (int i = 1; i < samples.Count; i++)
        {
            float age = now - samples[i].time;
            if (age > sampleWindow) continue;

            float dt = samples[i].time - samples[i - 1].time;
            if (dt < 0.0001f) continue;

            Vector3 vLocal = (samples[i].localPos - samples[i - 1].localPos) / dt;
            Vector3 vWorld = trackingSpace.TransformVector(vLocal);
            float s = vWorld.magnitude;

            float recency = 1f - Mathf.Clamp01(age / sampleWindow);
            float w = s * s * (0.25f + 0.75f * recency);

            sumVec += vWorld * w;
            sumSpeed += s * w;
            sumW += w;
        }

        if (sumW < 0.000001f) return false;

        Vector3 avg = sumVec / sumW;
        if (avg.sqrMagnitude < 0.000001f) return false;

        speed = sumSpeed / sumW;
        dirWorld = avg.normalized;
        return true;
    }

    // Reduz o erro lateral em relação ao olhar e limita a um cone (só gira no plano horizontal)
    private Vector3 CorrectYaw(Vector3 dir, Transform head, float factor)
    {
        Vector3 headFwd = head.forward;
        headFwd.y = 0f;
        Vector3 flat = dir;
        flat.y = 0f;
        if (headFwd.sqrMagnitude < 0.001f || flat.sqrMagnitude < 0.001f) return dir;

        float offset = Vector3.SignedAngle(headFwd, flat, Vector3.up);
        float newOffset = offset * (1f - aimBlend * factor);

        float cone = Mathf.Lerp(180f, maxAimError, factor);
        newOffset = Mathf.Clamp(newOffset, -cone, cone);

        return Quaternion.AngleAxis(newOffset - offset, Vector3.up) * dir;
    }

    private float YawOffset(Vector3 dir, Transform head)
    {
        Vector3 headFwd = head.forward;
        headFwd.y = 0f;
        Vector3 flat = dir;
        flat.y = 0f;
        if (headFwd.sqrMagnitude < 0.001f || flat.sqrMagnitude < 0.001f) return 0f;
        return Vector3.SignedAngle(headFwd, flat, Vector3.up);
    }

    private Vector3 ApplyUpwardBias(Vector3 dir, float degrees)
    {
        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 0.0001f) return dir; // quase vertical
        return Quaternion.AngleAxis(-degrees, right.normalized) * dir;
    }

    // Procura o alvo mais alinhado com o arremesso e inclina a direção para o acerto
    private Vector3 ApplyAimAssist(Vector3 start, Vector3 dir, float speed, out string targetName)
    {
        targetName = null;

        float g = Physics.gravity.magnitude * gravityScale;
        float bestAngle = assistCone;
        Vector3 bestDir = dir;

        WaterTarget[] targets = FindObjectsByType<WaterTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < targets.Length; i++)
        {
            WaterTarget t = targets[i];
            if (t == null || !t.isActiveAndEnabled) continue;
            if (t.transform.position.y < t.SpawnPosition.y + minTargetHeight) continue;

            Collider col = t.GetComponentInChildren<Collider>();
            if (col == null || !col.enabled) continue;

            Vector3 aimPoint = col.bounds.center;
            if (Vector3.Distance(start, aimPoint) > assistMaxDistance) continue;
            if (!SolveLaunchDirection(start, aimPoint, speed, g, out Vector3 ideal)) continue;

            float angle = Vector3.Angle(dir, ideal);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestDir = ideal;
                targetName = t.name;
            }
        }

        if (targetName == null) return dir;
        return Vector3.Slerp(dir, bestDir, assistStrength).normalized;
    }

    // Direção de lançamento (arco baixo) para atingir "to" saindo de "from" com uma velocidade fixa
    private static bool SolveLaunchDirection(Vector3 from, Vector3 to, float speed, float gravity, out Vector3 dir)
    {
        dir = Vector3.zero;

        Vector3 delta = to - from;
        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        float d = flat.magnitude;
        float h = delta.y;
        if (d < 0.1f || gravity < 0.01f) return false;

        float v2 = speed * speed;
        float disc = v2 * v2 - gravity * (gravity * d * d + 2f * h * v2);
        if (disc < 0f) return false; // fora de alcance com essa velocidade

        float angle = Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (gravity * d));
        Vector3 flatDir = flat / d;
        dir = (flatDir * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)).normalized;
        return true;
    }

    // ---------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------

    private Transform GetHead()
    {
        if (origin != null && origin.Camera != null) return origin.Camera.transform;
        return Camera.main != null ? Camera.main.transform : null;
    }

    private void SetVelocity(Vector3 value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
        rb.velocity = value;
#endif
    }

    private void SetAngularVelocity(Vector3 value)
    {
        rb.angularVelocity = value;
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log("[RealisticThrow] " + msg, this);
    }
}