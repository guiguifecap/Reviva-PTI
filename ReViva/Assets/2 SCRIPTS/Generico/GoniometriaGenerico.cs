using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Calibração e medição dos braços:
///  1) calibra (braços relaxados -> levantar o máximo possível)
///  2) mede o ÂNGULO de cada braço em graus (0° = relaxado, 180° = esticado para cima)
///  3) registra o pico de cada repetição e calcula o MÁXIMO RECORRENTE em graus
///     (valor que o paciente consegue repetir, ignorando picos isolados)
///
/// A posição da mão é medida em relação à cabeça, então andar pelo cenário
/// não muda o resultado.
/// </summary>
public class GoniometriaGenerico : MonoBehaviour
{
    [Header("Referências VR")]
    public Transform head;
    public Transform handRight;
    public Transform handLeft;

    [Header("Calibração")]
    [Tooltip("Tempo (s) com os braços relaxados.")]
    public float tempoRelaxado = 2f;
    [Tooltip("Tempo (s) para levantar os braços o máximo possível.")]
    public float tempoMaximo = 3f;
    [Tooltip("Desconto (m) no alcance detectado: compensa o tronco e o exagero do tracking.")]
    public float ajusteClinico = 0.05f;
    [Tooltip("Alcance mínimo (m) para aceitar a calibração. Abaixo disso, a calibração é recusada.")]
    public float alcanceMinimoValido = 0.2f;

    [Header("Medição")]
    [Tooltip("Ligado: mede a mão em relação à cabeça considerando só o giro lateral da cabeça. " +
             "Olhar para cima/baixo ou inclinar a cabeça não altera o resultado. " +
             "Desligado: usa a cabeça inteira (igual ao outro minigame).")]
    public bool usarSomenteGiroDaCabeca = true;
    [Tooltip("Suavização do valor em tempo real (maior = reage mais rápido, 0 = sem suavização).")]
    public float suavizacao = 10f;

    [Header("Ângulo do Ombro")]
    [Tooltip("Distância (m) entre os dois ombros. Cada ombro fica a metade disso para o lado da cabeça.")]
    public float larguraOmbros = 0.36f;
    [Tooltip("Distância (m) da cabeça (headset) até a linha dos ombros, para baixo.")]
    public float alturaOmbros = 0.20f;

    [Header("Máximo Recorrente (em graus)")]
    [Tooltip("Ângulo (°) que o braço precisa passar para começar a contar uma repetição.")]
    [Range(5f, 90f)] public float limiarInicioRepeticao = 30f;
    [Tooltip("Ângulo (°) abaixo do qual a repetição termina (deve ser menor que o limiar de início).")]
    [Range(1f, 85f)] public float limiarFimRepeticao = 20f;
    [Tooltip("Quantas repetições precisam ter atingido o valor para ele valer como 'recorrente'. " +
             "2 = ignora um pico isolado. 3 = mais rigoroso.")]
    [Min(1)] public int repeticoesMinimas = 2;

    [Header("Estado")]
    public bool calibrando = false;
    public bool calibrado = false;
    public float progressoCalibracao = 0f;
    public string faseAtual = "";
    [Tooltip("Amplitude funcional máxima em metros")]
    public float alcanceMaximo = 0f;

    public float alcanceAtualDir { get; private set; }
    public float alcanceAtualEsq { get; private set; }

    public System.Action OnCalibracaoIniciada;
    public System.Action OnCalibracaoConcluida;

    // Interno
    private int fase = 0;
    private float timer = 0f;

    private Vector3 relaxRight;
    private Vector3 relaxLeft;
    private Vector3 somaRelaxRight;
    private Vector3 somaRelaxLeft;
    private int amostrasRelax;

    private float maxRight = 0f;
    private float maxLeft = 0f;

    // Uso em % (mantido para quem ainda precisar) e ângulo em graus
    private float usoDir = 0f;
    private float usoEsq = 0f;
    private float grausDir = 0f;
    private float grausEsq = 0f;
    private float picoGrausDir = 0f;   // pico absoluto (só usado como fallback)
    private float picoGrausEsq = 0f;

