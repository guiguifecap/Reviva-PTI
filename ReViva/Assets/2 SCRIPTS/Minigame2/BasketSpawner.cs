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
    [Tooltip("Marcado: o player pode pegar as esferas de longe com o raio. Desmarcado: só de perto.")]
    [SerializeField] private bool allowFarGrab = true;
    [Tooltip("Até essa distância (m) da mão, a esfera conta como 'perto' e sempre pode ser pega.")]
    [SerializeField] private float nearGrabDistance = 0.6f;
    [Tooltip("Distância máxima (m) para pegar de longe. 0 = sem limite.")]
    [SerializeField] private float maxFarGrabDistance = 15f;

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
            if (debugLogs) LogPress(it, over);

            if (over && inputFallback)
                StartCoroutine(CheckAfterPress(it));
        }
    }

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

        // 3. Regra de pegar de longe (configurável no Inspector)
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

// Regra de "pegar de longe". Não é MonoBehaviour: é criada em código para cada esfera.
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

        if (d <= nearGrabDistanceSafe(nearDistance)) return true;  // perto: sempre pode
        if (!allowFar) return false;                                // longe e desmarcado no Inspector
        return maxFar <= 0f || d <= maxFar;                         // longe: respeita o limite
    }

    private static float nearGrabDistanceSafe(float v) => v < 0f ? 0f : v;
}