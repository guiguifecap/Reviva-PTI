using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drives the whole mini-game: spawns targets, keeps a set number active at once,
/// and tracks hits vs. the win goal.
///
/// - Nada spawna até StartTreatment(); StopTreatment() para de spawnar.
/// - Os alvos aparecem EXATAMENTE à distância configurada do ponto de referência (Empty),
///   num arco à frente dele (spawnArcAngle). Nunca atrás.
/// - areaCornerA/B agora só definem a ALTURA da água (Y). Sem ponto de referência,
///   usa a área retangular antiga (colocada à mão na cena).
/// </summary>
public class TargetSpawner : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private WaterTarget targetPrefab;
    [Tooltip("Define a altura da água (Y). Também usados como área retangular se não houver ponto de referência.")]
    [SerializeField] private Transform areaCornerA;
    [SerializeField] private Transform areaCornerB;

    [Header("Posição dos Alvos")]
    [Tooltip("Abertura total (graus) do arco à frente do ponto de referência onde os alvos podem aparecer. " +
             "90 = 45° para cada lado do forward.")]
    [SerializeField, Range(10f, 180f)] private float spawnArcAngle = 90f;

    [Header("Debug")]
    [Tooltip("Avisa no Console se a distância REAL do alvo ao ponto de referência for diferente da configurada.")]
    [SerializeField] private bool verificarDistancia = true;
    [Tooltip("Desenha o arco de spawn na Scene/Game view (Gizmos ligados).")]
    [SerializeField] private bool desenharArco = true;

    [Header("Win / Spawn Settings")]
    [Tooltip("How many successful hits are needed to win")]
    [SerializeField] private int hitsToWin = 10;
    [Tooltip("How many targets are up at the same time (general spawn count)")]
    [SerializeField] private int maxActiveTargets = 3;

    [Header("Timing")]
    [Tooltip("Random range for how high a target rises above the water")]
    [SerializeField] private Vector2 riseHeightRange = new Vector2(0.7f, 1.3f);
    [SerializeField] private float riseDuration = 0.4f;
    [SerializeField] private float sinkDuration = 0.4f;
    [Tooltip("Random range for how long a target stays up before sinking")]
    [SerializeField] private Vector2 stayDurationRange = new Vector2(1f, 3f);
    [Tooltip("Random delay before a new target spawns after one goes down")]
    [SerializeField] private Vector2 spawnDelayRange = new Vector2(0.2f, 1f);
    [Tooltip("Minimum distance between a new spawn point and any currently active target")]
    [SerializeField] private float minSpawnDistance = 1.5f;

    [Header("Events")]
    public UnityEvent onHitRegistered;
    public UnityEvent onWin;

    private readonly List<WaterTarget> pool = new List<WaterTarget>();
    private readonly List<WaterTarget> activeTargets = new List<WaterTarget>();
    private int hitsSoFar;
    private bool gameWon;

    // Controle de início/parada e de spawns agendados
    private bool started;
    private int pendingSpawns;

    // Distância configurada pelo menu
    private Transform spawnReference;
    private float spawnDistance = 5f;

    public int HitsSoFar => hitsSoFar;
    public int HitsToWin => hitsToWin;
    public int MaxActiveTargets => maxActiveTargets;
    /// <summary>True enquanto o tratamento está rodando (false após parar ou vencer).</summary>
    public bool HasStarted => started;

    private void Start()
    {
        hitsSoFar = 0;
        gameWon = false;
        // Não spawna nada aqui: espera StartTreatment().
    }

    // ---------------------------------------------------------------------
    // Iniciar / parar tratamento
    // ---------------------------------------------------------------------

    /// <summary>Botão "Iniciar Tratamento". Só a partir daqui os alvos aparecem.</summary>
    public void StartTreatment()
    {
        if (started) return;

        started = true;
        hitsSoFar = 0;
        gameWon = false;
        pendingSpawns = 0;

        FillSlots(new Vector2(0f, 0.5f));
    }

    /// <summary>Botão "Parar Tratamento". Para de spawnar; os alvos que já estão fora afundam sozinhos.</summary>
    public void StopTreatment()
    {
        started = false;
        StopSpawning();
    }

    private void StopSpawning()
    {
        StopAllCoroutines();
        pendingSpawns = 0;
    }

    // ---------------------------------------------------------------------
    // Configurações em tempo de execução (menu de dificuldade)
    // ---------------------------------------------------------------------

    public void SetHitsToWin(int value)
    {
        hitsToWin = Mathf.Max(1, value);

        // Se já estava rodando e o novo objetivo já foi atingido, vence agora
        if (started && !gameWon && hitsSoFar >= hitsToWin)
            Win();
    }

    public void SetMaxActiveTargets(int value)
    {
        maxActiveTargets = Mathf.Max(1, value);

        // Aumentou: preenche as vagas novas. Diminuiu: os alvos extras
        // simplesmente não são repostos quando afundarem.
        FillSlots(spawnDelayRange);
    }

    /// <summary>
    /// Os alvos passam a aparecer exatamente a 'distance' metros de 'reference',
    /// num arco à frente dele (direção forward, no plano horizontal).
    /// </summary>
    public void SetSpawnDistance(Transform reference, float distance)
    {
        spawnReference = reference;
        spawnDistance = Mathf.Max(0.1f, distance);
    }

    // ---------------------------------------------------------------------
    // Spawn dos alvos
    // ---------------------------------------------------------------------

    // Garante que (ativos + agendados) chegue ao máximo configurado
    private void FillSlots(Vector2 delayRange)
    {
        if (!started || gameWon) return;

        while (activeTargets.Count + pendingSpawns < maxActiveTargets)
        {
            pendingSpawns++;
            StartCoroutine(SpawnRoutine(Random.Range(delayRange.x, delayRange.y)));
        }
    }

    private IEnumerator SpawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        pendingSpawns--;

        if (!started || gameWon) yield break;
        if (activeTargets.Count >= maxActiveTargets) yield break;

        SpawnOne();
    }

    private void SpawnOne()
    {
        if (gameWon) return;

        WaterTarget target = GetFromPool();
        Vector3 spawnPos = GetRandomSpawnPoint();
        float stayDuration = Random.Range(stayDurationRange.x, stayDurationRange.y);
        float riseHeight = Random.Range(riseHeightRange.x, riseHeightRange.y);

        target.Init(this, spawnPos, riseHeight, riseDuration, sinkDuration, stayDuration);
        activeTargets.Add(target);

        VerificarDistanciaReal(target, spawnPos);
    }

    // Compara a distância pedida com a posição REAL do alvo na cena
    private void VerificarDistanciaReal(WaterTarget target, Vector3 spawnPos)
    {
        if (!verificarDistancia || spawnReference == null) return;

        Vector3 delta = target.transform.position - spawnReference.position;
        delta.y = 0f;
        float real = delta.magnitude;

        if (Mathf.Abs(real - spawnDistance) > 0.05f)
        {
            Debug.LogWarning($"[TargetSpawner] Distância pedida {spawnDistance:F2} m, mas o alvo ficou a {real:F2} m do ponto de referência. " +
                             $"Escala do spawner: {transform.lossyScale}, escala do ponto de referência: {spawnReference.lossyScale}. " +
                             $"Posição calculada: {spawnPos}, posição real do alvo: {target.transform.position}.");
        }
    }

    private void OnDrawGizmos()
    {
        if (!desenharArco || spawnReference == null) return;

        Vector3 origin = spawnReference.position;
        Vector3 forward = Vector3.ProjectOnPlane(spawnReference.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        float y = areaCornerA != null ? areaCornerA.position.y : origin.y;
        float half = spawnArcAngle * 0.5f;
        const int steps = 24;

        Gizmos.color = Color.cyan;
        Vector3 prev = Vector3.zero;
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.Lerp(-half, half, i / (float)steps);
            Vector3 p = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * spawnDistance;
            p.y = y;
            if (i > 0) Gizmos.DrawLine(prev, p);
            prev = p;
        }

        Gizmos.DrawLine(origin, origin + Quaternion.AngleAxis(-half, Vector3.up) * forward * spawnDistance);
        Gizmos.DrawLine(origin, origin + Quaternion.AngleAxis(half, Vector3.up) * forward * spawnDistance);
    }

    private Vector3 GetRandomSpawnPoint()
    {
        const int maxAttempts = 30;
        Vector3 candidate = RandomCandidate();

        for (int i = 0; i < maxAttempts; i++)
        {
            candidate = RandomCandidate();
            if (IsFarEnoughFromActiveTargets(candidate))
            {
                return candidate;
            }
        }

        // Couldn't find a fully clear spot in time (arc too crowded/small) -
        // just use the last attempt so spawning never stalls.
        return candidate;
    }

    private Vector3 RandomCandidate()
    {
        return spawnReference != null ? RandomPointOnArc() : RandomPointInBounds();
    }

    // Ponto exatamente a spawnDistance do ponto de referência, dentro do arco à frente dele
    private Vector3 RandomPointOnArc()
    {
        Vector3 origin = spawnReference.position;

        Vector3 forward = Vector3.ProjectOnPlane(spawnReference.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        float half = spawnArcAngle * 0.5f;
        float angle = Random.Range(-half, half);
        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;

        Vector3 p = origin + dir * spawnDistance;
        p.y = areaCornerA != null ? areaCornerA.position.y : origin.y; // nível da água
        return p;
    }

    // Fallback: área retangular antiga (sem ponto de referência)
    private Vector3 RandomPointInBounds()
    {
        float x = Random.Range(areaCornerA.position.x, areaCornerB.position.x);
        float z = Random.Range(areaCornerA.position.z, areaCornerB.position.z);
        float y = areaCornerA.position.y; // assumes both corners sit at water level
        return new Vector3(x, y, z);
    }

    private bool IsFarEnoughFromActiveTargets(Vector3 candidate)
    {
        foreach (WaterTarget t in activeTargets)
        {
            if (Vector3.Distance(candidate, t.SpawnPosition) < minSpawnDistance)
            {
                return false;
            }
        }
        return true;
    }

    private WaterTarget GetFromPool()
    {
        if (pool.Count > 0)
        {
            WaterTarget t = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return t;
        }
        return Instantiate(targetPrefab, transform);
    }

    /// <summary>Called by WaterTarget once it fully sinks (hit or not).</summary>
    public void ReturnTarget(WaterTarget target)
    {
        activeTargets.Remove(target);
        target.gameObject.SetActive(false);
        pool.Add(target);

        FillSlots(spawnDelayRange);
    }

    /// <summary>Called by WaterTarget.Hit().</summary>
    public void RegisterHit()
    {
        if (gameWon) return;

        hitsSoFar++;
        onHitRegistered?.Invoke();

        if (hitsSoFar >= hitsToWin)
            Win();
    }

    private void Win()
    {
        gameWon = true;
        started = false;   // o botão volta para "Iniciar Tratamento"
        StopSpawning();
        onWin?.Invoke();
    }
}