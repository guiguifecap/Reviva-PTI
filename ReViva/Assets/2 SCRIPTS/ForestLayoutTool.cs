using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
[ExecuteInEditMode]
public class ForestLayoutTool : MonoBehaviour
{
    [Header("=== OBJETOS ===")]
    [Tooltip("Arraste aqui os prefabs de árvores")]
    public GameObject[] treePrefabs;

    [Header("=== ÁREA DA FLORESTA (Polígono) ===")]
    [Tooltip("Pontos que definem o polígono da área de spawn. Edite arrastando os pontos na Scene View.")]
    public List<Vector3> polygonPoints = new List<Vector3>
    {
        new Vector3(-20, 0, -20),
        new Vector3( 20, 0, -20),
        new Vector3( 20, 0,  20),
        new Vector3(-20, 0,  20),
    };

    [Header("=== CLAREIRA ===")]
    [Tooltip("Raio da área circular SEM árvores (0 = desativado)")]
    public float clearingRadius = 0f;
    public Vector2 clearingOffset = Vector2.zero;

    [Header("=== ESPAÇAMENTO ===")]
    public float minDistanceBetweenTrees = 3f;
    [Tooltip("Variação aleatória de posição (0 = grade uniforme, >0 = orgânico)")]
    public float randomOffset = 1.5f;

    [Header("=== CORREÇÃO DE ROTAÇÃO ===")]
    [Tooltip("Se as árvores nascerem deitadas, tente X = 90 ou -90")]
    public Vector3 rotationCorrection = Vector3.zero;

    [Header("=== VARIAÇÃO VISUAL ===")]
    public bool randomRotation = true;
    public bool randomScale = true;
    [Min(0.1f)] public float scaleMin = 0.8f;
    [Min(0.1f)] public float scaleMax = 1.3f;

    [Header("=== TERRENO ===")]
    [Tooltip("Cola as árvores no chão via Raycast")]
    public bool snapToGround = true;
    public float raycastHeight = 50f;
    public LayerMask groundLayer = ~0;

    [Header("=== SEED ===")]
    public int randomSeed = 42;
}


[CustomEditor(typeof(ForestLayoutTool))]
public class ForestLayoutToolEditor : Editor
{
    // Índice do ponto sendo arrastado (-1 = nenhum)
    private int _draggingIndex = -1;
    private bool _editMode = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ForestLayoutTool tool = (ForestLayoutTool)target;

        EditorGUILayout.Space(10);

        // Botão de editar polígono
        GUI.backgroundColor = _editMode
            ? new Color(1f, 0.7f, 0.1f)
            : new Color(0.3f, 0.6f, 1f);

        string editLabel = _editMode ? "✏️  EDITANDO POLÍGONO (clique pra sair)" : "✏️  EDITAR POLÍGONO NA SCENE";
        if (GUILayout.Button(editLabel, GUILayout.Height(32)))
        {
            _editMode = !_editMode;
            SceneView.RepaintAll();
        }

        if (_editMode)
        {
            EditorGUILayout.HelpBox(
                "🖱️ Arraste os pontos brancos para reshapear a área.\n" +
                "➕ Shift+Click em uma aresta = adicionar ponto.\n" +
                "➖ Ctrl+Click em um ponto = remover ponto.",
                MessageType.Info
            );
        }

        EditorGUILayout.Space(4);