    // Repetições (em graus)
    private readonly List<float> picosRepDir = new List<float>();
    private readonly List<float> picosRepEsq = new List<float>();
    private bool emRepDir = false;
    private bool emRepEsq = false;
    private float picoRepAtualDir = 0f;
    private float picoRepAtualEsq = 0f;

    void Update()
    {
        if (calibrando)
        {
            CalibracaoGuiada();
            return;
        }

        if (calibrado)
            AtualizarUsoAtual();
    }

    // ─────────────────────────────────────────────
    // Posição da mão no referencial da cabeça
    // ─────────────────────────────────────────────
    Vector3 NoReferencialDaCabeca(Vector3 pontoMundo)
    {
        if (!usarSomenteGiroDaCabeca)
            return head.InverseTransformPoint(pontoMundo);

        // Referencial só com o giro lateral (yaw) da cabeça
        Vector3 frente = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (frente.sqrMagnitude < 0.01f)
            frente = Vector3.ProjectOnPlane(head.up, Vector3.up); // olhando muito para cima/baixo
        if (frente.sqrMagnitude < 0.0001f)
            frente = Vector3.forward;

        Quaternion giro = Quaternion.LookRotation(frente.normalized, Vector3.up);
        return Quaternion.Inverse(giro) * (pontoMundo - head.position);
    }

    bool ReferenciasOk()
    {
        return head != null && handRight != null && handLeft != null;
    }

    // Posição estimada de cada ombro no referencial da cabeça
    Vector3 OmbroDireito => new Vector3(larguraOmbros * 0.5f, -alturaOmbros, 0f);
    Vector3 OmbroEsquerdo => new Vector3(-larguraOmbros * 0.5f, -alturaOmbros, 0f);

    // ─────────────────────────────────────────────
    // Calibração
    // ─────────────────────────────────────────────
    [ContextMenu("Iniciar calibração (teste)")]
    public void IniciarCalibracaoManual()
    {
        if (!ReferenciasOk())
        {
            Debug.LogError("[GoniometriaGenerico] Head, Hand Right e Hand Left precisam estar atribuídos no Inspector.", this);
            return;
        }

        calibrando = true;
        calibrado = false;

        fase = 0;
        timer = 0f;
        amostrasRelax = 0;
        somaRelaxRight = Vector3.zero;
        somaRelaxLeft = Vector3.zero;
        maxRight = 0f;
        maxLeft = 0f;

        alcanceMaximo = 0f;
        alcanceAtualDir = 0f;
        alcanceAtualEsq = 0f;
        ResetarSessao();

        progressoCalibracao = 0f;
        faseAtual = "Prepare-se...";

        OnCalibracaoIniciada?.Invoke();
    }

    void CalibracaoGuiada()
    {
        if (!ReferenciasOk())
        {
            calibrando = false;
            return;
        }

        timer += Time.deltaTime;

        // FASE 0: braços relaxados (usa a média do último trecho para reduzir o ruído do tracking)
        if (fase == 0)
        {
            faseAtual = "Deixe os braços relaxados ao lado do corpo";
            progressoCalibracao = Mathf.Clamp01(timer / Mathf.Max(0.01f, tempoRelaxado));

            float janela = Mathf.Min(0.5f, tempoRelaxado);
            if (timer >= tempoRelaxado - janela)
            {
                somaRelaxRight += NoReferencialDaCabeca(handRight.position);
                somaRelaxLeft += NoReferencialDaCabeca(handLeft.position);
                amostrasRelax++;
            }

            if (timer >= tempoRelaxado)
            {
                if (amostrasRelax > 0)
                {
                    relaxRight = somaRelaxRight / amostrasRelax;
                    relaxLeft = somaRelaxLeft / amostrasRelax;
                }
                else
                {
                    relaxRight = NoReferencialDaCabeca(handRight.position);
                    relaxLeft = NoReferencialDaCabeca(handLeft.position);
                }

                timer = 0f;
                fase = 1;
            }
        }
        // FASE 1: levantar o máximo possível
        else
        {
            faseAtual = "Levante os braços o máximo possível";
            progressoCalibracao = Mathf.Clamp01(timer / Mathf.Max(0.01f, tempoMaximo));

            float r = Vector3.Distance(relaxRight, NoReferencialDaCabeca(handRight.position));
            float l = Vector3.Distance(relaxLeft, NoReferencialDaCabeca(handLeft.position));

            if (r > maxRight) maxRight = r;
            if (l > maxLeft) maxLeft = l;

            if (timer >= tempoMaximo)
                EncerrarCalibracao();
        }
    }

