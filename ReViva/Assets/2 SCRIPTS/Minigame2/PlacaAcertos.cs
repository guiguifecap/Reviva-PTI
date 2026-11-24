using TMPro;
using UnityEngine;

/// <summary>
/// Atualiza um texto (TMP) do SEU canvas da placa com "Acertos X/Y",
/// lendo HitsSoFar / HitsToWin do TargetSpawner.
/// Pode ser colocado no próprio Canvas da placa ou em qualquer objeto da cena.
/// </summary>
public class PlacaAcertos : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Texto TMP do canvas da placa. Se vazio, procura nos filhos deste objeto.")]
    [SerializeField] private TMP_Text texto;
    [Tooltip("Se vazio, procura o TargetSpawner na cena.")]
    [SerializeField] private TargetSpawner targetSpawner;

    [Header("Texto")]
    [Tooltip("{0} = acertos feitos, {1} = total para vencer. Use \\n para quebrar linha.")]
    [SerializeField] private string formato = "Acertos\n{0}/{1}";

    [Header("Cores")]
    [Tooltip("Se desligado, mantém a cor que você já definiu no texto.")]
    [SerializeField] private bool mudarCor = true;
    [SerializeField] private Color corVitoria = new Color(0.3f, 1f, 0.3f);

    private Color corOriginal;
    private int ultimoFeitos = -1;
    private int ultimoTotal = -1;

    private void Awake()
    {
        if (texto == null)
            texto = GetComponentInChildren<TMP_Text>(true);

        if (texto != null)
            corOriginal = texto.color;
        else
            Debug.LogWarning("[PlacaAcertos] Nenhum texto TMP encontrado! Arraste o texto da placa no campo 'Texto'.", this);
    }

    private void Start()
    {
        if (targetSpawner == null)
            targetSpawner = FindFirstObjectByType<TargetSpawner>();

        if (targetSpawner == null)
            Debug.LogWarning("[PlacaAcertos] TargetSpawner não encontrado na cena!", this);

        Atualizar(true);
    }

    private void Update()
    {
        Atualizar(false);
    }

    private void Atualizar(bool forcar)
    {
        if (texto == null) return;

        int feitos = targetSpawner != null ? targetSpawner.HitsSoFar : 0;
        int total = targetSpawner != null ? targetSpawner.HitsToWin : 0;

        // Só mexe no texto quando o valor muda
        if (!forcar && feitos == ultimoFeitos && total == ultimoTotal) return;
        ultimoFeitos = feitos;
        ultimoTotal = total;

        texto.text = string.Format(formato.Replace("\\n", "\n"), feitos, total);

        if (mudarCor)
            texto.color = (total > 0 && feitos >= total) ? corVitoria : corOriginal;
    }
}