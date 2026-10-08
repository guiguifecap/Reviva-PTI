using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI_Minigame2 : MonoBehaviour
{
    [Header("Dificuldade (modo normal)")]
    public Toggle toggleLight;
    public Toggle toggleRegular;
    public Toggle toggleHard;
    public TextMeshProUGUI difficultyText;

    public GameObject difficultyNormalPanel;
    public GameObject difficultyAdvancedPanel;
    private bool advancedDifficulty = false;

    [Header("Modo Avançado (campos manuais)")]
    [Tooltip("Distância (m) exata do Empty de referência até os alvos (2 a 10). Content Type: Decimal Number.")]
    public TMP_InputField inputDistancia;
    [Tooltip("Quantos alvos precisam ser acertados para vencer. Content Type: Integer Number.")]
    public TMP_InputField inputAlvosParaVencer;
    [Tooltip("Quantos alvos podem aparecer ao mesmo tempo. Content Type: Integer Number.")]
    public TMP_InputField inputAlvosSimultaneos;
    public int maxAlvosParaVencer = 99;
    public int maxAlvosSimultaneos = 10;

    [Header("Distância dos Alvos")]
    [Tooltip("Spawner que controla os alvos. Se vazio, é procurado na cena.")]
    public TargetSpawner targetSpawner;
    [Tooltip("Empty posicionado onde o player fica. Os alvos aparecem à frente dele (eixo Z azul / forward), exatamente à distância configurada.")]
    public Transform pontoReferencia;
    [Tooltip("Distância mínima (m) permitida.")]
    public float distanciaMinima = 2f;
    [Tooltip("Distância máxima (m) permitida.")]
    public float distanciaMaximaMetros = 10f;
    [Tooltip("Distância (m) do preset LEVE.")]
    public float distanciaLeve = 3f;
    [Tooltip("Distância (m) do preset REGULAR.")]
    public float distanciaRegular = 5f;
    [Tooltip("Distância (m) do preset DIFÍCIL.")]
    public float distanciaDificil = 7f;
    public bool mostrarLogsDeDistancia = true;

    [Header("Presets: Alvos para vencer")]
    public int acertosLeve = 5;
    public int acertosRegular = 10;
    public int acertosDificil = 15;

    [Header("Presets: Alvos simultâneos")]
    public int simultaneosLeve = 2;
    public int simultaneosRegular = 3;
    public int simultaneosDificil = 4;

    [Header("Componentes do Jogo")]
    public GoniometriaGenerico goniometria;

    [Header("Calibração")]
    public Button botaoCalibrar;
    public TextMeshProUGUI textoBotaoCalibrar;
    public TextMeshProUGUI statusCalibracao;
    public Slider barraProgressoCalibracao;
    public Toggle statusToggle;

    [Header("Iniciar Tratamento")]
    public Button botaoIniciarTratamento;
    public TextMeshProUGUI textoBotaoIniciar;
    [Tooltip("Texto onde aparece a contagem regressiva (3, 2, 1...). Fica vazio quando não está contando.")]
    public TextMeshProUGUI textoContagem;
    [Tooltip("Quantos segundos de contagem depois de clicar em Iniciar.")]
    public int contagemInicial = 3;
    [Tooltip("Texto mostrado quando a contagem termina e os alvos começam.")]
    public string textoComecou = "JÁ!";
    [Tooltip("Por quantos segundos o texto acima fica na tela.")]
    public float tempoTextoComecou = 0.7f;

    [Header("Acertos")]
    [Tooltip("Mostra 'Acertos: X/Y' em tempo real.")]
    public TextMeshProUGUI textoAcertos;

    private Coroutine contagemRoutine;
    private Coroutine textoFinalRoutine;

    [Header("Desempenho (Tempo Real + Resultado Final)")]
    public TextMeshProUGUI textoDireito;
    public TextMeshProUGUI textoEsquerdo;
    private bool sessaoFinalizada = false;
    private bool tratamentoIniciouNoSpawner = false; // detecta quando o jogador vence

    [Header("Cenas")]
    public string cenaMenu = "Menu";
    public string cenaJogo = "Minigame2";

    [Header("Modo de Alcance")]
    public Toggle toggleMetadeAlcance;

    [Header("Panel Pause")]
    public GameObject PanelPause;
    public bool isPause;

    // Pontos de dificuldade: Leve = 0.4, Regular = 0.7, Difícil = 1.0
    private const float DificuldadeLeve = 0.4f;
    private const float DificuldadeRegular = 0.7f;
    private const float DificuldadeDificil = 1f;

    private float dificuldadeAtual = 0.7f;
    private bool referenciasProntas = false;

    // Valores efetivos (vêm do preset ou dos campos manuais)
    private float distanciaAtual = 5f;
    private int acertosAtual = 10;
    private int simultaneosAtual = 3;

    void Start()
    {
        if (toggleMetadeAlcance != null)
            toggleMetadeAlcance.onValueChanged.AddListener(OnToggleAlcance);

        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = 0.7f;

        if (toggleLight != null) toggleLight.onValueChanged.AddListener(OnLightSelected);
        if (toggleRegular != null) toggleRegular.onValueChanged.AddListener(OnRegularSelected);
        if (toggleHard != null) toggleHard.onValueChanged.AddListener(OnHardSelected);

        if (inputDistancia != null) inputDistancia.onEndEdit.AddListener(OnDistanciaEditada);
        if (inputAlvosParaVencer != null) inputAlvosParaVencer.onEndEdit.AddListener(OnAcertosEditado);
        if (inputAlvosSimultaneos != null) inputAlvosSimultaneos.onEndEdit.AddListener(OnSimultaneosEditado);

        // Estado inicial dos botões
        AtualizarBotaoCalibrar(false, false);
        AtualizarBotaoIniciar();

        SetAdvancedMode(false);
        SelecionarDificuldade(0.7f, toggleRegular);

        // ── O resto só faz sentido na cena do jogo ──
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

        if (targetSpawner == null)
            Debug.LogWarning("[MenuUI2] TargetSpawner não encontrado na cena!");

        if (pontoReferencia == null)
            Debug.LogWarning("[MenuUI2] Ponto de referência não atribuído! Arraste o Empty (posição do player) no campo 'Ponto Referencia'.");

        if (goniometria == null)
            Debug.LogWarning("[MenuUI2] GoniometriaGenerico não encontrada na cena!");

        referenciasProntas = targetSpawner != null && pontoReferencia != null;

        // Agora que o spawner e o player existem, aplica o preset (Regular) de verdade
        AplicarPreset();
    }

    void Update()
    {
        // Pausa funciona mesmo sem goniometria
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isPause = !isPause;
            if (PanelPause != null) PanelPause.SetActive(isPause);
        }

        AtualizarBotaoIniciar();
        AtualizarTextoAcertos();

        // Jogador venceu: o spawner parou sozinho -> mostra o máximo recorrente
        if (tratamentoIniciouNoSpawner && targetSpawner != null && !targetSpawner.HasStarted)
        {
            tratamentoIniciouNoSpawner = false;
            FinalizarSessao();
        }

        if (goniometria == null) return;
        AtualizarStatus();
        AtualizarTempoReal();
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

    // ── Iniciar / parar tratamento ──────────────────────────
    // Mantém o nome IniciarTratamento para não perder a ligação do OnClick no Inspector
    public void IniciarTratamento()
    {
        AlternarTratamento();
    }

    public void AlternarTratamento()
    {
        if (targetSpawner == null)
        {
            Debug.LogWarning("[MenuUI2] AlternarTratamento: TargetSpawner não encontrado.");
            return;
        }

        if (contagemRoutine != null)
        {
            // Clicou durante a contagem: cancela
            CancelarContagem();
        }
        else if (targetSpawner.HasStarted)
        {
            targetSpawner.StopTreatment();
            FinalizarSessao(); // mostra o máximo recorrente
        }
        else
        {
            if (textoFinalRoutine != null) { StopCoroutine(textoFinalRoutine); textoFinalRoutine = null; }
            contagemRoutine = StartCoroutine(ContagemEIniciar());
        }

        AtualizarBotaoIniciar();
    }

    IEnumerator ContagemEIniciar()
    {
        for (int i = Mathf.Max(1, contagemInicial); i >= 1; i--)
        {
            MostrarContagem(i.ToString());
            yield return new WaitForSeconds(1f);
        }

        // Garante que o spawner está com os valores atuais e libera os alvos
        AplicarNoSpawner();
        sessaoFinalizada = false;
        if (goniometria != null) goniometria.ResetarSessao();
        targetSpawner.StartTreatment();
        tratamentoIniciouNoSpawner = true;
        contagemRoutine = null;
        AtualizarBotaoIniciar();

        MostrarContagem(textoComecou);
        textoFinalRoutine = StartCoroutine(ApagarContagemDepois(tempoTextoComecou));
    }

    IEnumerator ApagarContagemDepois(float segundos)
    {
        yield return new WaitForSeconds(segundos);
        MostrarContagem("");
        textoFinalRoutine = null;
    }

    void CancelarContagem()
    {
        if (contagemRoutine != null)
        {
            StopCoroutine(contagemRoutine);
            contagemRoutine = null;
        }
        MostrarContagem("");
    }

    void MostrarContagem(string texto)
    {
        if (textoContagem != null) textoContagem.text = texto;
    }

    // Chamado todo frame: também volta para "Iniciar" quando o jogador vence
    void AtualizarBotaoIniciar()
    {
        if (textoBotaoIniciar == null) return;

        if (contagemRoutine != null)
            textoBotaoIniciar.text = "Cancelar";
        else if (targetSpawner != null && targetSpawner.HasStarted)
            textoBotaoIniciar.text = "Parar Tratamento";
        else
            textoBotaoIniciar.text = "Iniciar Tratamento";
    }

    // ── Acertos ─────────────────────────────────────────────
    void AtualizarTextoAcertos()
    {
        if (textoAcertos == null) return;

        int feitos = targetSpawner != null ? targetSpawner.HitsSoFar : 0;
        int total = targetSpawner != null ? targetSpawner.HitsToWin : acertosAtual;
        textoAcertos.text = $"Acertos: {feitos}/{total}";
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
        {
            MostrarStatus(goniometria.faseAtual, false, goniometria.progressoCalibracao);
            AtualizarBotaoCalibrar(true, false);
        }
        else if (goniometria.calibrado)
        {
            MostrarStatus($"✔ Calibrado ({goniometria.alcanceMaximo * 100f:F0} cm)", true, 1f);
            AtualizarBotaoCalibrar(false, true);
        }
        else
        {
            MostrarStatus("⚠ Não calibrado", false, 0f);
            AtualizarBotaoCalibrar(false, false);
        }
    }

    void AtualizarBotaoCalibrar(bool calibrando, bool calibrado)
    {
        if (botaoCalibrar != null)
            botaoCalibrar.interactable = !calibrando && !calibrado;

        if (textoBotaoCalibrar != null)
        {
            if (calibrado) textoBotaoCalibrar.text = "Calibragem Concluida";
            else if (calibrando) textoBotaoCalibrar.text = "Calibrando...";
            else textoBotaoCalibrar.text = "clique aqui para Calibrar";
        }
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
            textoDireito.text = $"{goniometria.GetGrausAtualDir():F0}°";

        if (textoEsquerdo != null)
            textoEsquerdo.text = $"{goniometria.GetGrausAtualEsq():F0}°";
    }

    // ── Dificuldade: presets (modo normal) ──────────────────
    public void OnLightSelected(bool selected) { if (selected) SelecionarDificuldade(DificuldadeLeve, toggleLight); }
    public void OnRegularSelected(bool selected) { if (selected) SelecionarDificuldade(DificuldadeRegular, toggleRegular); }
    public void OnHardSelected(bool selected) { if (selected) SelecionarDificuldade(DificuldadeDificil, toggleHard); }

    void SelecionarDificuldade(float valor, Toggle selecionado)
    {
        dificuldadeAtual = valor;

        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = valor;

        if (toggleLight != null && toggleLight != selecionado) toggleLight.SetIsOnWithoutNotify(false);
        if (toggleRegular != null && toggleRegular != selecionado) toggleRegular.SetIsOnWithoutNotify(false);
        if (toggleHard != null && toggleHard != selecionado) toggleHard.SetIsOnWithoutNotify(false);

        if (selecionado != null)
            selecionado.SetIsOnWithoutNotify(true);

        AplicarPreset();
    }

    // Aplica distância + acertos + simultâneos do preset atual
    void AplicarPreset()
    {
        if (dificuldadeAtual < 0.55f)
        {
            distanciaAtual = distanciaLeve;
            acertosAtual = acertosLeve;
            simultaneosAtual = simultaneosLeve;
        }
        else if (dificuldadeAtual < 0.85f)
        {
            distanciaAtual = distanciaRegular;
            acertosAtual = acertosRegular;
            simultaneosAtual = simultaneosRegular;
        }
        else
        {
            distanciaAtual = distanciaDificil;
            acertosAtual = acertosDificil;
            simultaneosAtual = simultaneosDificil;
        }

        distanciaAtual = Mathf.Clamp(distanciaAtual, distanciaMinima, distanciaMaximaMetros);

        AtualizarTextoDificuldade();
        AtualizarCampos();
        AplicarNoSpawner();
    }

    void AtualizarTextoDificuldade()
    {
        if (difficultyText == null) return;

        if (advancedDifficulty)
        {
            difficultyText.text = "Dificuldade: Personalizada";
            return;
        }

        string nome = dificuldadeAtual < 0.55f ? "Leve"
                    : dificuldadeAtual < 0.85f ? "Regular"
                    : "Difícil";
        difficultyText.text = $"Dificuldade: {nome}";
    }

    // ── Modo avançado ───────────────────────────────────────
    public void ToggleAdvancedDifficulty()
    {
        advancedDifficulty = !advancedDifficulty;
        SetAdvancedMode(advancedDifficulty);
    }

    void SetAdvancedMode(bool advanced)
    {
        advancedDifficulty = advanced;

        if (difficultyNormalPanel != null) difficultyNormalPanel.SetActive(!advanced);
        if (difficultyAdvancedPanel != null) difficultyAdvancedPanel.SetActive(advanced);

        // Voltando ao modo normal: o preset selecionado volta a valer
        if (!advanced)
            AplicarPreset();
        else
        {
            AtualizarTextoDificuldade();
            AtualizarCampos();
        }
    }

    void OnDistanciaEditada(string texto)
    {
        if (TentarLerFloat(texto, out float v))
            distanciaAtual = Mathf.Clamp(v, distanciaMinima, distanciaMaximaMetros);

        AtualizarCampos();
        AplicarNoSpawner();
    }

    void OnAcertosEditado(string texto)
    {
        if (int.TryParse(texto, out int v))
            acertosAtual = Mathf.Clamp(v, 1, maxAlvosParaVencer);

        AtualizarCampos();
        AplicarNoSpawner();
    }

    void OnSimultaneosEditado(string texto)
    {
        if (int.TryParse(texto, out int v))
            simultaneosAtual = Mathf.Clamp(v, 1, maxAlvosSimultaneos);

        AtualizarCampos();
        AplicarNoSpawner();
    }

    // Aceita "2.5" e "2,5"
    bool TentarLerFloat(string texto, out float valor)
    {
        return float.TryParse(texto.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }

    // Escreve os valores atuais nos campos sem disparar os eventos deles
    void AtualizarCampos()
    {
        if (inputDistancia != null)
            inputDistancia.SetTextWithoutNotify(distanciaAtual.ToString("F1"));

        if (inputAlvosParaVencer != null)
            inputAlvosParaVencer.SetTextWithoutNotify(acertosAtual.ToString());

        if (inputAlvosSimultaneos != null)
            inputAlvosSimultaneos.SetTextWithoutNotify(simultaneosAtual.ToString());
    }

    // Envia os 3 valores atuais para o TargetSpawner
    void AplicarNoSpawner()
    {
        if (!referenciasProntas) return;

        targetSpawner.SetSpawnDistance(pontoReferencia, distanciaAtual);

        targetSpawner.SetHitsToWin(acertosAtual);
        targetSpawner.SetMaxActiveTargets(simultaneosAtual);

        if (mostrarLogsDeDistancia)
            Debug.Log($"[MenuUI2] Alvos a {distanciaAtual:F1} m | vencer com {acertosAtual} acertos | {simultaneosAtual} simultâneos.");
    }

    // ── Resultados ──────────────────────────────────────────
    // Mostra o máximo recorrente de cada braço, em graus (sem texto, igual ao Minigame1)
    public void FinalizarSessao()
    {
        if (goniometria == null) return;

        var r = goniometria.GetResultados();
        sessaoFinalizada = true;
        tratamentoIniciouNoSpawner = false;

        if (textoDireito != null)
            textoDireito.text = $"{r.grausDireito:F0}°";

        if (textoEsquerdo != null)
            textoEsquerdo.text = $"{r.grausEsquerdo:F0}°";
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