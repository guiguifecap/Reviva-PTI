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

    [Header("Distância dos Alvos por Dificuldade")]
    [Tooltip("Spawner que controla os alvos. Se vazio, é procurado na cena.")]
    public TargetSpawner targetSpawner;
    [Tooltip("Transform do player (câmera do XR Origin). Se vazio, usa a Main Camera.")]
    public Transform jogador;
    [Tooltip("Dificuldade LEVE (0.4): quantos metros os alvos ficam MAIS PERTO do player que na posição original.")]
    public float aproximarNoLeve = 1.5f;
    [Tooltip("Dificuldade DIFÍCIL (1.0): quantos metros os alvos ficam MAIS LONGE do player que na posição original.")]
    public float afastarNoDificil = 2f;
    [Tooltip("Menor distância (m) que os alvos podem chegar do player ao aproximar.")]
    public float distanciaMinimaDoPlayer = 1f;
    [Tooltip("Se ligado e o paciente estiver calibrado, a distância = alcance calibrado × dificuldade e os valores acima são IGNORADOS. Deixe desligado para usar Leve / Regular / Difícil.")]
    public bool basearNoAlcanceCalibrado = false;
    [Tooltip("Escreve no Console o deslocamento aplicado a cada mudança de dificuldade.")]
    public bool mostrarLogsDeDistancia = true;

    [Header("Componentes do Jogo")]
    public GoniometriaGenerico goniometria;

    [Header("Calibração (opcional: pode deixar vazio)")]
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

    // Pontos de dificuldade: Leve = 0.4, Regular = 0.7 (posição original), Difícil = 1.0
    private const float DificuldadeLeve = 0.4f;
    private const float DificuldadeRegular = 0.7f;
    private const float DificuldadeDificil = 1f;

    private float dificuldadeAtual = 0.7f;
    private bool referenciasProntas = false;

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
            difficultySlider.minValue = DificuldadeLeve;
            difficultySlider.maxValue = DificuldadeDificil;
            difficultySlider.wholeNumbers = false;
            difficultySlider.value = 0.7f;
            difficultySlider.onValueChanged.AddListener(OnSliderChanged);
        }

        SetAdvancedMode(false);
        SelecionarDificuldade(0.7f, toggleRegular);

        // ── O resto só faz sentido na cena do jogo ──
        // Se existe um TargetSpawner na cena, estamos na cena do jogo (mesmo que o nome seja outro).
        if (targetSpawner == null)
            targetSpawner = FindObjectOfType<TargetSpawner>();

        bool naCenaDoJogo = targetSpawner != null ||
                            SceneManager.GetActiveScene().name == cenaJogo;

        if (!naCenaDoJogo)
        {
            Debug.Log($"[MenuUI2] Cena '{SceneManager.GetActiveScene().name}' não é a do jogo " +
                      $"('{cenaJogo}') e não há TargetSpawner: configuração de alvos ignorada.");
            return;
        }

        if (goniometria == null)
            goniometria = FindObjectOfType<GoniometriaGenerico>();

        if (jogador == null && Camera.main != null)
            jogador = Camera.main.transform;

        // Plano B: qualquer câmera ativa (caso a câmera do VR não tenha a tag MainCamera)
        if (jogador == null)
        {
            Camera qualquerCamera = FindObjectOfType<Camera>();
            if (qualquerCamera != null) jogador = qualquerCamera.transform;
        }

        if (targetSpawner == null)
            Debug.LogWarning("[MenuUI2] TargetSpawner não encontrado na cena!");

        if (jogador == null)
            Debug.LogWarning("[MenuUI2] Player não encontrado! Arraste a câmera do XR Origin no campo 'Jogador'.");

        if (goniometria != null)
            goniometria.OnCalibracaoConcluida += AtualizarDistanciaDosAlvos;
        else
            Debug.LogWarning("[MenuUI2] GoniometriaGenerico não encontrada na cena!");

        referenciasProntas = targetSpawner != null && jogador != null;

        // aplica a distância inicial (Regular = posição original)
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
        else
            Debug.LogWarning("[MenuUI2] CalibrarPaciente: GoniometriaGenerico não atribuída.");
    }

    // Todos os campos de UI são opcionais: não dá erro se algum estiver vazio
    void AtualizarStatus()
    {
        if (goniometria.calibrando)
            MostrarStatus(goniometria.faseAtual, false, goniometria.progressoCalibracao);
        else if (goniometria.calibrado)
            MostrarStatus($"✔ Calibrado ({goniometria.alcanceMaximo * 100f:F0} cm)", true, 1f);
        else
            MostrarStatus("⚠ Não calibrado", false, 0f);
    }

    void MostrarStatus(string texto, bool ok, float progresso)
    {
        if (statusCalibracao != null) statusCalibracao.text = texto;
        if (statusToggle != null) statusToggle.isOn = ok;
        if (barraProgressoCalibracao != null) barraProgressoCalibracao.value = progresso;
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

    /// <summary>
    /// Deslocamento (em metros) em relação à posição original dos alvos.
    /// Regular (0.7) = 0 | Leve (0.4) = -aproximarNoLeve | Difícil (1.0) = +afastarNoDificil.
    /// Valores intermediários do slider são interpolados.
    /// </summary>
    float CalcularDeslocamento()
    {
        if (dificuldadeAtual < DificuldadeRegular)
        {
            // 0 no Regular -> 1 no Leve
            float t = Mathf.InverseLerp(DificuldadeRegular, DificuldadeLeve, dificuldadeAtual);
            return -aproximarNoLeve * t;
        }

        // 0 no Regular -> 1 no Difícil
        float t2 = Mathf.InverseLerp(DificuldadeRegular, DificuldadeDificil, dificuldadeAtual);
        return afastarNoDificil * t2;
    }

    public void AtualizarDistanciaDosAlvos()
    {
        if (!referenciasProntas) return;

        // Modo opcional: distância pelo alcance calibrado do paciente
        bool usarCalibrado = basearNoAlcanceCalibrado && goniometria != null && goniometria.calibrado;
        if (usarCalibrado)
        {
            float alcance = goniometria.alcanceMaximo; // em metros
            if (GameSettings.Instance != null && GameSettings.Instance.usarMetadeDoAlcance)
                alcance *= 0.5f;

            float distancia = alcance * dificuldadeAtual;
            targetSpawner.SetSpawnDistance(jogador.position, distancia);

            if (mostrarLogsDeDistancia)
                Debug.Log($"[MenuUI2] Dificuldade {dificuldadeAtual:F2} -> alvos a {distancia:F2} m do player (alcance calibrado).");
            return;
        }

        float deslocamento = CalcularDeslocamento();
        targetSpawner.SetSpawnOffset(jogador.position, deslocamento, distanciaMinimaDoPlayer);

        if (mostrarLogsDeDistancia)
        {
            string descricao = Mathf.Abs(deslocamento) < 0.01f ? "posição original"
                             : deslocamento > 0f ? $"{deslocamento:F1} m mais longe"
                                                 : $"{-deslocamento:F1} m mais perto";
            Debug.Log($"[MenuUI2] Dificuldade {dificuldadeAtual:F2} -> alvos: {descricao}.");
        }
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

        if (goniometria != null)
            goniometria.ResetarSessao();
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