        // Botão pra resetar polígono padrão
        GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f);
        if (GUILayout.Button("↺  Resetar Polígono (quadrado 40x40)", GUILayout.Height(24)))
        {
            Undo.RecordObject(tool, "Resetar Polígono");
            Vector3 o = tool.transform.position;
            tool.polygonPoints = new List<Vector3>
            {
                new Vector3(o.x - 20, o.y, o.z - 20),
                new Vector3(o.x + 20, o.y, o.z - 20),
                new Vector3(o.x + 20, o.y, o.z + 20),
                new Vector3(o.x - 20, o.y, o.z + 20),
            };
            EditorUtility.SetDirty(tool);
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);

        // Botão gerar
        GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button("🌲  GERAR FLORESTA", GUILayout.Height(40)))
            GenerateForest(tool);

        // Botão limpar
        GUI.backgroundColor = new Color(0.85f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️  LIMPAR FLORESTA", GUILayout.Height(30)))
            ClearForest(tool);

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "• Funciona fora do Play Mode\n" +
            "• Mude o Seed para layouts diferentes\n" +
            "• Snap to Ground funciona com Terrain e Colliders\n" +
            "• Árvores deitadas? Use Rotation Correction X = 90",
            MessageType.Info
        );
    }

    // ─────────────────────────────────────────────
    //  SCENE GUI — desenha e edita o polígono
    // ─────────────────────────────────────────────
    void OnSceneGUI()
    {
        ForestLayoutTool tool = (ForestLayoutTool)target;
        var pts = tool.polygonPoints;

        if (pts == null || pts.Count < 3) return;

        Vector3 origin = tool.transform.position;
        Event e = Event.current;

        // ── Desenha polígono preenchido (semi-transparente) ──
        Handles.color = new Color(0.3f, 0.9f, 0.3f, 0.12f);
        Vector3[] verts = pts.ToArray();
        // Triangula o polígono de forma simples (fan a partir do ponto 0)
        List<Vector3> triVerts = new List<Vector3>();
        for (int i = 1; i < pts.Count - 1; i++)
        {
            triVerts.Add(pts[0]);
            triVerts.Add(pts[i]);
            triVerts.Add(pts[i + 1]);
        }
        Handles.DrawAAConvexPolygon(pts.ToArray()); // fallback rápido

        // ── Desenha arestas ──
        Handles.color = new Color(0.3f, 0.95f, 0.3f, 0.85f);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[(i + 1) % pts.Count];
            Handles.DrawLine(a, b, 2f);

            // Shift+Click numa aresta = inserir ponto
            if (_editMode && e.control && e.type == EventType.MouseDown && e.button == 0)
            {
                Vector3 mid = (a + b) * 0.5f;
                float screenDist = HandleUtility.WorldToGUIPoint(mid) != Vector2.zero
                    ? Vector2.Distance(HandleUtility.WorldToGUIPoint(mid), e.mousePosition)
                    : 9999f;
                if (screenDist < 20f)
                {
                    Undo.RecordObject(tool, "Inserir Ponto");
                    pts.Insert(i + 1, mid);
                    EditorUtility.SetDirty(tool);
                    e.Use();
                    break;
                }
            }
        }

        // ── Handles nos vértices ──
        for (int i = 0; i < pts.Count; i++)
        {
            float handleSize = HandleUtility.GetHandleSize(pts[i]) * 0.12f;

            if (_editMode)
            {
                // Ctrl+Click = remover ponto
                if (e.control && e.type == EventType.MouseDown && e.button == 0)
                {
                    float d = Vector2.Distance(HandleUtility.WorldToGUIPoint(pts[i]), e.mousePosition);
                    if (d < 15f && pts.Count > 3)
                    {
                        Undo.RecordObject(tool, "Remover Ponto");
                        pts.RemoveAt(i);
                        EditorUtility.SetDirty(tool);
                        e.Use();
                        break;
                    }
                }

                // Handle de arrastar
                Handles.color = (_draggingIndex == i) ? Color.yellow : Color.white;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.FreeMoveHandle(pts[i], handleSize, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tool, "Mover Ponto");
                    // Mantém o Y do objeto (trabalha no plano XZ)
                    newPos.y = origin.y;
                    pts[i] = newPos;
                    EditorUtility.SetDirty(tool);
                }

                // Label do índice
                Handles.Label(pts[i] + Vector3.up * 0.5f, $"  {i}",
                    new GUIStyle { normal = { textColor = Color.yellow }, fontSize = 11, fontStyle = FontStyle.Bold });
            }
            else
            {
                // Modo visualização: só bolinhas
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                Handles.SphereHandleCap(0, pts[i], Quaternion.identity, handleSize, EventType.Repaint);
            }
        }

        // ── Clareira ──
        if (tool.clearingRadius > 0f)
        {
            Vector3 clearCenter = new Vector3(
                origin.x + tool.clearingOffset.x,
                origin.y,
                origin.z + tool.clearingOffset.y);
            Handles.color = new Color(1f, 0.8f, 0.1f, 0.6f);
            Handles.DrawWireDisc(clearCenter, Vector3.up, tool.clearingRadius);
            Handles.Label(clearCenter + Vector3.right * tool.clearingRadius, " Clareira");
        }

        // Força repaint enquanto editando
        if (_editMode)
            HandleUtility.Repaint();
    }

    // ─────────────────────────────────────────────
    //  GERAÇÃO
    // ─────────────────────────────────────────────
    void GenerateForest(ForestLayoutTool tool)
    {
        if (tool.treePrefabs == null || tool.treePrefabs.Length == 0)
        {
            EditorUtility.DisplayDialog("Erro", "Adicione pelo menos um prefab em 'Tree Prefabs'!", "OK");
            return;
        }

        if (tool.polygonPoints == null || tool.polygonPoints.Count < 3)
        {
            EditorUtility.DisplayDialog("Erro", "O polígono precisa ter ao menos 3 pontos!", "OK");
            return;
        }

        ClearForest(tool);

        GameObject container = new GameObject(GetContainerName());
        container.transform.SetParent(tool.transform);
        container.transform.localPosition = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(container, "Gerar Floresta");

        Random.InitState(tool.randomSeed);

        List<Vector2> placedPositions = new List<Vector2>();
        Vector3 origin = tool.transform.position;
        Vector2 clearCenter = new Vector2(origin.x + tool.clearingOffset.x, origin.z + tool.clearingOffset.y);

        // Calcula bounding box do polígono pra varrer a grade
        float xMin = float.MaxValue, xMax = float.MinValue;
        float zMin = float.MaxValue, zMax = float.MinValue;
        foreach (var p in tool.polygonPoints)
        {
            if (p.x < xMin) xMin = p.x;
            if (p.x > xMax) xMax = p.x;
            if (p.z < zMin) zMin = p.z;
            if (p.z > zMax) zMax = p.z;
        }

        float step = tool.minDistanceBetweenTrees;
        int attempts = 0;
        int maxAttempts = 50000;

        // Pré-calcula o polígono em 2D (XZ)
        List<Vector2> poly2D = new List<Vector2>();
        foreach (var p in tool.polygonPoints)
            poly2D.Add(new Vector2(p.x, p.z));

        for (float x = xMin; x <= xMax; x += step)
        {
            for (float z = zMin; z <= zMax; z += step)
            {
                if (attempts++ > maxAttempts) break;

                float ox = Random.Range(-tool.randomOffset, tool.randomOffset);
                float oz = Random.Range(-tool.randomOffset, tool.randomOffset);
                float px = x + ox;
                float pz = z + oz;

                Vector2 pos2D = new Vector2(px, pz);

                // ✅ Verifica se está DENTRO do polígono
                if (!PointInPolygon(pos2D, poly2D))
                    continue;

                // Clareira
                if (tool.clearingRadius > 0f && Vector2.Distance(pos2D, clearCenter) < tool.clearingRadius)
                    continue;

                // Distância mínima
                bool tooClose = false;
                foreach (var placed in placedPositions)
                {
                    if (Vector2.Distance(pos2D, placed) < tool.minDistanceBetweenTrees)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Y via Raycast
                float py = origin.y;
                if (tool.snapToGround)
                {
                    Ray ray = new Ray(new Vector3(px, origin.y + tool.raycastHeight, pz), Vector3.down);
                    if (Physics.Raycast(ray, out RaycastHit hit, tool.raycastHeight * 2f, tool.groundLayer))
                        py = hit.point.y;
                }

                GameObject prefab = tool.treePrefabs[Random.Range(0, tool.treePrefabs.Length)];
                if (prefab == null) continue;

                Vector3 spawnPos = new Vector3(px, py, pz);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                if (instance == null)
                    instance = Instantiate(prefab, container.transform);

                instance.transform.position = spawnPos;

                float rotY = tool.randomRotation ? Random.Range(0f, 360f) : 0f;
                Quaternion correction = Quaternion.Euler(tool.rotationCorrection);
                Quaternion randomY = Quaternion.Euler(0f, rotY, 0f);
                instance.transform.rotation = randomY * correction;

                if (tool.randomScale)
                {
                    float s = Random.Range(tool.scaleMin, tool.scaleMax);
                    instance.transform.localScale = Vector3.one * s;
                }

                placedPositions.Add(pos2D);
            }
        }

        Debug.Log($"[ForestLayoutTool] ✅ {placedPositions.Count} árvores geradas!");
        EditorUtility.SetDirty(tool);
    }

    // ─────────────────────────────────────────────
    //  POINT IN POLYGON — Ray Casting Algorithm
    // ─────────────────────────────────────────────
    static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        int n = polygon.Count;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = polygon[i].x, yi = polygon[i].y;
            float xj = polygon[j].x, yj = polygon[j].y;

            bool intersect = ((yi > point.y) != (yj > point.y)) &&
                             (point.x < (xj - xi) * (point.y - yi) / (yj - yi) + xi);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    void ClearForest(ForestLayoutTool tool)
    {
        Transform existing = tool.transform.Find(GetContainerName());
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
            Debug.Log("[ForestLayoutTool] 🗑️ Floresta anterior removida.");
        }
    }

    string GetContainerName() => "_ForestGenerated";
}
#endif