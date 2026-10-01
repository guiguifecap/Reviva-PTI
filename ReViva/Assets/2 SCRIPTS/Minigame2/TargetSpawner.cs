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

    public int HitsSoFar => hitsSoFar;
    public int HitsToWin => hitsToWin;

    private void Start()
    {
        hitsSoFar = 0;
        gameWon = false;

        for (int i = 0; i < maxActiveTargets; i++)
        {
            StartCoroutine(SpawnRoutine(Random.Range(0f, 0.5f)));
        }
    }

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
    [Header("Distance From Player")]
    [Tooltip("O empty PAI que contém as duas quinas (areaCornerA e areaCornerB)")]
    [SerializeField] private Transform areaRoot;

    private Vector3 areaDirection = Vector3.forward; // direção player -> área
    private bool directionCached;

    /// <summary>
    /// Move a área de spawn para ficar a 'distance' metros do player,
    /// mantendo a direção original e a altura (nível da água).
    /// </summary>
    public void SetSpawnDistance(Vector3 playerPosition, float distance)
    {
        if (areaRoot == null) return;

        // guarda a direção original na primeira chamada
        if (!directionCached)
        {
            Vector3 dir = areaRoot.position - playerPosition;
            dir.y = 0f;
            areaDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            directionCached = true;
        }

        Vector3 pos = playerPosition + areaDirection * distance;
        pos.y = areaRoot.position.y; // mantém a altura da água
        areaRoot.position = pos;
    }
}