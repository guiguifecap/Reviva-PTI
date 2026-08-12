using UnityEngine;

/// <summary>
/// Anexado automaticamente pelo Degrais.cs em cada pedra/ponto de descanso gerado.
/// Detecta quando o player toca essa pedra e avisa o ProgressoEscalada
/// para atualizar a UI (ex: "3/15"), resetando quando é um ponto de descanso.
/// </summary>
public class PedraProgresso : MonoBehaviour
{
    // preenchidos pelo Degrais.cs logo após o Instantiate
    [HideInInspector] public int numeroNaSerie;
    [HideInInspector] public int totalNaSerie;
    [HideInInspector] public bool ehPontoDeDescanso;

    [Header("Detecção de Toque")]
    [Tooltip("Tag usada para identificar o player (mão, pé, corpo etc).")]
    public string tagPlayer = "Player";
    [Tooltip("Raio do gatilho de detecção, criado automaticamente sobre a pedra.")]
    public float raioDeteccao = 0.15f;
    [Tooltip("Se true, só dispara a atualização de UI uma vez por pedra (evita repetir se o player encostar várias vezes).")]
    public bool dispararApenasUmaVez = true;

    bool jaDisparou = false;

    void Awake()
    {
        // cria um trigger extra apenas para detecção, sem alterar a colisão
        // física existente na pedra (a que o player fica em cima/segura)
        SphereCollider gatilho = gameObject.AddComponent<SphereCollider>();
        gatilho.isTrigger = true;
        gatilho.radius = raioDeteccao;
    }

    void OnTriggerEnter(Collider other)
    {
        if (dispararApenasUmaVez && jaDisparou)
            return;

        if (!other.CompareTag(tagPlayer))
            return;

        jaDisparou = true;

        if (ProgressoEscalada.Instance == null)
        {
            Debug.LogWarning("[PedraProgresso] Nenhum ProgressoEscalada encontrado na cena.");
            return;
        }

        if (ehPontoDeDescanso)
            ProgressoEscalada.Instance.ResetarSerie(totalNaSerie);
        else
            ProgressoEscalada.Instance.AtualizarProgresso(numeroNaSerie, totalNaSerie);
    }

    // permite tocar a mesma pedra de novo depois de sair, se quiser reativar manualmente
    public void ResetarDisparo()
    {
        jaDisparou = false;
    }
}
