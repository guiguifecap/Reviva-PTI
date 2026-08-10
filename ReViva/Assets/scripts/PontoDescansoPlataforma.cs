using UnityEngine;

/// <summary>
/// Torna a colisão deste objeto "de mão única":
/// - Se o player estiver por baixo (ou entrando por baixo), a colisão é ignorada e ele passa por dentro.
/// - Assim que os pés do player alcançam o topo do objeto, a colisão normal é restaurada e ele fica em cima sem cair.
///
/// Uso: coloque este script no mesmo GameObject que tem o Collider do ponto de descanso
/// (o prefab que é instanciado pelo Degrais.cs quando "ehPontoDeDescanso" é true).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PontoDescansoPlataforma : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Tag usada para encontrar o player automaticamente.")]
    public string tagPlayer = "Player";
    [Tooltip("Arraste o player aqui se preferir referenciar manualmente em vez de usar a tag.")]
    public Transform playerManual;

    [Header("Comportamento")]
    [Tooltip("Margem acima do topo do objeto considerada 'em cima'. Evita flickering bem na borda.")]
    public float margemSuperior = 0.05f;
    [Tooltip("Se true, também exige que o player esteja caindo/parado (não subindo) para travar a colisão. Útil se o player pode pular por dentro do objeto.")]
    public bool considerarVelocidadeVertical = true;

    Collider colisorPlataforma;
    Collider colisorPlayer;
    Rigidbody rbPlayer;
    Transform player;

    // evita chamar Physics.IgnoreCollision toda hora sem necessidade
    bool? colisaoAtivaAtual = null;

    void Awake()
    {
        colisorPlataforma = GetComponent<Collider>();
    }

    void Start()
    {
        CachearPlayer();
    }

    void CachearPlayer()
    {
        if (playerManual != null)
        {
            player = playerManual;
        }
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(tagPlayer);
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (player == null)
        {
            Debug.LogWarning($"[PontoDescansoPlataforma] Player não encontrado (tag '{tagPlayer}').");
            return;
        }

        colisorPlayer = player.GetComponent<Collider>();
        rbPlayer = player.GetComponent<Rigidbody>();

        if (colisorPlayer == null)
            Debug.LogWarning("[PontoDescansoPlataforma] Player não tem Collider.");
    }

    void FixedUpdate()
    {
        // tenta recachear caso o player tenha sido instanciado depois deste objeto
        if (player == null || colisorPlayer == null)
        {
            CachearPlayer();
            if (player == null || colisorPlayer == null)
                return;
        }

        float topoPlataforma = colisorPlataforma.bounds.max.y;
        float pesPlayer = colisorPlayer.bounds.min.y;

        bool jogadorAcimaDoTopo = pesPlayer >= topoPlataforma - margemSuperior;

        bool subindo = false;
        if (considerarVelocidadeVertical && rbPlayer != null)
            subindo = rbPlayer.linearVelocity.y > 0.05f;

        // só trava a colisão (fica sólido) quando o player está em cima
        // e não está subindo por dentro do objeto
        bool deveColidir = jogadorAcimaDoTopo && !subindo;

        AplicarColisao(deveColidir);
    }

    void AplicarColisao(bool deveColidir)
    {
        // só chama Physics.IgnoreCollision quando o estado realmente muda
        if (colisaoAtivaAtual.HasValue && colisaoAtivaAtual.Value == deveColidir)
            return;

        Physics.IgnoreCollision(colisorPlayer, colisorPlataforma, !deveColidir);
        colisaoAtivaAtual = deveColidir;
    }

    void OnDisable()
    {
        // ao desativar o objeto (ex: pool de objetos), garante que a colisão volte ao normal
        if (colisorPlayer != null && colisorPlataforma != null)
        {
            Physics.IgnoreCollision(colisorPlayer, colisorPlataforma, false);
            colisaoAtivaAtual = null;
        }
    }
}
