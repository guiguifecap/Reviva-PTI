using UnityEngine;

public class BotWGrass : MonoBehaviour
{
    [SerializeField] private WindZone windZone;
    [SerializeField] private float cullingBoundsSize = 10000f; // tamanho da "caixa" de culling

    private new Renderer renderer;

    private void Awake()
    {
        renderer = GetComponent<Renderer>();
        DisableCulling();
    }

    private void DisableCulling()
    {
        // MeshRenderer / grama normal: aumenta os bounds do mesh (cria uma cópia só para esse objeto)
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            Mesh mesh = meshFilter.mesh;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * cullingBoundsSize);
        }

        // SkinnedMeshRenderer: aumenta os bounds locais
        if (renderer is SkinnedMeshRenderer skinned)
        {
            skinned.updateWhenOffscreen = true;
            skinned.localBounds = new Bounds(Vector3.zero, Vector3.one * cullingBoundsSize);
        }
    }

    private void Update()
    {
        Vector3 velocity = windZone.transform.forward * windZone.windMain;
        renderer.material.SetVector("_WindVelocity", velocity);
        renderer.material.SetFloat("_WindFrequency", windZone.windPulseFrequency);
    }
}