    void EncerrarCalibracao()
    {
        float maxDetectado = Mathf.Max(maxRight, maxLeft);

        calibrando = false;
        progressoCalibracao = 1f;

        if (maxDetectado < alcanceMinimoValido)
        {
            calibrado = false;
            alcanceMaximo = 0f;
            faseAtual = "Movimento muito pequeno. Calibre de novo e levante mais os braços.";
            Debug.LogWarning($"[GoniometriaGenerico] Calibração recusada: alcance detectado {maxDetectado * 100f:F1} cm " +
                             $"(mínimo {alcanceMinimoValido * 100f:F0} cm).");
            return;
        }

        alcanceMaximo = Mathf.Max(0f, maxDetectado - ajusteClinico);
        calibrado = true;
        faseAtual = "Calibrado";
        ResetarSessao();

        if (GameSettings.Instance != null)
            GameSettings.Instance.alcanceMaximoCM = alcanceMaximo * 100f;

        Debug.Log($"[GoniometriaGenerico] Calibrado. Detectado: {maxDetectado * 100f:F1} cm | Ajustado: {alcanceMaximo * 100f:F1} cm");

        OnCalibracaoConcluida?.Invoke();
    }

    // ─────────────────────────────────────────────
    // Tempo real
    // ─────────────────────────────────────────────
    void AtualizarUsoAtual()
    {
        if (!ReferenciasOk() || alcanceMaximo <= 0f) return;

        Vector3 maoDir = NoReferencialDaCabeca(handRight.position);
        Vector3 maoEsq = NoReferencialDaCabeca(handLeft.position);

        // Distância (usada pelo jogo e pelo % de uso)
        alcanceAtualDir = Vector3.Distance(relaxRight, maoDir);
        alcanceAtualEsq = Vector3.Distance(relaxLeft, maoEsq);

        float alvoDir = Mathf.Clamp01(alcanceAtualDir / alcanceMaximo) * 100f;
        float alvoEsq = Mathf.Clamp01(alcanceAtualEsq / alcanceMaximo) * 100f;

        // Ângulo do ombro: direção de repouso (0°) contra a direção atual ombro -> mão
        Vector3 ombroD = OmbroDireito;
        Vector3 ombroE = OmbroEsquerdo;
        float alvoGrauDir = Mathf.Clamp(Vector3.Angle(relaxRight - ombroD, maoDir - ombroD), 0f, 180f);
        float alvoGrauEsq = Mathf.Clamp(Vector3.Angle(relaxLeft - ombroE, maoEsq - ombroE), 0f, 180f);

        float k = suavizacao > 0f ? 1f - Mathf.Exp(-suavizacao * Time.deltaTime) : 1f;
        usoDir = Mathf.Lerp(usoDir, alvoDir, k);
        usoEsq = Mathf.Lerp(usoEsq, alvoEsq, k);
        grausDir = Mathf.Lerp(grausDir, alvoGrauDir, k);
        grausEsq = Mathf.Lerp(grausEsq, alvoGrauEsq, k);

        if (grausDir > picoGrausDir) picoGrausDir = grausDir;
        if (grausEsq > picoGrausEsq) picoGrausEsq = grausEsq;

        RegistrarRepeticao(grausDir, ref emRepDir, ref picoRepAtualDir, picosRepDir);
        RegistrarRepeticao(grausEsq, ref emRepEsq, ref picoRepAtualEsq, picosRepEsq);
    }

