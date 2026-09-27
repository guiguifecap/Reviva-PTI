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
        Despawn();
    }

    // Lets you test by just clicking the target in the editor/desktop build.
    // Harmless to leave in even for VR - just won't fire from a headset.
    private void OnMouseDown()
    {
        Hit();
    }

    private void Despawn()
    {
        spawner.ReturnTarget(this);
    }
}