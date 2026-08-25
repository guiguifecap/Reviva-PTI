using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Espalha vegetação (grama, samambaias, etc.) de forma aleatória sobre terreno ou mesh.
/// Suporta múltiplos prefabs com chances de aparição configuráveis.
/// </summary>
public class VegetationSpawner : MonoBehaviour
{
    [System.Serializable]
    public class VegetationEntry
    {
        [Tooltip("Prefab da vegetação (FBX importado como prefab)")]
        public GameObject prefab;

        [Tooltip("Chance de aparição relativa (peso). Ex: grama=10, samambaia=3 = grama aparece ~3x mais")]
        [Range(0.1f, 100f)]
        public float spawnWeight = 10f;

        [Tooltip("Escala mínima aleatória")]
        public float minScale = 0.8f;

        [Tooltip("Escala máxima aleatória")]
        public float maxScale = 1.2f;

        [Tooltip("Rotação Y aleatória")]
        public bool randomRotationY = true;

        [Tooltip("Inclinar levemente com a normal do terreno")]
        public bool alignToNormal = true;

        [Tooltip("Inclinação máxima em graus para alinhar com a normal")]
        [Range(0f, 90f)]
        public float maxNormalTilt = 30f;
    }

    [Header("Prefabs de Vegetação")]
    [Tooltip("Lista de vegetações com suas chances de aparição")]
    public List<VegetationEntry> vegetationEntries = new List<VegetationEntry>();

    [Header("Área de Spawn")]
    [Tooltip("Centro da área de spawn (usa a posição deste GameObject se nulo)")]
    public Transform spawnCenter;

    [Tooltip("Largura da área de spawn (eixo X)")]
    public float areaWidth = 20f;

    [Tooltip("Comprimento da área de spawn (eixo Z)")]
    public float areaLength = 20f;

    [Tooltip("Altura máxima de raycast (de onde o ray começa a cair)")]
    public float raycastHeight = 50f;

    [Header("Quantidade")]
    [Tooltip("Quantidade total de vegetações a spawnar")]
    public int totalSpawnCount = 200;

    [Tooltip("Distância mínima entre cada vegetação (evita sobreposição)")]
    [Range(0f, 5f)]
    public float minDistanceBetween = 0.3f;

    [Header("Layers e Física")]
    [Tooltip("Layer do terreno/mesh onde a vegetação será colocada")]
    public LayerMask groundLayer = ~0;

    [Tooltip("Offset Y aplicado após posicionar no terreno")]
    public float yOffset = 0f;

    [Header("Organização")]
    [Tooltip("Container pai para os objetos spawnados (organiza a hierarquia)")]
    public Transform spawnParent;

    [Tooltip("Limpar vegetação antiga antes de spawnar nova")]
    public bool clearOnSpawn = true;

    // Lista interna para controle de distância mínima
    private List<Vector3> spawnedPositions = new List<Vector3>();
    private List<GameObject> spawnedObjects = new List<GameObject>();

    // Pesos acumulados para seleção aleatória ponderada
    private float[] cumulativeWeights;
    private float totalWeight;

    private void Start()
    {
        // Spawn automático ao iniciar a cena (pode desabilitar se preferir spawnar pelo Editor)
        SpawnVegetation();
    }

    /// <summary>
    /// Calcula os pesos acumulados para seleção aleatória ponderada.
    /// </summary>
    private void CalculateWeights()
    {
        if (vegetationEntries == null || vegetationEntries.Count == 0)
        {
            Debug.LogWarning("[VegetationSpawner] Nenhuma entrada de vegetação configurada!");
            return;
        }

        cumulativeWeights = new float[vegetationEntries.Count];
        totalWeight = 0f;

        for (int i = 0; i < vegetationEntries.Count; i++)
        {
            totalWeight += vegetationEntries[i].spawnWeight;
            cumulativeWeights[i] = totalWeight;
        }
    }

    /// <summary>
    /// Seleciona um prefab aleatório com base nos pesos configurados.
    /// </summary>
    private VegetationEntry GetRandomEntry()
    {
        float rand = Random.Range(0f, totalWeight);

        for (int i = 0; i < cumulativeWeights.Length; i++)
        {
            if (rand <= cumulativeWeights[i])
                return vegetationEntries[i];
        }

        return vegetationEntries[vegetationEntries.Count - 1];
    }