    /// <summary>
    /// Detecta repetições com histerese: começa ao passar do limiar de início,
    /// termina ao cair abaixo do limiar de fim, e guarda o pico (em graus) daquela repetição.
    /// </summary>
    void RegistrarRepeticao(float graus, ref bool emRep, ref float picoRep, List<float> lista)
    {
        float inicio = limiarInicioRepeticao;
        float fim = Mathf.Min(limiarFimRepeticao, inicio - 1f);

        if (!emRep)
        {
            if (graus >= inicio)
            {
                emRep = true;
                picoRep = graus;
            }
        }
        else
        {
            if (graus > picoRep) picoRep = graus;

            if (graus <= fim)
            {
                lista.Add(picoRep);
                emRep = false;
                picoRep = 0f;
            }
        }
    }

    /// <summary>Ângulo atual do braço direito (0° relaxado, 180° esticado para cima).</summary>
    public float GetGrausAtualDir()
    {
        return calibrado ? grausDir : 0f;
    }

    /// <summary>Ângulo atual do braço esquerdo (0° relaxado, 180° esticado para cima).</summary>
    public float GetGrausAtualEsq()
    {
        return calibrado ? grausEsq : 0f;
    }

    public float GetUsoAtualDir()
    {
        return calibrado ? usoDir : 0f;
    }

    public float GetUsoAtualEsq()
    {
        return calibrado ? usoEsq : 0f;
    }

    /// <summary>Zera os dados da sessão (chamado ao calibrar e ao reiniciar a sessão).</summary>
    public void ResetarSessao()
    {
        usoDir = 0f;
        usoEsq = 0f;
        grausDir = 0f;
        grausEsq = 0f;
        picoGrausDir = 0f;
        picoGrausEsq = 0f;

        picosRepDir.Clear();
        picosRepEsq.Clear();
        emRepDir = false;
        emRepEsq = false;
        picoRepAtualDir = 0f;
        picoRepAtualEsq = 0f;
    }

    // ─────────────────────────────────────────────
    // Resultados (máximo recorrente de cada braço, em graus)
    // ─────────────────────────────────────────────
    public struct ResultadoSessao
    {
        public float grausDireito;
        public float grausEsquerdo;
        public float alcanceMaximoCM;
        public string diagnostico;
    }

    public ResultadoSessao GetResultados()
    {
        // Fecha a repetição que ainda estiver em andamento
        var dir = new List<float>(picosRepDir);
        var esq = new List<float>(picosRepEsq);
        if (emRepDir) dir.Add(picoRepAtualDir);
        if (emRepEsq) esq.Add(picoRepAtualEsq);

        ResultadoSessao r;
        r.grausDireito = MaximoRecorrente(dir, picoGrausDir);
        r.grausEsquerdo = MaximoRecorrente(esq, picoGrausEsq);
        r.alcanceMaximoCM = alcanceMaximo * 100f;
        r.diagnostico = GerarDiagnostico(r.grausDireito, r.grausEsquerdo);

        return r;
    }

    /// <summary>
    /// Maior ângulo que foi atingido (ou superado) em pelo menos 'repeticoesMinimas' repetições.
    /// Ex.: picos 150°, 141°, 140°, 140°, 139° com mínimo 2 -> 141°. O 150° isolado é ignorado.
    /// Se houve menos repetições que o mínimo, usa a de menor pico entre as que existem
    /// (ou o pico absoluto, se nenhuma repetição foi detectada).
    /// </summary>
    float MaximoRecorrente(List<float> picos, float fallbackPicoAbsoluto)
    {
        if (picos.Count == 0)
            return fallbackPicoAbsoluto;

        picos.Sort((a, b) => b.CompareTo(a)); // decrescente

        int indice = Mathf.Min(Mathf.Max(1, repeticoesMinimas), picos.Count) - 1;
        return picos[indice];
    }

    string GerarDiagnostico(float dir, float esq)
    {
        float diff = Mathf.Abs(dir - esq);

        if (diff > 15f)
        {
            string ladoFraco = dir < esq ? "Direito" : "Esquerdo";
            return $"⚠ Assimetria funcional — lado {ladoFraco}";
        }

        if (dir < 150f || esq < 150f)
            return "⚠ Amplitude abaixo do ideal clínico";

        return "✔ Movimento dentro do esperado";
    }
}