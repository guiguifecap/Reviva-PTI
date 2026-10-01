using UnityEngine;

/// <summary>
/// Calibração e medição de alcance dos braços. Só faz isso:
///  1) calibra (braços relaxados -> levantar o máximo possível)
///  2) mede o uso atual de cada braço em % do alcance calibrado
///  3) guarda o pico da sessão para o resultado final
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

    private float usoDir = 0f;
    private float usoEsq = 0f;
    private float picoDir = 0f;
    private float picoEsq = 0f;

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

        alcanceAtualDir = Vector3.Distance(relaxRight, NoReferencialDaCabeca(handRight.position));
        alcanceAtualEsq = Vector3.Distance(relaxLeft, NoReferencialDaCabeca(handLeft.position));

        float alvoDir = Mathf.Clamp01(alcanceAtualDir / alcanceMaximo) * 100f;
        float alvoEsq = Mathf.Clamp01(alcanceAtualEsq / alcanceMaximo) * 100f;

        float k = suavizacao > 0f ? 1f - Mathf.Exp(-suavizacao * Time.deltaTime) : 1f;
        usoDir = Mathf.Lerp(usoDir, alvoDir, k);
        usoEsq = Mathf.Lerp(usoEsq, alvoEsq, k);

        if (usoDir > picoDir) picoDir = usoDir;
        if (usoEsq > picoEsq) picoEsq = usoEsq;
    }

    public float GetUsoAtualDir()
    {
        return calibrado ? usoDir : 0f;
    }

    public float GetUsoAtualEsq()
    {
        return calibrado ? usoEsq : 0f;
    }

    /// <summary>Zera os picos da sessão (chamado ao calibrar e ao reiniciar a sessão).</summary>
    public void ResetarSessao()
    {
        usoDir = 0f;
        usoEsq = 0f;
        picoDir = 0f;
        picoEsq = 0f;
    }

    // ─────────────────────────────────────────────
    // Resultados (pico de uso de cada braço na sessão)
    // ─────────────────────────────────────────────
    public struct ResultadoSessao
    {
        public float percDireito;
        public float percEsquerdo;
        public float alcanceMaximoCM;
        public string diagnostico;
    }

    public ResultadoSessao GetResultados()
    {
        ResultadoSessao r;

        r.percDireito = picoDir;
        r.percEsquerdo = picoEsq;
        r.alcanceMaximoCM = alcanceMaximo * 100f;
        r.diagnostico = GerarDiagnostico(r.percDireito, r.percEsquerdo);

        return r;
    }

    string GerarDiagnostico(float dir, float esq)
    {
        float diff = Mathf.Abs(dir - esq);

        if (diff > 15f)
        {
            string ladoFraco = dir < esq ? "Direito" : "Esquerdo";
            return $"⚠ Assimetria funcional — lado {ladoFraco}";
        }

        if (dir < 70f || esq < 70f)
            return "⚠ Amplitude abaixo do ideal clínico";

        return "✔ Movimento dentro do esperado";
    }
}