    /// <summary>
    /// Método principal: limpa a vegetação antiga e spawna nova.
    /// </summary>
    public void SpawnVegetation()
    {
        if (vegetationEntries == null || vegetationEntries.Count == 0)
        {
            Debug.LogError("[VegetationSpawner] Adicione ao menos um prefab de vegetação!");
            return;
        }

        // Valida prefabs
        foreach (var entry in vegetationEntries)
        {
            if (entry.prefab == null)
            {
                Debug.LogError("[VegetationSpawner] Um ou mais prefabs estão nulos! Verifique a lista.");
                return;
            }
        }

        if (clearOnSpawn)
            ClearVegetation();

        CalculateWeights();

        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;

        // Cria container se não existir
        if (spawnParent == null)
        {
            GameObject container = new GameObject("_VegetationContainer");
            container.transform.position = Vector3.zero;
            spawnParent = container.transform;
        }

        spawnedPositions.Clear();
        int spawned = 0;
        int maxAttempts = totalSpawnCount * 10; // Limite de tentativas para evitar loop infinito
        int attempts = 0;

        while (spawned < totalSpawnCount && attempts < maxAttempts)
        {
            attempts++;

            // Posição aleatória na área
            float randX = Random.Range(-areaWidth * 0.5f, areaWidth * 0.5f);
            float randZ = Random.Range(-areaLength * 0.5f, areaLength * 0.5f);
            Vector3 rayOrigin = center + new Vector3(randX, raycastHeight, randZ);

            // Raycast para encontrar o terreno
            RaycastHit hit;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastHeight * 2f, groundLayer))
                continue;

            Vector3 spawnPos = hit.point + new Vector3(0f, yOffset, 0f);

            // Verifica distância mínima em relação às posições já spawnadas
            bool tooClose = false;
            foreach (var pos in spawnedPositions)
            {
                if (Vector3.Distance(spawnPos, pos) < minDistanceBetween)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            // Seleciona entrada aleatória ponderada
            VegetationEntry entry = GetRandomEntry();

            // Instancia o prefab
            GameObject instance = Instantiate(entry.prefab, spawnPos, Quaternion.identity, spawnParent);

            // Rotação
            Quaternion rotation = Quaternion.identity;

            if (entry.alignToNormal && entry.maxNormalTilt > 0f)
            {
                // Alinha parcialmente com a normal do terreno
                Quaternion normalRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                float tiltFactor = entry.maxNormalTilt / 90f;
                rotation = Quaternion.Slerp(Quaternion.identity, normalRotation, tiltFactor);
            }

            if (entry.randomRotationY)
            {
                rotation = Quaternion.Euler(rotation.eulerAngles.x, Random.Range(0f, 360f), rotation.eulerAngles.z);
            }

            instance.transform.rotation = rotation;

            // Escala aleatória uniforme
            float scale = Random.Range(entry.minScale, entry.maxScale);
            instance.transform.localScale = Vector3.one * scale;

            // Adiciona o componente de vento automaticamente se não tiver
            if (instance.GetComponentInChildren<Renderer>() != null)
            {
                if (instance.GetComponent<VegetationWindSway>() == null)
                    instance.AddComponent<VegetationWindSway>();
            }

            spawnedPositions.Add(spawnPos);
            spawnedObjects.Add(instance);
            spawned++;
        }

        Debug.Log($"[VegetationSpawner] Spawnados {spawned} objetos de vegetação. (Tentativas: {attempts})");
    }

    /// <summary>
    /// Remove toda a vegetação spawnada anteriormente.
    /// </summary>
    public void ClearVegetation()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(obj);
                else
                    Destroy(obj);
#else
                Destroy(obj);
#endif
            }
        }

        spawnedObjects.Clear();
        spawnedPositions.Clear();

        // Limpa também filhos do container que possam ter sobrado
        if (spawnParent != null)
        {
            for (int i = spawnParent.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(spawnParent.GetChild(i).gameObject);
                else
                    Destroy(spawnParent.GetChild(i).gameObject);
#else
                Destroy(spawnParent.GetChild(i).gameObject);
#endif
            }
        }
    }

    /// <summary>
    /// Desenha o Gizmo da área de spawn no Editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        Gizmos.DrawCube(center, new Vector3(areaWidth, 0.1f, areaLength));

        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(center, new Vector3(areaWidth, 0.1f, areaLength));
    }
}


// ─────────────────────────────────────────────
// EDITOR CUSTOM INSPECTOR (só compila no Editor)
// ─────────────────────────────────────────────
#if UNITY_EDITOR
[CustomEditor(typeof(VegetationSpawner))]
public class VegetationSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VegetationSpawner spawner = (VegetationSpawner)target;

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("🌿  Spawnar Vegetação", GUILayout.Height(35)))
        {
            spawner.SpawnVegetation();
        }

        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑  Limpar Vegetação", GUILayout.Height(28)))
        {
            spawner.ClearVegetation();
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif
