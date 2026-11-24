using TMPro;
using UnityEngine;

/// <summary>
/// Texto 3D que sobe, desaparece e se destrói sozinho (ex: "+1").
/// Sempre gira para olhar a câmera do player.
/// Não precisa ser colocado na cena: o WaterTarget cria por código.
/// </summary>
public class FloatingText : MonoBehaviour
{
    private TextMeshPro tmp;
    private Color baseColor;
    private Vector3 startPos;
    private float rise;
    private float duration;
    private float timer;

    public static FloatingText Spawn(Vector3 position, string text, Color color,
                                     float fontSize, float rise, float duration)
    {
        GameObject go = new GameObject("FloatingText");
        go.transform.position = position;

        FloatingText ft = go.AddComponent<FloatingText>();
        ft.tmp = go.AddComponent<TextMeshPro>();
        ft.tmp.text = text;
        ft.tmp.fontSize = fontSize;
        ft.tmp.fontStyle = FontStyles.Bold;
        ft.tmp.alignment = TextAlignmentOptions.Center;
        ft.tmp.color = color;
        ft.tmp.textWrappingMode = TextWrappingModes.NoWrap;

        ft.baseColor = color;
        ft.startPos = position;
        ft.rise = rise;
        ft.duration = Mathf.Max(0.1f, duration);

        go.transform.localScale = Vector3.one * 0.5f;
        FaceCamera(go.transform);
        return ft;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float p = Mathf.Clamp01(timer / duration);

        // Sobe rápido no começo e desacelera
        float ease = 1f - (1f - p) * (1f - p);
        transform.position = startPos + Vector3.up * (rise * ease);

        // "Pop" de escala no início
        float pop = Mathf.Clamp01(timer / 0.15f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, pop);

        // Some na segunda metade
        float alpha = p < 0.5f ? 1f : 1f - (p - 0.5f) / 0.5f;
        tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        if (p >= 1f) Destroy(gameObject);
    }

    private void LateUpdate()
    {
        FaceCamera(transform);
    }

    // O TextMeshPro 3D é lido de frente quando o forward dele aponta para longe da câmera
    private static void FaceCamera(Transform t)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 dir = t.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(dir);
    }
}
