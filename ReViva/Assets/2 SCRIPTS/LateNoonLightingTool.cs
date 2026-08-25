using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR

public class LateNoonLightingTool : MonoBehaviour
{
    [Header("=== GLOBAL STRENGTH ===")]
    [Tooltip("Master multiplier — scales sun, fill and ambient intensity together")]
    [Range(0f, 2f)] public float globalStrength = 1f;

    [Header("=== SUN ===")]
    public Light sunLight;
    [Range(0f, 360f)] public float sunHorizontalAngle = 35f;
    [Range(0f, 90f)] public float sunAltitude = 18f;
    [Range(0f, 10f)] public float sunIntensity = 0.7f;
    public Color sunColor = new Color(1f, 0.72f, 0.45f);

    [Header("=== LENS FLARE ===")]
    [Tooltip("Assign a Lens Flare asset for the sun glare effect (Window > Rendering > Lens Flare)")]
    public Flare sunFlare;
    [Range(0f, 2f)] public float flareStrength = 1f;

    [Header("=== AMBIENT / SKY ===")]
    public Color skyColor = new Color(0.38f, 0.26f, 0.16f);
    public Color equatorColor = new Color(0.26f, 0.20f, 0.15f);
    public Color groundColor = new Color(0.10f, 0.08f, 0.06f);
    [Range(0f, 4f)] public float ambientIntensity = 0.55f;

    [Header("=== FOG ===")]
    public bool enableFog = true;
    public Color fogColor = new Color(0.52f, 0.38f, 0.24f);
    [Range(0f, 0.05f)] public float fogDensity = 0.003f;

    [Header("=== FILL LIGHT (optional) ===")]
    [Tooltip("Leave null to skip")]
    public Light fillLight;
    [Range(0f, 2f)] public float fillIntensity = 0.18f;
    public Color fillColor = new Color(0.35f, 0.45f, 0.65f);

    [Header("=== SHADOWS ===")]
    [Range(0f, 1f)] public float shadowStrength = 0.65f;
    public float shadowDistance = 120f;
}


[CustomEditor(typeof(LateNoonLightingTool))]
public class LateNoonLightingToolEditor : Editor
{
    struct Preset
    {
        public string label;
        public float altitude, horizAngle, sunIntensity;
        public Color sunColor, skyColor, equatorColor, groundColor, fogColor;
        public float fogDensity, ambientIntensity, shadowStrength;
        public float flareStrength;
    }

    static readonly Preset[] Presets = new Preset[]
    {
        new Preset {
            label          = "Golden Hour",
            altitude       = 18f, horizAngle = 35f, sunIntensity = 0.7f,
            sunColor       = new Color(1.00f, 0.72f, 0.45f),
            skyColor       = new Color(0.38f, 0.26f, 0.16f),
            equatorColor   = new Color(0.26f, 0.20f, 0.15f),
            groundColor    = new Color(0.10f, 0.08f, 0.06f),
            fogColor       = new Color(0.52f, 0.38f, 0.24f),
            fogDensity     = 0.003f, ambientIntensity = 0.55f, shadowStrength = 0.65f,
            flareStrength  = 0.5f
        },
        new Preset {
            label          = "Warm Dusk",
            altitude       = 7f, horizAngle = 60f, sunIntensity = 0.45f,
            sunColor       = new Color(1.00f, 0.55f, 0.28f),
            skyColor       = new Color(0.28f, 0.16f, 0.12f),
            equatorColor   = new Color(0.22f, 0.14f, 0.12f),
            groundColor    = new Color(0.07f, 0.05f, 0.04f),
            fogColor       = new Color(0.38f, 0.22f, 0.14f),
            fogDensity     = 0.005f, ambientIntensity = 0.45f, shadowStrength = 0.55f,
            flareStrength  = 0.8f
        },
        new Preset {
            label          = "Hazy Afternoon",
            altitude       = 30f, horizAngle = 20f, sunIntensity = 0.9f,
            sunColor       = new Color(1.00f, 0.85f, 0.62f),
            skyColor       = new Color(0.42f, 0.36f, 0.28f),
            equatorColor   = new Color(0.30f, 0.26f, 0.22f),
            groundColor    = new Color(0.12f, 0.10f, 0.08f),
            fogColor       = new Color(0.55f, 0.48f, 0.38f),
            fogDensity     = 0.005f, ambientIntensity = 0.65f, shadowStrength = 0.55f,
            flareStrength  = 0.4f
        },
        new Preset {
            // Sun blasting through tree canopy — blown-out white-yellow core bleeding into orange
            // High intensity + dense warm fog = light volumetrically scattered between trunks
            label          = "Firewatch",
            altitude       = 22f, horizAngle = 35f, sunIntensity = 3.5f,
            sunColor       = new Color(1.00f, 0.90f, 0.60f),   // near-white hot core
            skyColor       = new Color(0.90f, 0.52f, 0.18f),   // blown-out orange horizon
            equatorColor   = new Color(0.60f, 0.28f, 0.10f),   // warm reddish mid
            groundColor    = new Color(0.08f, 0.05f, 0.03f),   // near-black floor
            fogColor       = new Color(0.78f, 0.48f, 0.20f),   // thick warm haze
            fogDensity     = 0.018f, ambientIntensity = 0.50f, shadowStrength = 0.92f,
            flareStrength  = 1.4f
        },
    };

