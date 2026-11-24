using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

public class Timer : MonoBehaviour
{
    public static Timer Instance;

    public TMP_Text timerText;

    [Header("Tags")]
    public string homeTag = "Casa";
    public string playerTag = "Player";

    [Header("Finish")]
    public float homePadding = 1f; // how close to the house (in meters) counts as arrived

    public bool isRunning = false;
    public bool finished = false;

    private float startTime;

    private Transform player;
    private Transform[] homes;
    private float nextLog;

    private XRBaseInteractor[] interactors;
    private LocomotionProvider[] providers;
    private bool locked;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateDisplay(0f);

        try
        {
            var p = GameObject.FindWithTag(playerTag);
            if (p != null) player = p.transform;
        }
        catch (UnityException e)
        {
            Debug.LogError("[Timer] A tag doesn't exist in Project Settings > Tags and Layers: " + e.Message);
        }

        Debug.Log($"[Timer] player found: {player != null}");
    }

    void Update()
    {
        PollClimb();

        if (!isRunning || finished) return;

        UpdateDisplay(Time.time - startTime);

        if (ReachedHome())
            StopTimer();
    }

    // ---------- climbing: starts timer + locks joysticks ----------

    void PollClimb()
    {
        if (interactors == null || interactors.Length == 0)
            interactors = FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (providers == null || providers.Length == 0)
            providers = FindObjectsByType<LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        bool climbing = false;
        foreach (var i in interactors)
        {
            if (i == null || !i.hasSelection) continue;

            foreach (var s in i.interactablesSelected)
            {
                if (s is ClimbInteractable) { climbing = true; break; }
            }
            if (climbing) break;
        }

        if (climbing && !finished)
            StartTimer(); // ignored if already running, so only the first rock counts

        if (climbing != locked)
        {
            locked = climbing;
            foreach (var p in providers)
            {
                if (p == null || p is ClimbProvider) continue; // never disable climbing itself
                p.enabled = !locked;
            }
            Debug.Log("[Timer] locomotion " + (locked ? "DISABLED" : "ENABLED"));
        }
    }

    // ---------- finish line (found by tag) ----------

    // true if this object is the timer text or any UI, so it isn't counted as part of the house
    bool IsIgnored(Component c)
    {
        if (c.GetComponentInParent<Canvas>() != null) return true;
        if (timerText != null && c.transform.IsChildOf(timerText.transform)) return true;
        return false;
    }

    bool ReachedHome()
    {
        // find the house(s) by tag, retries until found
        if (homes == null || homes.Length == 0)
        {
            var objs = GameObject.FindGameObjectsWithTag(homeTag);
            homes = new Transform[objs.Length];
            for (int i = 0; i < objs.Length; i++) homes[i] = objs[i].transform;

            if (homes.Length == 0)
            {
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 2f;
                    Debug.LogWarning($"[Timer] No object with tag '{homeTag}' found. Check the spelling matches exactly.");
                }
                return false;
            }
        }

        Vector3 feet = player != null ? player.position : Vector3.zero;
        Vector3 head = Camera.main != null ? Camera.main.transform.position : feet;

        float best = float.MaxValue;

        foreach (var h in homes)
        {
            if (h == null) continue;

            // size of the house, ignoring UI / the timer text
            Bounds b = new Bounds(h.position, Vector3.zero);

            foreach (var c in h.GetComponentsInChildren<Collider>())
                if (!IsIgnored(c)) b.Encapsulate(c.bounds);

            foreach (var r in h.GetComponentsInChildren<Renderer>())
                if (!IsIgnored(r)) b.Encapsulate(r.bounds);

            float d = Mathf.Min(Mathf.Sqrt(b.SqrDistance(feet)), Mathf.Sqrt(b.SqrDistance(head)));
            best = Mathf.Min(best, d);

            if (d <= homePadding)
                return true;
        }

        if (Time.time >= nextLog)
        {
            nextLog = Time.time + 1f;
            Debug.Log($"[Timer] distance to house: {best:F2} m (needs <= {homePadding})");
        }
        return false;
    }

    // ---------- timer ----------

    void UpdateDisplay(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        int centis = Mathf.FloorToInt((time * 100) % 100);

        timerText.text = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, centis);
    }

    public void StartTimer()
    {
        if (isRunning || finished) return;
        startTime = Time.time;
        isRunning = true;
    }

    public void StopTimer()
    {
        if (!isRunning || finished) return;
        isRunning = false;
        finished = true;
        UpdateDisplay(Time.time - startTime);
    }

    public void ResetTimer()
    {
        isRunning = false;
        finished = false;
        UpdateDisplay(0f);
    }
}