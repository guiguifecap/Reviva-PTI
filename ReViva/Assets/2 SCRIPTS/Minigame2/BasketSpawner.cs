using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable))]
public class BasketSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab da esfera. Precisa ter Rigidbody + XRGrabInteractable.")]
    [SerializeField] private XRGrabInteractable spherePrefab;

    [Header("Spawn")]
    [SerializeField] private float spawnCooldown = 0.3f;
    [Tooltip("Segundos até destruir a bola. 0 = nunca destrói.")]
    [SerializeField] private float ballLifetime = 0f;

    [Header("Pegar de longe (esferas geradas)")]
    [Tooltip("Chave geral. Marcado: o player pode pegar as esferas de longe. Desmarcado: só de perto.")]
    [SerializeField] private bool allowFarGrab = true;
    [Tooltip("Faz a esfera voar até a mão quando o raio aponta para ela e você aperta o grip. " +
             "Desmarque se o Select do Ray Interactor já estiver funcionando sozinho.")]
    [SerializeField] private bool farGrabAssist = true;
    [Tooltip("Até essa distância (m) da mão, a esfera conta como 'perto' e é pega normalmente.")]
    [SerializeField] private float nearGrabDistance = 0.6f;
    [Tooltip("Distância máxima (m) para pegar de longe. 0 = sem limite.")]
    [SerializeField] private float maxFarGrabDistance = 15f;
    [Tooltip("Tempo (s) da esfera voando até a mão.")]
    [SerializeField] private float pullDuration = 0.15f;

    [Header("Diagnóstico")]
    [Tooltip("Se o botão for apertado sobre a cesta e o XRI não selecionar, gera a esfera mesmo assim.")]
    [SerializeField] private bool inputFallback = true;
    [SerializeField] private bool debugLogs = true;

    private XRSimpleInteractable basket;
    private Collider[] basketColliders;
    private float lastSpawnTime = -10f;
    private float nextRefreshTime;

    private readonly List<XRBaseInputInteractor> hovering = new List<XRBaseInputInteractor>();
    private readonly List<XRBaseInputInteractor> allInteractors = new List<XRBaseInputInteractor>();
    private readonly Dictionary<XRBaseInputInteractor, bool> wasPressed = new Dictionary<XRBaseInputInteractor, bool>();
    private readonly List<XRGrabInteractable> spawnedBalls = new List<XRGrabInteractable>();
    private readonly HashSet<XRGrabInteractable> pulling = new HashSet<XRGrabInteractable>();

    private void Awake()
    {
        basket = GetComponent<XRSimpleInteractable>();
        basketColliders = GetComponentsInChildren<Collider>();
    }

    private void OnEnable()
    {
        basket.hoverEntered.AddListener(OnHoverEntered);
        basket.hoverExited.AddListener(OnHoverExited);
        basket.selectEntered.AddListener(OnBasketSelected);
    }

    private void OnDisable()
    {
        basket.hoverEntered.RemoveListener(OnHoverEntered);
        basket.hoverExited.RemoveListener(OnHoverExited);
        basket.selectEntered.RemoveListener(OnBasketSelected);
        hovering.Clear();
        wasPressed.Clear();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        Log("HOVER na cesta por: " + PathOf(args.interactorObject.transform));

        var input = args.interactorObject as XRBaseInputInteractor;
        if (input != null && !hovering.Contains(input))
            hovering.Add(input);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        var input = args.interactorObject as XRBaseInputInteractor;
        if (input != null)
            hovering.Remove(input);
    }

    private void Update()
    {
        // Atualiza a lista de todos os interactors da cena a cada 2 segundos
        if (Time.time >= nextRefreshTime)
        {
            allInteractors.Clear();
            allInteractors.AddRange(FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None));
            nextRefreshTime = Time.time + 2f;
        }

        for (int i = 0; i < allInteractors.Count; i++)
        {
            var it = allInteractors[i];
            if (it == null || !it.isActiveAndEnabled) continue;

            bool pressed = it.selectInput.ReadIsPerformed();
            wasPressed.TryGetValue(it, out bool before);
            wasPressed[it] = pressed;

            // Só reage ao momento em que o botão é apertado
            if (!pressed || before) continue;

            bool over = IsOverBasket(it);
            if (debugLogs && over) LogPress(it, over);

            if (over)
            {
                if (inputFallback)
                    StartCoroutine(CheckAfterPress(it));
            }
            else if (allowFarGrab && farGrabAssist)
            {
                TryFarGrab(it);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Pegar de longe
    // ---------------------------------------------------------------------

    private void TryFarGrab(XRBaseInputInteractor it)
    {
        spawnedBalls.RemoveAll(b => b == null);
        if (spawnedBalls.Count == 0) return;
        if (it.hasSelection) return;

        XRRayInteractor ray = FindRayFor(it);
        if (ray == null)
        {
            Log("FAR GRAB: nenhum Ray Interactor encontrado perto de " + PathOf(it.transform));
            return;
        }

        if (!ray.TryGetCurrent3DRaycastHit(out RaycastHit hit) || hit.collider == null)
        {
            Log("FAR GRAB: o raio não está apontando para nada.");
            return;
        }

        XRGrabInteractable ball = hit.collider.GetComponentInParent<XRGrabInteractable>();
        if (ball == null || !spawnedBalls.Contains(ball))
        {
            Log("FAR GRAB: o raio aponta para '" + hit.collider.name + "', que não é uma esfera gerada pela cesta.");
            return;
        }

        if (ball.isSelected || pulling.Contains(ball)) return;

        float d = Vector3.Distance(it.transform.position, ball.transform.position);
        if (d <= nearGrabDistance) return; // perto: é pega normalmente

        if (maxFarGrabDistance > 0f && d > maxFarGrabDistance)
        {
            Log("FAR GRAB: esfera longe demais (" + d.ToString("F1") + " m).");
            return;
        }

        StartCoroutine(PullAndGrab(it, ball));
    }

    // Procura o Ray Interactor da mesma mão (sobe na hierarquia até achar um)
    private XRRayInteractor FindRayFor(XRBaseInputInteractor it)
    {
        if (it is XRRayInteractor self) return self;

        Transform t = it.transform;
        while (t != null)
        {
            var ray = t.GetComponentInChildren<XRRayInteractor>();
            if (ray != null) return ray;
            t = t.parent;
        }
        return null;
    }

    private IEnumerator PullAndGrab(XRBaseInputInteractor it, XRGrabInteractable ball)
    {
        pulling.Add(ball);

        IXRSelectInteractor si = it;
        IXRSelectInteractable sb = ball;

        Transform hand = si.GetAttachTransform(sb);
        if (hand == null) hand = it.transform;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        bool wasKinematic = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true; // sem gravidade/colisão enquanto voa

        Log("FAR GRAB: puxando a esfera para a mão.");

        Vector3 start = ball.transform.position;
        float t = 0f;
        float duration = Mathf.Max(0.01f, pullDuration);

        while (t < duration)
        {
            if (ball == null) { pulling.Remove(ball); yield break; }

            // Soltou o botão no meio do caminho: cancela
            if (it == null || !it.selectInput.ReadIsPerformed())
            {
                if (rb != null) rb.isKinematic = wasKinematic;
                pulling.Remove(ball);
                yield break;
            }

            t += Time.deltaTime;
            ball.transform.position = Vector3.Lerp(start, hand.position, Mathf.Clamp01(t / duration));
            yield return null;
        }

        if (ball == null) { pulling.Remove(ball); yield break; }

        ball.transform.position = hand.position;
        if (rb != null) rb.isKinematic = wasKinematic; // o XRI precisa ver o estado original

        var manager = basket.interactionManager;
        if (manager != null && !ball.isSelected && !it.hasSelection)
        {
            manager.SelectEnter(si, sb);
            Log("FAR GRAB: esfera entregue à mão.");
        }

        pulling.Remove(ball);
    }

    // ---------------------------------------------------------------------
    // Cesta
    // ---------------------------------------------------------------------

    // O interactor está apontando para a cesta, em hover nela ou com a mão dentro dela?
    private bool IsOverBasket(XRBaseInputInteractor it)
    {
        if (hovering.Contains(it)) return true;

        if (it is XRRayInteractor ray &&
            ray.TryGetCurrent3DRaycastHit(out RaycastHit hit) &&
            hit.collider != null &&
            hit.collider.transform.IsChildOf(transform))
            return true;

        Vector3 p = it.transform.position;
        for (int c = 0; c < basketColliders.Length; c++)
        {
            if (basketColliders[c] != null && basketColliders[c].bounds.Contains(p))
                return true;
        }
        return false;
    }

    private void LogPress(XRBaseInputInteractor it, bool over)
    {
        IXRSelectInteractor si = it;
        IXRSelectInteractable sb = basket;

        var hov = new StringBuilder();
        foreach (var h in it.interactablesHovered)
            hov.Append(h.transform.name).Append("; ");

        Debug.Log("[BasketSpawner/Diag] PRESS em '" + PathOf(it.transform) + "' (" + it.GetType().Name + ")\n" +
                  "  sobreACesta = " + over + " | em hover com: " + (hov.Length > 0 ? hov.ToString() : "nada") + "\n" +
                  "  hasSelection = " + it.hasSelection + " | isSelectActive = " + it.isSelectActive +
                  " | allowHover = " + it.allowHover + " | allowSelect = " + it.allowSelect + "\n" +
                  "  CanSelect(cesta) = " + si.CanSelect(sb) + " | cesta.IsSelectableBy = " + sb.IsSelectableBy(si) + "\n" +
                  "  layers interactor = " + it.interactionLayers.value + " | layers cesta = " + basket.interactionLayers.value,
                  this);
    }

    // Dá 1 frame para o XRI selecionar normalmente; se não selecionou, usa o fallback
    private IEnumerator CheckAfterPress(XRBaseInputInteractor it)
    {
        yield return null;

        if (it == null) yield break;
        if (Time.time - lastSpawnTime < spawnCooldown) yield break; // o XRI já cuidou

        IXRSelectInteractor si = it;
        IXRSelectInteractable sb = basket;
        if (si.IsSelecting(sb)) yield break;

        TrySpawn(it);
    }

    private void OnBasketSelected(SelectEnterEventArgs args)
    {
        Log("SELECT na cesta por: " + PathOf(args.interactorObject.transform));
        TrySpawn(args.interactorObject);
    }

    private void TrySpawn(IXRSelectInteractor interactor)
    {
        if (Time.time - lastSpawnTime < spawnCooldown) return;
        lastSpawnTime = Time.time;
        StartCoroutine(SpawnInHand(interactor));
    }

    private IEnumerator SpawnInHand(IXRSelectInteractor interactor)
    {
        yield return null;

        if (spherePrefab == null)
        {
            Debug.LogError("[BasketSpawner] Sphere Prefab não foi atribuído.", this);
            yield break;
        }

        var manager = basket.interactionManager;
        if (manager == null)
        {
            Debug.LogError("[BasketSpawner] Não há XR Interaction Manager na cena.", this);
            yield break;
        }

        IXRSelectInteractable selBasket = basket;

        // 1. Solta a cesta, se ela foi selecionada
        if (interactor.IsSelecting(selBasket))
            manager.SelectExit(interactor, selBasket);

        // 2. Instancia a esfera na posição da mão
        Transform hand = interactor.GetAttachTransform(spherePrefab);
        if (hand == null) hand = interactor.transform;

        XRGrabInteractable ball = Instantiate(spherePrefab, hand.position, hand.rotation);
        IXRSelectInteractable selBall = ball;
        spawnedBalls.Add(ball);

        // 3. Regra de pegar de longe para a seleção automática do XRI
        ball.selectFilters.Add(new FarGrabFilter
        {
            allowFar = allowFarGrab,
            nearDistance = nearGrabDistance,
            maxFar = maxFarGrabDistance
        });

        if (ballLifetime > 0f)
            Destroy(ball.gameObject, ballLifetime);

        if (!interactor.CanSelect(selBall))
            Debug.LogWarning("[BasketSpawner] A mão não pode selecionar a esfera normalmente " +
                             "(layers, filtros ou grupo). Vou forçar a seleção.", ball);

        // 4. Força a mão a pegar a esfera
        manager.SelectEnter(interactor, selBall);
        Log("SPAWN: esfera criada e entregue à mão: " + PathOf(interactor.transform));

        // 5. Confere se ela continua na mão
        yield return null;
        yield return null;
        if (ball != null && !interactor.IsSelecting(selBall))
            Log("A esfera saiu da mão logo depois de pegar (se você já soltou o botão, ignore).");
    }

    private static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log("[BasketSpawner] " + msg, this);
    }
}

// Regra de "pegar de longe" para a seleção automática do XRI.
// Não é MonoBehaviour: é criada em código para cada esfera.
public class FarGrabFilter : IXRSelectFilter
{
    public bool allowFar = true;
    public float nearDistance = 0.6f;
    public float maxFar = 15f;

    public bool canProcess => true;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        // Nunca derruba uma seleção que já existe
        if (interactor.IsSelecting(interactable)) return true;

        float d = Vector3.Distance(interactor.transform.position, interactable.transform.position);

        if (d <= Mathf.Max(0f, nearDistance)) return true;  // perto: sempre pode
        if (!allowFar) return false;                         // longe e desmarcado no Inspector
        return maxFar <= 0f || d <= maxFar;                  // longe: respeita o limite
    }
}