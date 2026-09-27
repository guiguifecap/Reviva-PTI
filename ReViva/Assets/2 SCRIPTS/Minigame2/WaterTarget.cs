using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to your target prefab. Handles the rise -> stay -> sink cycle.
/// The spawner calls Init() to activate it, and this script tells the
/// spawner when it's done (via ReturnTarget) so a new one can spawn.
/// </summary>
public class WaterTarget : MonoBehaviour
{
    [Tooltip("Fires when this target gets hit")]
    public UnityEvent onHit;

    [Header("Hit Reaction")]
    [Tooltip("How long the pop + shrink animation takes")]
    [SerializeField] private float hitAnimDuration = 0.35f;
    [Tooltip("Optional - a particle system to spawn at the hit point (leave empty to skip)")]
    [SerializeField] private ParticleSystem hitEffectPrefab;

    private enum State { Rising, Staying, Sinking }

    private TargetSpawner spawner;
    private State state;
    private float stateTimer;
    private bool isHit;

    private Vector3 sunkenPos;
    private Vector3 risenPos;
    private float riseDuration;
    private float sinkDuration;
    private float stayDuration;

    /// <summary>
    /// Called by TargetSpawner right after grabbing this target from the pool.
    /// </summary>
    public void Init(TargetSpawner spawner, Vector3 spawnPos, float riseHeight,
                      float riseDuration, float sinkDuration, float stayDuration)
    {
        this.spawner = spawner;
        this.riseDuration = Mathf.Max(0.01f, riseDuration);
        this.sinkDuration = Mathf.Max(0.01f, sinkDuration);
        this.stayDuration = stayDuration;

        sunkenPos = spawnPos;
        risenPos = spawnPos + Vector3.up * riseHeight;

        transform.position = sunkenPos;
        state = State.Rising;
        stateTimer = 0f;
        isHit = false;

        gameObject.SetActive(true);
    }

    public Vector3 SpawnPosition => sunkenPos;

    private void Update()
    {
        if (isHit) return; // hit reaction coroutine is driving position/scale now

        stateTimer += Time.deltaTime;

        switch (state)
        {
            case State.Rising:
                float tRise = Mathf.Clamp01(stateTimer / riseDuration);
                transform.position = Vector3.Lerp(sunkenPos, risenPos, tRise);
                if (tRise >= 1f)
                {
                    state = State.Staying;
                    stateTimer = 0f;
                }
                break;

            case State.Staying:
                if (stateTimer >= stayDuration)
                {
                    state = State.Sinking;
                    stateTimer = 0f;
                }
                break;

            case State.Sinking:
                float tSink = Mathf.Clamp01(stateTimer / sinkDuration);
                transform.position = Vector3.Lerp(risenPos, sunkenPos, tSink);
                if (tSink >= 1f)
                {
                    Despawn();
                }
                break;
        }
    }

    /// <summary>
    /// Call this from whatever detects a hit (raycast, VR controller collider,
    /// bullet trigger, etc). Safe to call multiple times - only counts once.
    /// </summary>
    public void Hit()
    {
        if (isHit) return;
        isHit = true;

        onHit?.Invoke();
        spawner.RegisterHit();
        StartCoroutine(HitReaction());
    }

    // Lets you test by just clicking the target in the editor/desktop build.
    // Harmless to leave in even for VR - just won't fire from a headset.
    private void OnMouseDown()
    {
        Hit();
    }

    /// <summary>
    /// Little punchy pop-up, then spins and shrinks away to nothing.
    /// Purely code-driven so it works with zero extra art/particles - assign
    /// hitEffectPrefab if you want a particle burst layered on top later.
    /// </summary>
    private System.Collections.IEnumerator HitReaction()
    {
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        Vector3 startPos = transform.position;

        if (hitEffectPrefab != null)
        {
            ParticleSystem fx = Instantiate(hitEffectPrefab, startPos, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax);
        }

        Vector3 popScale = startScale * 1.3f;
        Vector3 popPos = startPos + Vector3.up * 0.3f;

        float popTime = hitAnimDuration * 0.35f;
        float shrinkTime = hitAnimDuration - popTime;

        // Quick punchy pop - scales up and lifts slightly
        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float p = t / popTime;
            transform.localScale = Vector3.Lerp(startScale, popScale, p);
            transform.position = Vector3.Lerp(startPos, popPos, p);
            yield return null;
        }

        // Spins while shrinking to nothing - the "disappear with style" part
        t = 0f;
        while (t < shrinkTime)
        {
            t += Time.deltaTime;
            float p = t / shrinkTime;
            transform.localScale = Vector3.Lerp(popScale, Vector3.zero, p);
            transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
            yield return null;
        }

        // Reset so this instance looks normal next time it's pulled from the pool
        transform.localScale = startScale;
        transform.rotation = startRotation;

        Despawn();
    }

    private void Despawn()
    {
        spawner.ReturnTarget(this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Destroy(collision.gameObject);
    }
}