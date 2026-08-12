using UnityEngine;
using TMPro;

/// <summary>
/// Controla a UI que mostra em qual pedra da série o player está (ex: "3/15"),
/// resetando ao alcançar um ponto de descanso.
///
/// Coloque este script em um GameObject qualquer da Canvas (ex: "UI_Progresso")
/// e arraste o campo de texto (TextMeshPro) no Inspector.
/// </summary>
public class ProgressoEscalada : MonoBehaviour
{
    public static ProgressoEscalada Instance;

    [Header("UI")]
    [Tooltip("Texto (TextMeshPro - UGUI) que mostra o progresso, ex: '3/15'.")]
    public TMP_Text textoProgresso;

    [Header("Formatação")]
    [Tooltip("Formato usado durante a subida normal. {0} = pedra atual, {1} = total da série.")]
    public string formatoProgresso = "{0}/{1}";
    [Tooltip("Formato usado assim que a série é resetada num ponto de descanso. {0} = total da próxima série.")]
    public string formatoDescanso = "0/{0}";

    void Awake()
    {
        Instance = this;
    }

    /// <summary>Chamado a cada pedra normal tocada dentro da série atual.</summary>
    public void AtualizarProgresso(int atual, int total)
    {
        if (textoProgresso == null) return;
        textoProgresso.text = string.Format(formatoProgresso, atual, total);
    }

    /// <summary>Chamado ao tocar um ponto de descanso: reinicia a contagem da próxima série.</summary>
    public void ResetarSerie(int totalProximaSerie)
    {
        if (textoProgresso == null) return;
        textoProgresso.text = string.Format(formatoDescanso, totalProximaSerie);
    }
}
