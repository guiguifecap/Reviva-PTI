using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI_Jogo : MonoBehaviour
{
    [Header("Configuração de Dificuldade")]
    public Slider difficultySlider;
    public TextMeshProUGUI difficultyText;

    [Header("Componentes do Jogo")]
    public Degrais degrais;
    public GoniometriaClimb goniometria;

    [Header("Calibração")]
    public TextMeshProUGUI statusCalibracao;
    public Slider barraProgressoCalibracao;

    [Header("Tempo Real")]
    public TextMeshProUGUI usoAtualDireito;
    public TextMeshProUGUI usoAtualEsquerdo;

    [Header("Resultados Clínicos")]
    public TextMeshProUGUI resultadoDireito;
    public TextMeshProUGUI resultadoEsquerdo;
    public TextMeshProUGUI diagnostico;

    [Header("Cenas")]
    public string cenaMenu = "Menu";
    public string cenaJogo = "EscaladaPrototipo";

    [Header("Modo de Alcance")]
    public Toggle toggleMetadeAlcance;

    void Start()
    {
        // ── Toggle de alcance (pode existir em qualquer cena) ──
        if (toggleMetadeAlcance != null)
            toggleMetadeAlcance.onValueChanged.AddListener(OnToggleAlcance);

        // ── Slider de dificuldade: configura ANTES do guard ──
        if (difficultySlider != null)
        {
            difficultySlider.minValue = 0.4f;
            difficultySlider.maxValue = 1f;
            difficultySlider.wholeNumbers = false;
            difficultySlider.value = 0.7f; // <-- isso agora sempre executa
        }

        AtualizarTextoDificuldade(); // <-- idem

        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = 0.7f;

        // ── Guard: o resto só faz sentido na cena do jogo ──
        if (SceneManager.GetActiveScene().name != cenaJogo) return;

        // Busca goniometria se não foi assignada no Inspector
        if (goniometria == null)
            goniometria = FindObjectOfType<GoniometriaClimb>();

        // Busca degrais se não foi assignado no Inspector
        if (degrais == null)
            degrais = FindObjectOfType<Degrais>();

        // Registra callback de calibração
        if (goniometria != null)
            goniometria.OnCalibracaoConcluida += RegenerarDegraus;
        else
            Debug.LogWarning("[MenuUI] GoniometriaClimb não encontrada na cena!");

        if (degrais == null)
            Debug.LogWarning("[MenuUI] Degrais não encontrado na cena!");
    }

    void Update()
    {
        if (goniometria == null) return;
        AtualizarStatus();
        AtualizarTempoReal();
    }

    void OnDestroy()
    {
        // Boa prática: desregistrar o evento ao destruir o objeto
        if (goniometria != null)
            goniometria.OnCalibracaoConcluida -= RegenerarDegraus;
    }

    // ── Calibração ──────────────────────────────────────────
    public void CalibrarPaciente()
    {
        if (goniometria != null)
            goniometria.IniciarCalibracaoManual();
    }

    void AtualizarStatus()
    {
        if (goniometria.calibrando)
        {
            statusCalibracao.text = goniometria.faseAtual;
            if (barraProgressoCalibracao != null)
                barraProgressoCalibracao.value = goniometria.progressoCalibracao;
        }
        else if (goniometria.calibrado)
        {
            statusCalibracao.text = $"✔ Calibrado ({goniometria.alcanceMaximo * 100f:F0} cm)";
            if (barraProgressoCalibracao != null)
                barraProgressoCalibracao.value = 1f;
        }
        else
        {
            statusCalibracao.text = "⚠ Não calibrado";
            if (barraProgressoCalibracao != null)
                barraProgressoCalibracao.value = 0f;
        }
    }

    // ── Tempo real ──────────────────────────────────────────
    void AtualizarTempoReal()
    {
        if (!goniometria.calibrado) return;
        usoAtualDireito.text = $"Dir: {goniometria.GetUsoAtualDir():F0}%";
        usoAtualEsquerdo.text = $"Esq: {goniometria.GetUsoAtualEsq():F0}%";
    }

    // ── Dificuldade ─────────────────────────────────────────
    public void OnSliderChanged()
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = difficultySlider.value;

        AtualizarTextoDificuldade();

        if (goniometria != null && goniometria.calibrado)
            RegenerarDegraus();
    }

    void AtualizarTextoDificuldade()
    {
        if (difficultyText == null) return; // evita NullRef se não assignado
        int porcentagem = Mathf.RoundToInt(difficultySlider != null ? difficultySlider.value * 100f : 70f);
        difficultyText.text = $"Dificuldade: {porcentagem}%";
    }

    // ── Degraus ─────────────────────────────────────────────
    public void RegenerarDegraus()
    {
        if (degrais == null)
        {
            Debug.LogWarning("[MenuUI] RegenerarDegraus: degrais é null!");
            return;
        }
        degrais.GerarDegraus();
    }

    // ── Resultados ──────────────────────────────────────────
    public void FinalizarSessao()
    {
        if (goniometria == null) return;
        var r = goniometria.GetResultados();
        resultadoDireito.text = $"Direito: {r.percDireito:F1}% ({r.alcanceMaximoCM:F0}cm máx)";
        resultadoEsquerdo.text = $"Esquerdo: {r.percEsquerdo:F1}% ({r.alcanceMaximoCM:F0}cm máx)";
        diagnostico.text = r.diagnostico;
    }

    // ── Toggle alcance ──────────────────────────────────────
    public void OnToggleAlcance(bool metade)
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.usarMetadeDoAlcance = metade;

        if (Degrais.Instance != null)
            Degrais.Instance.GerarDegraus();
    }
}