using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drives the whole mini-game: spawns targets at random points in a defined
/// area, keeps a set number active at once, and tracks hits vs. the win goal.
///
/// Setup:
/// 1. Make a target prefab with WaterTarget.cs + a collider on it.
/// 2. Create two empty GameObjects marking opposite corners of your spawn
///    area (same Y height = water surface) and assign them below.
/// 3. Assign the prefab, tweak the numbers, hit play.
///
/// The area is placed by hand in the scene. The difficulty menu can then move it
/// closer to / farther from the player via SetSpawnOffset (0 = original position).
/// </summary>
public class TargetSpawner : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private WaterTarget targetPrefab;
    [SerializeField] private Transform areaCornerA;
    [SerializeField] private Transform areaCornerB;

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

    // Posição original da área (colocada à mão na cena) e referência do player
    private Vector3 originalCornerA;
    private Vector3 originalCornerB;
    private bool cornersCached;

    private Vector3 areaDirection = Vector3.forward; // direção player -> centro da área
    private float originalDistance;                  // distância horizontal player -> centro da área
    private bool referenceCached;

    public int HitsSoFar => hitsSoFar;
    public int HitsToWin => hitsToWin;

    private void Awake()
    {
        CacheOriginalCorners();
    }

    private void Start()
    {
        hitsSoFar = 0;
        gameWon = false;

        for (int i = 0; i < maxActiveTargets; i++)
        {
            StartCoroutine(SpawnRoutine(Random.Range(0f, 0.5f)));
        }
    }

    // ---------------------------------------------------------------------
    // Distância da área em relação ao player (usado pelo menu de dificuldade)
    // ---------------------------------------------------------------------

    private void CacheOriginalCorners()
    {
        if (cornersCached || areaCornerA == null || areaCornerB == null) return;

        originalCornerA = areaCornerA.position;
        originalCornerB = areaCornerB.position;
        cornersCached = true;
    }

    // Guarda, uma única vez, a direção e a distância originais entre o player e a área
    private void CacheReference(Vector3 playerPosition)
    {
        if (referenceCached) return;

        Vector3 center = (originalCornerA + originalCornerB) * 0.5f;
        Vector3 toArea = center - playerPosition;
        toArea.y = 0f;

        originalDistance = toArea.magnitude;
        areaDirection = originalDistance > 0.001f ? toArea / originalDistance : Vector3.forward;
        referenceCached = true;
    }

    /// <summary>
    /// Desloca a área de spawn em relação à posição ORIGINAL (a que foi colocada na cena).
    /// offset = 0  -> posição original
    /// offset > 0  -> mais longe do player
    /// offset < 0  -> mais perto do player (nunca abaixo de minDistanceFromPlayer)
    /// </summary>
    public void SetSpawnOffset(Vector3 playerPosition, float offset, float minDistanceFromPlayer = 1f)
    {
        CacheOriginalCorners();
        if (!cornersCached) return;
        CacheReference(playerPosition);

        // Só limita ao aproximar; o offset 0 sempre mantém a posição original
        float limit = Mathf.Min(0f, minDistanceFromPlayer - originalDistance);
        float finalOffset = Mathf.Max(offset, limit);

        Vector3 delta = areaDirection * finalOffset; // y = 0: a altura da água não muda
        areaCornerA.position = originalCornerA + delta;
        areaCornerB.position = originalCornerB + delta;
    }

    /// <summary>
    /// Coloca o centro da área a 'distance' metros do player (distância absoluta),
    /// mantendo a direção e a altura originais.
    /// </summary>
    public void SetSpawnDistance(Vector3 playerPosition, float distance)
    {
        CacheOriginalCorners();
        if (!cornersCached) return;
        CacheReference(playerPosition);

        SetSpawnOffset(playerPosition, distance - originalDistance, 0.5f);
    }

    // ---------------------------------------------------------------------
    // Spawn dos alvos
    // ---------------------------------------------------------------------

    private IEnumerator SpawnRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnOne();
    }

    private void SpawnOne()
    {
        if (gameWon) return;

        WaterTarget target = GetFromPool();
        Vector3 spawnPos = GetRandomPointInArea();
        float stayDuration = Random.Range(stayDurationRange.x, stayDurationRange.y);
        float riseHeight = Random.Range(riseHeightRange.x, riseHeightRange.y);

        target.Init(this, spawnPos, riseHeight, riseDuration, sinkDuration, stayDuration);
        activeTargets.Add(target);
    }

    private Vector3 GetRandomPointInArea()
    {
        const int maxAttempts = 30;
        Vector3 candidate = RandomPointInBounds();

        for (int i = 0; i < maxAttempts; i++)
        {
            candidate = RandomPointInBounds();
            if (IsFarEnoughFromActiveTargets(candidate))
            {
                return candidate;
            }
        }

        // Couldn't find a fully clear spot in time (area too crowded/small) -
        // just use the last attempt so spawning never stalls.
        return candidate;
    }

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

        if (!gameWon)
        {
            StartCoroutine(SpawnRoutine(Random.Range(spawnDelayRange.x, spawnDelayRange.y)));
        }
    }

    /// <summary>Called by WaterTarget.Hit().</summary>
    public void RegisterHit()
    {
        if (gameWon) return;

        hitsSoFar++;
        onHitRegistered?.Invoke();

        if (hitsSoFar >= hitsToWin)
        {
            gameWon = true;
            onWin?.Invoke();
        }
    }
}