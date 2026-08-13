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
    [Tooltip("Input field para definir o número de séries (Degrais.numeroDeSeries).")]
    public TMP_InputField inputNumeroDeSeries;
    [Tooltip("Input field para definir quantas pedras por série (Degrais.pedrasPorDescanso).")]
    public TMP_InputField inputPedrasPorDescanso;

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

        // Registra callback de calibração
        if (goniometria != null)
            goniometria.OnCalibracaoConcluida += RegenerarDegraus;
        else
            Debug.LogWarning("[MenuUI] GoniometriaClimb não encontrada na cena!");

        if (degrais == null)
            Debug.LogWarning("[MenuUI] Degrais não encontrado na cena!");
        else if (Degrais.Instance != null && Degrais.Instance != degrais)
            Debug.LogWarning(
                "[MenuUI] O 'degrais' referenciado neste MenuUI é diferente de Degrais.Instance! " +
                "Isso indica que existe mais de um Degrais na cena. Os inputs de série vão atualizar " +
                "um objeto, mas a geração real pode estar usando o outro (ex: via Degrais.Instance " +
                "em OnToggleAlcance). Verifique se há Degrais duplicado na Hierarchy."
            );

        ConfigurarInputsDeSerie();
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

    // ── Séries (Numero De Series / Pedras Por Descanso) ──────
    void ConfigurarInputsDeSerie()
    {
        if (degrais == null)
        {
            Debug.LogWarning("[MenuUI] ConfigurarInputsDeSerie: 'degrais' está null, os inputs de série não vão funcionar.");
            return;
        }

        // preenche os campos com o valor atual configurado no Degrais
        if (inputNumeroDeSeries != null)
        {
            inputNumeroDeSeries.text = degrais.numeroDeSeries.ToString();
            inputNumeroDeSeries.onEndEdit.AddListener(OnInputNumeroDeSeriesChanged);
            Debug.Log("[MenuUI] Listener de 'Numero De Series' registrado.");
        }
        else
        {
            Debug.LogWarning("[MenuUI] 'inputNumeroDeSeries' não foi arrastado no Inspector do MenuUI_Jogo.");
        }

        if (inputPedrasPorDescanso != null)
        {
            inputPedrasPorDescanso.text = degrais.pedrasPorDescanso.ToString();
            inputPedrasPorDescanso.onEndEdit.AddListener(OnInputPedrasPorDescansoChanged);
            Debug.Log("[MenuUI] Listener de 'Pedras Por Descanso' registrado.");
        }
        else
        {
            Debug.LogWarning("[MenuUI] 'inputPedrasPorDescanso' não foi arrastado no Inspector do MenuUI_Jogo.");
        }
    }

    public void OnInputNumeroDeSeriesChanged(string valor)
    {
        Debug.Log($"[MenuUI] OnInputNumeroDeSeriesChanged recebeu: '{valor}'");

        if (degrais == null)
        {
            Debug.LogWarning("[MenuUI] 'degrais' é null, não é possível aplicar o valor.");
            return;
        }

        if (int.TryParse(valor, out int numero) && numero > 0)
        {
            degrais.numeroDeSeries = numero;
            Debug.Log($"[MenuUI] degrais.numeroDeSeries agora é {degrais.numeroDeSeries}");

            // regenera sempre, independente de já estar calibrado ou não
            RegenerarDegraus();
        }
        else
        {
            Debug.LogWarning("[MenuUI] Valor inválido para 'Número de Séries'. Use um número inteiro maior que zero.");
            // reverte o campo para o valor válido atual
            if (inputNumeroDeSeries != null)
                inputNumeroDeSeries.text = degrais.numeroDeSeries.ToString();
        }
    }

    public void OnInputPedrasPorDescansoChanged(string valor)
    {
        Debug.Log($"[MenuUI] OnInputPedrasPorDescansoChanged recebeu: '{valor}'");

        if (degrais == null)
        {
            Debug.LogWarning("[MenuUI] 'degrais' é null, não é possível aplicar o valor.");
            return;
        }

        if (int.TryParse(valor, out int numero) && numero > 0)
        {
            degrais.pedrasPorDescanso = numero;
            Debug.Log($"[MenuUI] degrais.pedrasPorDescanso agora é {degrais.pedrasPorDescanso}");

            // regenera sempre, independente de já estar calibrado ou não
            RegenerarDegraus();
        }
        else
        {
            Debug.LogWarning("[MenuUI] Valor inválido para 'Pedras Por Descanso'. Use um número inteiro maior que zero.");
            // reverte o campo para o valor válido atual
            if (inputPedrasPorDescanso != null)
                inputPedrasPorDescanso.text = degrais.pedrasPorDescanso.ToString();
        }
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