    public override void OnInspectorGUI()
    {
        LateNoonLightingTool tool = (LateNoonLightingTool)target;

        EditorGUILayout.LabelField("PRESETS", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (var p in Presets)
        {
            GUI.backgroundColor = p.label == "Firewatch"
                ? new Color(0.9f, 0.40f, 0.10f)
                : new Color(0.85f, 0.65f, 0.25f);
            if (GUILayout.Button(p.label, GUILayout.Height(28)))
            {
                Undo.RecordObject(tool, "Apply Lighting Preset");
                ApplyPreset(tool, p);
                ApplyToScene(tool);
                EditorUtility.SetDirty(tool);
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        DrawDefaultInspector();
        EditorGUILayout.Space(8);

        GUI.backgroundColor = new Color(1f, 0.75f, 0.2f);
        if (GUILayout.Button("☀️  APPLY LIGHTING TO SCENE", GUILayout.Height(40)))
        {
            Undo.RecordObject(tool, "Apply Late Noon Lighting");
            ApplyToScene(tool);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "• Global Strength scales everything at once\n" +
            "• Firewatch: sun intensity is HIGH on purpose — it simulates the blown-out canopy look\n" +
            "• For the lens flare: create a Flare asset, assign it to 'Sun Flare', then re-apply\n" +
            "• For volumetric god rays, enable Volumes > Fog in your URP/HDRP settings",
            MessageType.Info
        );
    }

    void ApplyToScene(LateNoonLightingTool tool)
    {
        float g = tool.globalStrength;

        // ── Sun ──────────────────────────────────────────────────────────────────
        if (tool.sunLight != null)
        {
            Undo.RecordObject(tool.sunLight, "Sun Light");
            Undo.RecordObject(tool.sunLight.transform, "Sun Transform");

            tool.sunLight.type = LightType.Directional;
            tool.sunLight.color = tool.sunColor;
            tool.sunLight.intensity = tool.sunIntensity * g;
            tool.sunLight.transform.rotation = Quaternion.Euler(tool.sunAltitude, tool.sunHorizontalAngle, 0f);
            tool.sunLight.shadows = LightShadows.Soft;
            tool.sunLight.shadowStrength = tool.shadowStrength;

            // Lens flare
            var lf = tool.sunLight.GetComponent<LensFlare>();
            if (tool.sunFlare != null)
            {
                if (lf == null) lf = tool.sunLight.gameObject.AddComponent<LensFlare>();
                lf.flare = tool.sunFlare;
                lf.brightness = tool.flareStrength * g;
                lf.color = tool.sunColor;
                lf.fadeSpeed = 5f;
            }
            else if (lf != null)
            {
                lf.brightness = 0f; // hide if no flare assigned
            }

            EditorUtility.SetDirty(tool.sunLight);
        }
        else
        {
            Debug.LogWarning("[LateNoonLighting] No Sun Light assigned.");
        }

        // ── Fill ─────────────────────────────────────────────────────────────────
        if (tool.fillLight != null)
        {
            Undo.RecordObject(tool.fillLight, "Fill Light");
            tool.fillLight.color = tool.fillColor;
            tool.fillLight.intensity = tool.fillIntensity * g;
            tool.fillLight.shadows = LightShadows.None;
            EditorUtility.SetDirty(tool.fillLight);
        }

        // ── Ambient ──────────────────────────────────────────────────────────────
        float a = tool.ambientIntensity * g;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = tool.skyColor * a;
        RenderSettings.ambientEquatorColor = tool.equatorColor * a;
        RenderSettings.ambientGroundColor = tool.groundColor * a;

        // ── Fog ──────────────────────────────────────────────────────────────────
        RenderSettings.fog = tool.enableFog;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = tool.fogColor;
        RenderSettings.fogDensity = tool.fogDensity;

        // ── Shadows ──────────────────────────────────────────────────────────────
        QualitySettings.shadowDistance = tool.shadowDistance;

        Debug.Log($"[LateNoonLighting] ☀️ Applied — Global Strength: {g:F2}");
    }

    void ApplyPreset(LateNoonLightingTool tool, Preset p)
    {
        tool.sunAltitude = p.altitude;
        tool.sunHorizontalAngle = p.horizAngle;
        tool.sunIntensity = p.sunIntensity;
        tool.sunColor = p.sunColor;
        tool.skyColor = p.skyColor;
        tool.equatorColor = p.equatorColor;
        tool.groundColor = p.groundColor;
        tool.fogColor = p.fogColor;
        tool.fogDensity = p.fogDensity;
        tool.ambientIntensity = p.ambientIntensity;
        tool.shadowStrength = p.shadowStrength;
        tool.flareStrength = p.flareStrength;
    }
}
#endif