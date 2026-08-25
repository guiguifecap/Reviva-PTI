using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simula movimento natural de vento em vegetação (grama, samambaias, arbustos).
/// Aplica oscilação suave via shader (MaterialPropertyBlock) ou transform,
/// com suporte a rajadas aleatórias de vento.
/// </summary>
public class VegetationWindSway : MonoBehaviour
{
    // ──────────────────────────────────────────────
    // CONFIGURAÇÕES DE VENTO
    // ──────────────────────────────────────────────

    [Header("Intensidade do Vento")]
    [Tooltip("Intensidade base do balanço (amplitude do movimento)")]
    [Range(0f, 5f)]
    public float swayStrength = 0.8f;

    [Tooltip("Velocidade base da oscilação (Hz)")]
    [Range(0.1f, 5f)]
    public float swaySpeed = 1.2f;

    [Tooltip("Randomiza levemente speed e strength por instância (efeito mais natural)")]
    public bool randomizePerInstance = true;

    [Header("Direção do Vento")]
    [Tooltip("Usar direção global de vento do WindManager (se existir na cena)")]
    public bool useGlobalWind = true;

    [Tooltip("Direção local do vento (usada se não houver WindManager)")]
    public Vector2 localWindDirection = new Vector2(1f, 0.3f);

    [Header("Rajadas (Gusts)")]
    [Tooltip("Habilita rajadas aleatórias de vento")]
    public bool enableGusts = true;

    [Tooltip("Intensidade extra durante uma rajada")]
    [Range(0f, 10f)]
    public float gustStrength = 3f;

    [Tooltip("Duração mínima de uma rajada (segundos)")]
    public float gustDurationMin = 0.5f;

    [Tooltip("Duração máxima de uma rajada (segundos)")]
    public float gustDurationMax = 2f;

    [Tooltip("Intervalo mínimo entre rajadas (segundos)")]
    public float gustIntervalMin = 3f;

    [Tooltip("Intervalo máximo entre rajadas (segundos)")]
    public float gustIntervalMax = 10f;

    [Header("Modo de Animação")]
    [Tooltip("Transform: move o objeto via código. Shader: passa valores para o shader (mais performático para muitos objetos)")]
    public SwayMode swayMode = SwayMode.Transform;

    [Tooltip("Nome da propriedade no shader para o deslocamento do vento (modo Shader)")]
    public string shaderWindProperty = "_WindStrength";

    public enum SwayMode { Transform, Shader }

    // ──────────────────────────────────────────────
    // VARIÁVEIS INTERNAS
    // ──────────────────────────────────────────────

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float timeOffset;          // Offset de fase para dessincronizar instâncias
    private float instanceSpeedMult;
    private float instanceStrengthMult;

    private bool isGusting = false;
    private float currentGustStrength = 0f;
    private float gustTimer = 0f;
    private float gustDuration = 0f;
    private float nextGustTime = 0f;

    private Renderer[] renderers;
    private MaterialPropertyBlock mpb;

    // Cache da direção de vento atual
    private Vector2 currentWindDir;

    private void Awake()
    {
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
        timeOffset = Random.Range(0f, 100f);

        if (randomizePerInstance)
        {
            instanceSpeedMult = Random.Range(0.7f, 1.3f);
            instanceStrengthMult = Random.Range(0.8f, 1.2f);
        }
        else
        {
            instanceSpeedMult = 1f;
            instanceStrengthMult = 1f;
        }

        if (swayMode == SwayMode.Shader)
        {
            renderers = GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
        }

        if (enableGusts)
            nextGustTime = Random.Range(gustIntervalMin, gustIntervalMax);
    }

    private void Update()
    {
        UpdateGust();

        if (useGlobalWind && WindManager.Instance != null)
            currentWindDir = WindManager.Instance.GetWindDirection();
        else
            currentWindDir = localWindDirection.normalized;

        float t = Time.time + timeOffset;
        float speed = swaySpeed * instanceSpeedMult;
        float strength = (swayStrength + currentGustStrength) * instanceStrengthMult;

        switch (swayMode)
        {
            case SwayMode.Transform:
                ApplyTransformSway(t, speed, strength);
                break;
            case SwayMode.Shader:
                ApplyShaderSway(strength);
                break;
        }
    }

    /// <summary>
    /// Move o transform para simular o balanço. Recomendado para poucos objetos ou objetos maiores.
    /// </summary>
    private void ApplyTransformSway(float t, float speed, float strength)
    {
        // Oscilação primária suave (senoide)
        float primarySway = Mathf.Sin(t * speed) * strength * 0.01f;

        // Oscilação secundária mais rápida e leve (dá textura ao movimento)
        float secondarySway = Mathf.Sin(t * speed * 2.3f + 1.7f) * strength * 0.004f;

        // Oscilação de torção leve
        float twistSway = Mathf.Sin(t * speed * 0.7f + 3.1f) * strength * 0.003f;

        float totalSway = primarySway + secondarySway;

        // Aplica como rotação (mais realista que translação)
        float rotX = currentWindDir.y * totalSway * Mathf.Rad2Deg * 15f;
        float rotZ = -currentWindDir.x * totalSway * Mathf.Rad2Deg * 15f;
        float rotY = twistSway * Mathf.Rad2Deg * 5f;

        transform.localRotation = originalRotation * Quaternion.Euler(rotX, rotY, rotZ);
    }

