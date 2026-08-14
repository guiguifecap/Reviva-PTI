using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI_Jogo : MonoBehaviour
{
    [Header("Configuração de Dificuldade")]
    public Slider difficultySlider;
    public TextMeshProUGUI difficultyText;

    [Header("Configuração de Séries (Repetições)")]
    [Tooltip("Input field para definir o número de séries. É a ÚNICA fonte desse valor — o Degrais nunca decide isso sozinho.")]
    public TMP_InputField inputNumeroDeSeries;
    [Tooltip("Input field para definir quantas pedras por série. É a ÚNICA fonte desse valor — o Degrais nunca decide isso sozinho.")]
    public TMP_InputField inputPedrasPorDescanso;
    [Tooltip("Valor usado no campo 'Número de Séries' caso ele esteja vazio na primeira vez que o jogo abre.")]
    public int padraoNumeroDeSeries = 3;
    [Tooltip("Valor usado no campo 'Pedras Por Descanso' caso ele esteja vazio na primeira vez que o jogo abre.")]
    public int padraoPedrasPorDescanso = 5;

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
            difficultySlider.value = 0.7f;
        }

        AtualizarTextoDificuldade();

        if (GameSettings.Instance != null)
            GameSettings.Instance.difficulty = 0.7f;

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

        // Busca goniometria se não foi assignada no Inspector
        if (goniometria == null)
            goniometria = FindObjectOfType<GoniometriaClimb>();

        // Busca degrais se não foi assignado no Inspector
        if (degrais == null)
            degrais = FindObjectOfType<Degrais>();

        if (degrais == null)
            Debug.LogWarning("[MenuUI] Degrais não encontrado na cena!");
        else if (Degrais.Instance != null && Degrais.Instance != degrais)
            Debug.LogWarning(
                "[MenuUI] O 'degrais' referenciado neste MenuUI é diferente de Degrais.Instance! " +
                "Existe mais de um Degrais na cena — verifique a Hierarchy."
            );

        // ── PASSO CRÍTICO: entrega as MESMAS referências de Input Field para o
        // Degrais, para que GerarDegraus() sempre leia o valor direto da UI,
        // não importa quem chamou a geração nem em que ordem os scripts rodaram.
        if (degrais != null)
        {
            degrais.inputNumeroDeSeries = inputNumeroDeSeries;
            degrais.inputPedrasPorDescanso = inputPedrasPorDescanso;
        }

        // ── PASSO CRÍTICO: aplica os valores dos Input Fields no Degrais
        // ANTES de registrar a calibração e antes de qualquer pedra existir.
        // A partir daqui, pedrasPorDescanso/numeroDeSeries só mudam através
        // desses campos — nunca através do Inspector do Degrais.
        ConfigurarInputsDeSerie();

        // Registra callback de calibração (só depois dos valores de série já aplicados)
        if (goniometria != null)
            goniometria.OnCalibracaoConcluida += RegenerarDegraus;
        else
            Debug.LogWarning("[MenuUI] GoniometriaClimb não encontrada na cena!");
    }

    void Update()
    {
        if (goniometria == null) return;
        AtualizarStatus();
        AtualizarTempoReal();
    }

    void OnDestroy()
    {
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
        if (difficultyText == null) return;
        int porcentagem = Mathf.RoundToInt(difficultySlider != null ? difficultySlider.value * 100f : 70f);
        difficultyText.text = $"Dificuldade: {porcentagem}%";
    }

    // ── Séries (Numero De Series / Pedras Por Descanso) ──────
    // Estes dois campos são a ÚNICA fonte de verdade para o Degrais.
    // O Inspector do Degrais só serve como fallback de emergência.

    void ConfigurarInputsDeSerie()
    {
        if (degrais == null)
        {
            Debug.LogWarning("[MenuUI] ConfigurarInputsDeSerie: 'degrais' está null, os inputs de série não vão funcionar.");
            return;
        }

        if (inputNumeroDeSeries == null)
            Debug.LogWarning("[MenuUI] 'inputNumeroDeSeries' não foi arrastado no Inspector do MenuUI_Jogo.");

        if (inputPedrasPorDescanso == null)
            Debug.LogWarning("[MenuUI] 'inputPedrasPorDescanso' não foi arrastado no Inspector do MenuUI_Jogo.");

        // se os campos estiverem vazios (primeira vez que o jogo abre), preenche com o padrão
        if (inputNumeroDeSeries != null && string.IsNullOrWhiteSpace(inputNumeroDeSeries.text))
            inputNumeroDeSeries.text = padraoNumeroDeSeries.ToString();

        if (inputPedrasPorDescanso != null && string.IsNullOrWhiteSpace(inputPedrasPorDescanso.text))
            inputPedrasPorDescanso.text = padraoPedrasPorDescanso.ToString();

        // registra os listeners para futuras edições
        if (inputNumeroDeSeries != null)
            inputNumeroDeSeries.onEndEdit.AddListener(_ => AplicarValoresDosInputsNoDegrais(regenerarDepois: true));

        if (inputPedrasPorDescanso != null)
            inputPedrasPorDescanso.onEndEdit.AddListener(_ => AplicarValoresDosInputsNoDegrais(regenerarDepois: true));

        // aplica o valor inicial dos campos no Degrais AGORA, antes de qualquer
        // calibração ou geração de pedras
        AplicarValoresDosInputsNoDegrais(regenerarDepois: false);
    }

    /// <summary>
    /// Lê o texto atual dos dois Input Fields, valida, e envia para Degrais.ConfigurarSeries().
    /// Se algum campo estiver com valor inválido, ele é revertido para o último valor válido conhecido.
    /// </summary>
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