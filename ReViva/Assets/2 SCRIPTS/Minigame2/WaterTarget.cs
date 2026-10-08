using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to your target prefab. Handles the rise -> stay -> sink cycle.
/// The spawner calls Init() to activate it, and this script tells the
/// spawner when it's done (via ReturnTarget) so a new one can spawn.
/// Breaks when hit by any object tagged with ballTag ("Bola").
/// Shows a floating "+1" when hit.
/// </summary>
public class WaterTarget : MonoBehaviour
{
    [Tooltip("Fires when this target gets hit")]
    public UnityEvent onHit;

    [Header("Ball Detection")]
    [Tooltip("Tag of the objects that break this target")]
    [SerializeField] private string ballTag = "Bola";
    [Tooltip("Minimum impact speed (m/s) needed to break. Only applies to normal (non-trigger) collisions")]
    [SerializeField] private float minImpactSpeed = 0.5f;
    [Tooltip("The target can only be broken after rising this many meters above the water (ignores hits while underwater)")]
    [SerializeField] private float minHeightToHit = 0.2f;
    [Tooltip("Destroy the ball that hit the target")]
    [SerializeField] private bool destroyBallOnHit = false;

    [Header("Hit Reaction")]
    [Tooltip("How long the pop + shrink animation takes")]
    [SerializeField] private float hitAnimDuration = 0.35f;
    [Tooltip("Optional - a particle system to spawn at the hit point (leave empty to skip)")]
    [SerializeField] private ParticleSystem hitEffectPrefab;

    [Header("Break Effect (shards)")]
    [SerializeField] private bool spawnShards = true;
    [SerializeField] private int shardCount = 12;
    [SerializeField] private Vector2 shardSizeRange = new Vector2(0.05f, 0.12f);
    [Tooltip("Speed (m/s) the shards fly away with")]
    [SerializeField] private Vector2 shardSpeedRange = new Vector2(1.5f, 4f);
    [SerializeField] private float shardLifetime = 2.5f;
    [SerializeField] private AudioClip breakSound;

    [Header("Score Popup (+1)")]
    [SerializeField] private bool showScorePopup = true;
    [SerializeField] private string scoreText = "+1";
    [SerializeField] private Color scoreColor = new Color(1f, 0.9f, 0.2f);
    [Tooltip("Tamanho da fonte (3 a 6 costuma ficar bom no mundo VR)")]
    [SerializeField] private float scoreFontSize = 4f;
    [Tooltip("Altura (m) acima do topo do alvo onde o texto aparece")]
    [SerializeField] private float scoreHeightOffset = 0.25f;
    [Tooltip("Quantos metros o texto sobe")]
    [SerializeField] private float scoreRise = 0.8f;
    [SerializeField] private float scoreDuration = 1.2f;

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

    private Collider[] colliders;
    private MeshRenderer[] meshRenderers;
    private bool hasImpactPoint;
    private Vector3 impactPoint;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
    }

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
        hasImpactPoint = false;

        SetCollidersEnabled(true);
        gameObject.SetActive(true);
    }

    public Vector3 SpawnPosition => sunkenPos;

    private void Update()
    {
        // No spawner = this instance never went through Init() (e.g. it was placed
        // in the scene by hand). Stay idle instead of spamming NullReferenceExceptions.
        if (spawner == null) return;

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

        // Stop the ball from bouncing off / re-hitting the target while it breaks
        SetCollidersEnabled(false);

        onHit?.Invoke();
        if (spawner != null) spawner.RegisterHit();

        ShowScorePopup();

        StartCoroutine(HitReaction());
    }

    // Lets you test by just clicking the target in the editor/desktop build.
    // Harmless to leave in even for VR - just won't fire from a headset.
    private void OnMouseDown()
    {
        Hit();
    }

    // ---------------------------------------------------------------------
    // Score popup
    // ---------------------------------------------------------------------

    private void ShowScorePopup()
    {
        if (!showScorePopup) return;

        Bounds b = GetVisualBounds();
        Vector3 pos = new Vector3(b.center.x, b.max.y + scoreHeightOffset, b.center.z);

        FloatingText.Spawn(pos, scoreText, scoreColor, scoreFontSize, scoreRise, scoreDuration);
    }

    // ---------------------------------------------------------------------
    // Ball detection
    // ---------------------------------------------------------------------

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude < minImpactSpeed) return;

        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        TryHitFromBall(collision.collider, point);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHitFromBall(other, other.ClosestPoint(transform.position));
    }

    private void TryHitFromBall(Collider other, Vector3 point)
    {
        if (isHit || spawner == null) return;
        if (!IsBall(other)) return;

        // Ignore hits while the target is still (mostly) underwater
        if (transform.position.y < sunkenPos.y + minHeightToHit) return;

        impactPoint = point;
        hasImpactPoint = true;
        Hit();

        if (destroyBallOnHit)
        {
            Rigidbody rb = other.attachedRigidbody;
            Destroy(rb != null ? rb.gameObject : other.gameObject);
        }
    }

    private bool IsBall(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag(ballTag)) return true;

        Rigidbody rb = col.attachedRigidbody;
        if (rb != null && rb.CompareTag(ballTag)) return true;

        return col.transform.root.CompareTag(ballTag);
    }

    private void SetCollidersEnabled(bool value)
    {
        if (colliders == null) CacheComponents();

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = value;
        }
    }

    // ---------------------------------------------------------------------
    // Hit reaction
    // ---------------------------------------------------------------------

    /// <summary>
    /// Shards burst out, then a little punchy pop-up, then it spins and shrinks
    /// away to nothing. Purely code-driven so it works with zero extra art -
    /// assign hitEffectPrefab if you want a particle burst layered on top.
    /// </summary>
    private System.Collections.IEnumerator HitReaction()
    {
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.rotation;
        Vector3 startPos = transform.position;

        Vector3 origin = hasImpactPoint ? impactPoint : startPos;

        if (spawnShards)
            SpawnShards(origin);

        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, startPos);

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

    private void SpawnShards(Vector3 impactPoint)
    {
        Bounds bounds = GetVisualBounds();

        Material mat = null;
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null && meshRenderers[i].sharedMaterial != null)
            {
                mat = meshRenderers[i].sharedMaterial;
                break;
            }
        }

        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Shard";

            float size = Random.Range(shardSizeRange.x, shardSizeRange.y);
            shard.transform.localScale = new Vector3(size, size, size);
            shard.transform.rotation = Random.rotation;
            shard.transform.position = bounds.center + Vector3.Scale(Random.insideUnitSphere, bounds.extents);

            if (mat != null)
                shard.GetComponent<Renderer>().sharedMaterial = mat;

            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.05f;

            // Shards fly away from the impact point, with a little upward kick
            Vector3 dir = (shard.transform.position - impactPoint).normalized + Vector3.up * 0.5f;
            rb.AddForce(dir.normalized * Random.Range(shardSpeedRange.x, shardSpeedRange.y),
                        ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);

            Destroy(shard, shardLifetime);
        }
    }

    private Bounds GetVisualBounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
        bool has = false;

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] == null) continue;

            if (!has) { b = meshRenderers[i].bounds; has = true; }
            else b.Encapsulate(meshRenderers[i].bounds);
        }
        return b;
    }

    private void Despawn()
    {
        if (spawner != null)
            spawner.ReturnTarget(this);
        else
            gameObject.SetActive(false);
    }
}