    /// <summary>
    /// Passa o valor de vento para o shader via MaterialPropertyBlock (mais performático).
    /// Requer que o shader da vegetação suporte a propriedade configurada.
    /// </summary>
    private void ApplyShaderSway(float strength)
    {
        if (renderers == null) return;

        float normalizedStrength = strength / 10f;

        foreach (var r in renderers)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(shaderWindProperty, normalizedStrength);
            r.SetPropertyBlock(mpb);
        }
    }

    /// <summary>
    /// Gerencia o ciclo de rajadas de vento.
    /// </summary>
    private void UpdateGust()
    {
        if (!enableGusts) return;

        if (!isGusting)
        {
            nextGustTime -= Time.deltaTime;
            if (nextGustTime <= 0f)
            {
                StartGust();
            }
        }
        else
        {
            gustTimer += Time.deltaTime;
            float progress = gustTimer / gustDuration;

            if (progress < 0.2f)
            {
                // Entrada rápida da rajada
                currentGustStrength = Mathf.Lerp(0f, gustStrength, progress / 0.2f);
            }
            else if (progress < 0.7f)
            {
                // Pico com leve variação aleatória (turbulência)
                float turbulence = Mathf.PerlinNoise(Time.time * 5f, timeOffset) * 0.3f;
                currentGustStrength = gustStrength * (1f + turbulence - 0.15f);
            }
            else if (progress < 1f)
            {
                // Saída suave da rajada
                currentGustStrength = Mathf.Lerp(gustStrength, 0f, (progress - 0.7f) / 0.3f);
            }
            else
            {
                EndGust();
            }
        }
    }

    private void StartGust()
    {
        isGusting = true;
        gustTimer = 0f;
        gustDuration = Random.Range(gustDurationMin, gustDurationMax);
    }

    private void EndGust()
    {
        isGusting = false;
        currentGustStrength = 0f;
        nextGustTime = Random.Range(gustIntervalMin, gustIntervalMax);
    }

    /// <summary>
    /// Reseta a vegetação para a posição/rotação original.
    /// </summary>
    public void ResetToOriginal()
    {
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
    }

    private void OnDisable()
    {
        ResetToOriginal();
    }
}


// ──────────────────────────────────────────────────────────────
// WIND MANAGER — Singleton para controlar o vento global da cena
// ──────────────────────────────────────────────────────────────

/// <summary>
/// Gerencia o vento global da cena. Adicione este componente a um GameObject vazio na cena.
/// Todas as vegetações com "useGlobalWind = true" irão utilizar esta direção.
/// </summary>
public class WindManager : MonoBehaviour
{
    public static WindManager Instance { get; private set; }

    [Header("Vento Global")]
    [Tooltip("Direção base do vento (X = leste/oeste, Y = norte/sul)")]
    public Vector2 baseWindDirection = new Vector2(1f, 0.2f);

    [Tooltip("Velocidade de mudança suave da direção do vento")]
    [Range(0f, 1f)]
    public float windDirectionChangeSpeed = 0.05f;

    [Tooltip("Variar a direção do vento ao longo do tempo (efeito mais natural)")]
    public bool varyWindDirection = true;

    [Tooltip("Amplitude da variação da direção (graus)")]
    [Range(0f, 90f)]
    public float windVariationAngle = 20f;

    private Vector2 currentWindDir;
    private Vector2 targetWindDir;
    private float changeTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentWindDir = baseWindDirection.normalized;
        targetWindDir = currentWindDir;
    }

    private void Update()
    {
        if (!varyWindDirection)
        {
            currentWindDir = baseWindDirection.normalized;
            return;
        }

        changeTimer -= Time.deltaTime;
        if (changeTimer <= 0f)
        {
            // Gera nova direção alvo com variação angular
            float baseAngle = Mathf.Atan2(baseWindDirection.y, baseWindDirection.x) * Mathf.Rad2Deg;
            float variation = Random.Range(-windVariationAngle, windVariationAngle);
            float newAngle = (baseAngle + variation) * Mathf.Deg2Rad;
            targetWindDir = new Vector2(Mathf.Cos(newAngle), Mathf.Sin(newAngle));
            changeTimer = Random.Range(2f, 6f);
        }

        currentWindDir = Vector2.Lerp(currentWindDir, targetWindDir, windDirectionChangeSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Retorna a direção normalizada atual do vento.
    /// </summary>
    public Vector2 GetWindDirection()
    {
        return currentWindDir.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 dir3D = new Vector3(baseWindDirection.x, 0f, baseWindDirection.y).normalized;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, dir3D * 5f);
        Gizmos.DrawSphere(transform.position + dir3D * 5f, 0.3f);
    }
}
