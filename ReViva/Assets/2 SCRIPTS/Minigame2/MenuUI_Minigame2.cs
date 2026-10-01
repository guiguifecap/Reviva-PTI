using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI_Minigame2 : MonoBehaviour
{
    [Header("Dificuldade")]
    public Toggle toggleLight;
    public Toggle toggleRegular;
    public Toggle toggleHard;

    public GameObject difficultyNormalPanel;
    public GameObject difficultyAdvancedPanel;

    public Slider difficultySlider;
    public TextMeshProUGUI difficultyText;
    private bool advancedDifficulty = false;

    [Header("Distância dos Alvos")]
    [Tooltip("Spawner que controla os alvos.")]
    public TargetSpawner targetSpawner;
    [Tooltip("Transform do player (câmera do XR Origin). Se vazio, usa a Main Camera.")]
    public Transform jogador;
    [Tooltip("Distância dos alvos com dificuldade mínima (0.4), em metros.")]
    public float distanciaMinima = 2f;
    [Tooltip("Distância dos alvos com dificuldade máxima (1.0), em metros.")]
    public float distanciaMaxima = 6f;
    [Tooltip("Se ligado e o paciente estiver calibrado, a distância = alcance calibrado × dificuldade (igual ao minigame 1). Se desligado, usa mínima/máxima acima.")]
    public bool basearNoAlcanceCalibrado = true;

    [Header("Componentes do Jogo")]
    public GoniometriaClimb goniometria;

    [Header("Calibração")]
    public TextMeshProUGUI statusCalibracao;
    public Slider barraProgressoCalibracao;
    public Toggle statusToggle;

    [Header("Desempenho (Tempo Real + Resultado Final)")]
    public TextMeshProUGUI textoDireito;
    public TextMeshProUGUI textoEsquerdo;
    private bool sessaoFinalizada = false;

    [Header("Cenas")]
    public string cenaMenu = "Menu";
    public string cenaJogo = "Minigame2";

    [Header("Modo de Alcance")]
    public Toggle toggleMetadeAlcance;

    [Header("Panel Pause")]
    public GameObject PanelPause;
    public bool isPause;

    private float dificuldadeAtual = 0.7f;

    void Start()
    {
        if (toggleMetadeAlcance != null)
            toggleMetadeAlcance.onValueChanged.AddListener(OnToggleAlcance);

        AtualizarTextoDificuldade();

        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = 0.7f;

        if (toggleLight != null) toggleLight.onValueChanged.AddListener(OnLightSelected);
        if (toggleRegular != null) toggleRegular.onValueChanged.AddListener(OnRegularSelected);
        if (toggleHard != null) toggleHard.onValueChanged.AddListener(OnHardSelected);

        if (difficultySlider != null)
        {
            difficultySlider.minValue = 0.4f;
            difficultySlider.maxValue = 1f;
            difficultySlider.wholeNumbers = false;
            difficultySlider.value = 0.7f;
            difficultySlider.onValueChanged.AddListener(OnSliderChanged);
        }

        SetAdvancedMode(false);
        SelecionarDificuldade(0.7f, toggleRegular);

        // ── Guard: o resto só faz sentido na cena do jogo ──
        if (SceneManager.GetActiveScene().name != cenaJogo)
        {
            Debug.LogWarning(
                $"[MenuUI2] Start() interrompido pelo guard de cena: cena ativa é " +
                $"'{SceneManager.GetActiveScene().name}', mas 'cenaJogo' é '{cenaJogo}'.");
            return;
        }

        if (goniometria == null)
            goniometria = FindObjectOfType<GoniometriaClimb>();

        if (targetSpawner == null)
            targetSpawner = FindObjectOfType<TargetSpawner>();

        if (jogador == null && Camera.main != null)
            jogador = Camera.main.transform;

        if (targetSpawner == null)
            Debug.LogWarning("[MenuUI2] TargetSpawner não encontrado na cena!");

        if (goniometria != null)
            goniometria.OnCalibracaoConcluida += AtualizarDistanciaDosAlvos;
        else
            Debug.LogWarning("[MenuUI2] GoniometriaClimb não encontrada na cena!");

        // aplica a distância inicial
        AtualizarDistanciaDosAlvos();
    }

    void Update()
    {
        // Pausa funciona mesmo sem goniometria
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isPause = !isPause;
            if (PanelPause != null) PanelPause.SetActive(isPause);
        }

        if (goniometria == null) return;
        AtualizarStatus();
        AtualizarTempoReal();
    }

    void OnDestroy()
    {
        if (goniometria != null)
            goniometria.OnCalibracaoConcluida -= AtualizarDistanciaDosAlvos;
    }

    // ── Botões ─────────────────────────────────────────────
    public void ConfigButtonOpen()
    {
        PanelPause.SetActive(true);
        isPause = true;
    }

    public void ConfigButtonClose()
    {
        PanelPause.SetActive(false);
        isPause = false;
    }

    // ── Calibração ──────────────────────────────────────────
    public void CalibrarPaciente()
    {
        sessaoFinalizada = false;

        if (goniometria != null)
            goniometria.IniciarCalibracaoManual();
    }

    void AtualizarStatus()
    {
        if (goniometria.calibrando)
        {
            statusCalibracao.text = goniometria.faseAtual;
            statusToggle.isOn = false;
            if (barraProgressoCalibracao != null)
                barraProgressoCalibracao.value = goniometria.progressoCalibracao;
        }
        else if (goniometria.calibrado)
        {
            statusCalibracao.text = $"✔ Calibrado ({goniometria.alcanceMaximo * 100f:F0} cm)";
            statusToggle.isOn = true;
            if (barraProgressoCalibracao != null)
                barraProgressoCalibracao.value = 1f;
        }
        else
        {
            statusCalibracao.text = "⚠ Não calibrado";
            statusToggle.isOn = false;
            if (barraProgressoCalibracao != null)
                barraProgressoCalibracao.value = 0f;
        }
    }

    // ── Tempo real ──────────────────────────────────────────
    void AtualizarTempoReal()
    {
        if (sessaoFinalizada) return;
        if (!goniometria.calibrado) return;

        if (textoDireito != null)
            textoDireito.text = $"Dir: {goniometria.GetUsoAtualDir():F0}%";

        if (textoEsquerdo != null)
            textoEsquerdo.text = $"Esq: {goniometria.GetUsoAtualEsq():F0}%";
    }

    // ── Dificuldade ─────────────────────────────────────────
    public void OnSliderChanged(float value)
    {
        dificuldadeAtual = value;

        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = value;

        AtualizarTextoDificuldade();
        AtualizarDistanciaDosAlvos();
    }

    void AtualizarTextoDificuldade()
    {
        if (difficultyText == null) return;
        int porcentagem = Mathf.RoundToInt(difficultySlider != null ? difficultySlider.value * 100f : 70f);
        difficultyText.text = $"Dificuldade: {porcentagem}%";
    }

    public void OnLightSelected(bool selected) { if (selected) SelecionarDificuldade(0.4f, toggleLight); }
    public void OnRegularSelected(bool selected) { if (selected) SelecionarDificuldade(0.7f, toggleRegular); }
    public void OnHardSelected(bool selected) { if (selected) SelecionarDificuldade(1.0f, toggleHard); }

    void SelecionarDificuldade(float valor, Toggle selecionado)
    {
        dificuldadeAtual = valor;

        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = valor;

        if (toggleLight != null && toggleLight != selecionado) toggleLight.isOn = false;
        if (toggleRegular != null && toggleRegular != selecionado) toggleRegular.isOn = false;
        if (toggleHard != null && toggleHard != selecionado) toggleHard.isOn = false;

        if (selecionado != null)
            selecionado.isOn = true;

        if (difficultySlider != null)
            difficultySlider.value = valor;

        AtualizarTextoDificuldade();
        AtualizarDistanciaDosAlvos();
    }

    public void ToggleAdvancedDifficulty()
    {
        advancedDifficulty = !advancedDifficulty;
        SetAdvancedMode(advancedDifficulty);
    }

    void SetAdvancedMode(bool advanced)
    {
        if (difficultyNormalPanel != null) difficultyNormalPanel.SetActive(!advanced);
        if (difficultyAdvancedPanel != null) difficultyAdvancedPanel.SetActive(advanced);
        if (difficultySlider != null) difficultySlider.interactable = advanced;
    }

    // ── Distância dos alvos ─────────────────────────────────
    float CalcularDistancia()
    {
        bool usarCalibrado = basearNoAlcanceCalibrado && goniometria != null && goniometria.calibrado;

        if (usarCalibrado)
        {
            float alcance = goniometria.alcanceMaximo; // em metros
            if (GameSettings.Instance != null && GameSettings.Instance.usarMetadeDoAlcance)
                alcance *= 0.5f;
            return alcance * dificuldadeAtual;
        }

        float t = Mathf.InverseLerp(0.4f, 1f, dificuldadeAtual);
        return Mathf.Lerp(distanciaMinima, distanciaMaxima, t);
    }

    public void AtualizarDistanciaDosAlvos()
    {
        if (targetSpawner == null || jogador == null) return;
        targetSpawner.SetSpawnDistance(jogador.position, CalcularDistancia());
    }

    // ── Resultados ──────────────────────────────────────────
    public void FinalizarSessao()
    {
        if (goniometria == null) return;

        var r = goniometria.GetResultados();
        sessaoFinalizada = true;

        if (textoDireito != null)
            textoDireito.text =
                $"Direito: {r.percDireito:F1}% ({r.alcanceMaximoCM:F0}cm máx)\n{r.diagnostico}";

        if (textoEsquerdo != null)
            textoEsquerdo.text =
                $"Esquerdo: {r.percEsquerdo:F1}% ({r.alcanceMaximoCM:F0}cm máx)\n{r.diagnostico}";
    }

    public void ReiniciarParaNovaSessao()
    {
        sessaoFinalizada = false;
    }

    // ── Toggle alcance ──────────────────────────────────────
    public void OnToggleAlcance(bool metade)
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.usarMetadeDoAlcance = metade;

        AtualizarDistanciaDosAlvos();
    }

    public void botaoSair()
    {
        SceneManager.LoadScene("Menu");
    }

    public void botaoSairEscolha()
    {
        SceneManager.LoadScene("MenuEscolha");
    }
}