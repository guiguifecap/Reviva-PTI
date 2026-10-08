using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI_Minigame1 : MonoBehaviour
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

    [Header("Configuração de Séries (Repetições)")]
    [Tooltip("Input field para definir o número de séries. É a ÚNICA fonte desse valor — o Degrais nunca decide isso sozinho.")]
    public TMP_InputField inputNumeroDeSeries;
    [Tooltip("Input field para definir quantas pedras por série. É a ÚNICA fonte desse valor — o Degrais nunca decide isso sozinho.")]
    public TMP_InputField inputPedrasPorDescanso;
    public int padraoNumeroDeSeries = 3;
    public int padraoPedrasPorDescanso = 5;

    [Header("Componentes do Jogo")]
    public Degrais degrais;
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
    [Tooltip("Texto mostrado quando a contagem termina e as pedras aparecem.")]
    public string textoComecou = "JÁ!";
    [Tooltip("Por quantos segundos o texto acima fica na tela.")]
    public float tempoTextoComecou = 0.7f;
    [Tooltip("Se ligado, só deixa iniciar o tratamento depois de calibrar.")]
    public bool exigirCalibracaoParaIniciar = true;

    private Coroutine contagemRoutine;
    private Coroutine textoFinalRoutine;
    // true = pedras já foram geradas (tratamento rodando)
    private bool tratamentoAtivo = false;

    [Header("Desempenho (Tempo Real + Resultado Final)")]
    public TextMeshProUGUI textoDireito;
    public TextMeshProUGUI textoEsquerdo;
    private bool sessaoFinalizada = false;

    [Header("Cenas")]
    public string cenaMenu = "Menu";
    public string cenaJogo = "EscaladaPrototipo";

    [Header("Modo de Alcance")]
    public Toggle toggleMetadeAlcance;

    [Header("Panel Pause")]
    public GameObject PanelPause;
    public bool isPause;

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

        // Estado inicial dos botões
        AtualizarBotaoCalibrar(false, false);
        AtualizarBotaoIniciar();

        SetAdvancedMode(false);
        SelecionarDificuldade(0.7f, toggleRegular);

        // ── Guard: o resto só faz sentido na cena do jogo ──
        if (SceneManager.GetActiveScene().name != cenaJogo)
        {
            Debug.LogWarning(
                $"[MenuUI] Start() interrompido pelo guard de cena: cena ativa é " +
                $"'{SceneManager.GetActiveScene().name}', mas 'cenaJogo' está configurado como " +
                $"'{cenaJogo}'. Se os nomes não baterem EXATAMENTE, os inputs de série nunca são configurados."
            );
            return;
        }

        if (goniometria == null)
            goniometria = FindObjectOfType<GoniometriaGenerico>();

        if (degrais == null)
            degrais = FindObjectOfType<Degrais>();

        if (degrais == null)
            Debug.LogWarning("[MenuUI] Degrais não encontrado na cena!");
        else if (Degrais.Instance != null && Degrais.Instance != degrais)
            Debug.LogWarning(
                "[MenuUI] O 'degrais' referenciado neste MenuUI é diferente de Degrais.Instance! " +
                "Existe mais de um Degrais na cena — verifique a Hierarchy."
            );

        if (degrais != null)
        {
            degrais.inputNumeroDeSeries = inputNumeroDeSeries;
            degrais.inputPedrasPorDescanso = inputPedrasPorDescanso;
        }

        ConfigurarInputsDeSerie();

        // A calibração NÃO gera pedras. Elas só nascem quando o jogador
        // clica em "Iniciar Tratamento" (veja ContagemEIniciar).
        if (goniometria == null)
            Debug.LogWarning("[MenuUI] GoniometriaGenerico não encontrada na cena!");
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
    // Ligue este método no OnClick do botão "Iniciar Tratamento"
    public void IniciarTratamento()
    {
        AlternarTratamento();
    }

    public void AlternarTratamento()
    {
        if (degrais == null)
        {
            Debug.LogWarning("[MenuUI] AlternarTratamento: Degrais não encontrado.");
            return;
        }

        if (contagemRoutine != null)
        {
            // Clicou durante a contagem: cancela
            CancelarContagem();
        }
        else if (tratamentoAtivo)
        {
            PararTratamento();
        }
        else
        {
            if (exigirCalibracaoParaIniciar && (goniometria == null || !goniometria.calibrado))
            {
                Debug.LogWarning("[MenuUI] Calibre o paciente antes de iniciar o tratamento.");
                MostrarContagem("Calibre primeiro!");
                if (textoFinalRoutine != null) StopCoroutine(textoFinalRoutine);
                textoFinalRoutine = StartCoroutine(ApagarContagemDepois(1.5f));
                return;
            }

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

        // Único ponto onde as pedras são geradas pela primeira vez
        AplicarValoresDosInputsNoDegrais(regenerarDepois: false);
        tratamentoAtivo = true;
        sessaoFinalizada = false;
        RegenerarDegraus();

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

    void PararTratamento()
    {
        tratamentoAtivo = false;

        // Se o seu Degrais tiver um método para apagar/limpar as pedras,
        // chame ele aqui. Ex.: degrais.LimparDegraus();
    }

    void MostrarContagem(string texto)
    {
        if (textoContagem != null) textoContagem.text = texto;
    }

    void AtualizarBotaoIniciar()
    {
        if (textoBotaoIniciar == null) return;

        if (contagemRoutine != null)
            textoBotaoIniciar.text = "Cancelar";
        else if (tratamentoAtivo)
            textoBotaoIniciar.text = "Parar Tratamento";
        else
            textoBotaoIniciar.text = "Iniciar Tratamento";
    }

    // ── Calibração ──────────────────────────────────────────
    public void CalibrarPaciente()
    {
        sessaoFinalizada = false;

        if (goniometria != null)
            goniometria.IniciarCalibracaoManual();
        else
            Debug.LogWarning("[MenuUI] CalibrarPaciente: GoniometriaGenerico não atribuída.");
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

    // ── Dificuldade ─────────────────────────────────────────
    public void OnSliderChanged(float value)
    {
        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = value;

        AtualizarTextoDificuldade();

        // Só refaz as pedras se o tratamento já estiver rodando
        if (tratamentoAtivo && goniometria != null && goniometria.calibrado)
            RegenerarDegraus();
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

        if (tratamentoAtivo && goniometria != null && goniometria.calibrado)
            RegenerarDegraus();
    }

    public void ToggleAdvancedDifficulty()
    {
        advancedDifficulty = !advancedDifficulty;
        SetAdvancedMode(advancedDifficulty);
    }

    void SetAdvancedMode(bool advanced)
    {
        if (difficultyNormalPanel != null)
            difficultyNormalPanel.SetActive(!advanced);

        if (difficultyAdvancedPanel != null)
            difficultyAdvancedPanel.SetActive(advanced);

        if (difficultySlider != null)
            difficultySlider.interactable = advanced;
    }

    // ── Séries (Numero De Series / Pedras Por Descanso) ──────
    void ConfigurarInputsDeSerie()
    {
        if (degrais == null)
        {
            Debug.LogWarning("[MenuUI] ConfigurarInputsDeSerie: 'degrais' está null, os inputs de série não vão funcionar.");
            return;
        }

        if (inputNumeroDeSeries == null)
            Debug.LogWarning("[MenuUI] 'inputNumeroDeSeries' não foi arrastado no Inspector do MenuUI.");

        if (inputPedrasPorDescanso == null)
            Debug.LogWarning("[MenuUI] 'inputPedrasPorDescanso' não foi arrastado no Inspector do MenuUI.");

        if (inputNumeroDeSeries != null && string.IsNullOrWhiteSpace(inputNumeroDeSeries.text))
            inputNumeroDeSeries.text = padraoNumeroDeSeries.ToString();

        if (inputPedrasPorDescanso != null && string.IsNullOrWhiteSpace(inputPedrasPorDescanso.text))
            inputPedrasPorDescanso.text = padraoPedrasPorDescanso.ToString();

        // Ao editar, só regenera se o tratamento já estiver rodando
        if (inputNumeroDeSeries != null)
            inputNumeroDeSeries.onEndEdit.AddListener(_ => AplicarValoresDosInputsNoDegrais(regenerarDepois: tratamentoAtivo));

        if (inputPedrasPorDescanso != null)
            inputPedrasPorDescanso.onEndEdit.AddListener(_ => AplicarValoresDosInputsNoDegrais(regenerarDepois: tratamentoAtivo));

        AplicarValoresDosInputsNoDegrais(regenerarDepois: false);
    }

    void AplicarValoresDosInputsNoDegrais(bool regenerarDepois)
    {
        if (degrais == null) return;

        int numeroDeSeriesValido = degrais.NumeroDeSeries > 0 ? degrais.NumeroDeSeries : padraoNumeroDeSeries;
        int pedrasPorDescansoValido = degrais.PedrasPorDescanso > 0 ? degrais.PedrasPorDescanso : padraoPedrasPorDescanso;

        bool ok = true;

        if (inputNumeroDeSeries != null)
        {
            if (int.TryParse(inputNumeroDeSeries.text, out int n) && n > 0)
            {
                numeroDeSeriesValido = n;
            }
            else
            {
                Debug.LogWarning("[MenuUI] Valor inválido em 'Número de Séries'. Revertendo para o último válido.");
                inputNumeroDeSeries.text = numeroDeSeriesValido.ToString();
                ok = false;
            }
        }

        if (inputPedrasPorDescanso != null)
        {
            if (int.TryParse(inputPedrasPorDescanso.text, out int p) && p > 0)
            {
                pedrasPorDescansoValido = p;
            }
            else
            {
                Debug.LogWarning("[MenuUI] Valor inválido em 'Pedras Por Descanso'. Revertendo para o último válido.");
                inputPedrasPorDescanso.text = pedrasPorDescansoValido.ToString();
                ok = false;
            }
        }

        degrais.ConfigurarSeries(pedrasPorDescansoValido, numeroDeSeriesValido);

        if (regenerarDepois && ok)
            RegenerarDegraus();
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

    // Mostra o máximo recorrente de cada braço, em graus
    public void FinalizarSessao()
    {
        if (goniometria == null) return;

        var r = goniometria.GetResultados();
        sessaoFinalizada = true;
        tratamentoAtivo = false; // só no Minigame1; no Minigame2 apague esta linha

        if (textoDireito != null)
            textoDireito.text = $"{r.grausDireito:F0}°";

        if (textoEsquerdo != null)
            textoEsquerdo.text = $"{r.grausEsquerdo:F0}°";
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

        // Só refaz as pedras se o tratamento já estiver rodando
        if (tratamentoAtivo && Degrais.Instance != null)
            Degrais.Instance.GerarDegraus();
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