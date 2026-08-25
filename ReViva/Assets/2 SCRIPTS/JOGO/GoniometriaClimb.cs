// GoniometriaClimb.cs (CALIBRAÇÃO CORRIGIDA PARA ALCANCE REAL)
//
// PROBLEMA:
// Você estava medindo:
// ombro → mão
//
// Isso mede só o braço.
//
// MAS no exercício de escalada/reabilitação o alcance funcional real inclui:
// extensão de tronco + ombro + braço
//
// Então o certo é medir:
// mão relaxada (baixo) → mão elevada (máximo)
//
// Isso normalmente dá ~120–150cm dependendo da pessoa,
// em vez de 70–85cm.
//
// CALIBRAÇÃO NOVA:
// Fase 1 = salva posição relaxada de cada mão
// Fase 2 = mede a maior distância entre posição relaxada e posição máxima
//
// Isso corrige drasticamente o valor clínico.

using UnityEngine;

public class GoniometriaClimb : MonoBehaviour
{
    [Header("Referências VR")]
    public Transform head;
    public Transform handRight;
    public Transform handLeft;

    [Header("Calibração")]
    public float tempoRelaxado = 2f;
    public float tempoMaximo = 3f;

    [Header("Estado")]
    public bool calibrando = false;
    public bool calibrado = false;

    public float progressoCalibracao = 0f;
    public string faseAtual = "";

    [Tooltip("Amplitude funcional máxima em metros")]
    public float alcanceMaximo = 0f;

    public float alcanceAtualDir { get; private set; }
    public float alcanceAtualEsq { get; private set; }

    public System.Action OnCalibracaoConcluida;

    // Estado interno
    private int fase = 0;
    private float timer = 0f;

    private Vector3 relaxRight;
    private Vector3 relaxLeft;

    private float maxRight = 0f;
    private float maxLeft = 0f;

    void Update()
    {
        if (calibrando)
        {
            CalibracaoGuiada();
            return;
        }

        if (!calibrado) return;

        // Uso em tempo real = quanto da amplitude total está usando AGORA
        alcanceAtualDir = Vector3.Distance(relaxRight, handRight.position);
        alcanceAtualEsq = Vector3.Distance(relaxLeft, handLeft.position);
    }

    // ─────────────────────────────────────────────
    // INICIAR CALIBRAÇÃO
    // ─────────────────────────────────────────────
    public void IniciarCalibracaoManual()
    {
        calibrando = true;
        calibrado = false;

        fase = 0;
        timer = 0f;

        maxRight = 0f;
        maxLeft = 0f;

        progressoCalibracao = 0f;
        faseAtual = "Prepare-se...";
    }

    // ─────────────────────────────────────────────
    // CALIBRAÇÃO GUIADA
    // ─────────────────────────────────────────────
    void CalibracaoGuiada()
    {
        timer += Time.deltaTime;

        // FASE 0 = RELAXADO
        if (fase == 0)
        {
            faseAtual = "Deixe os braços relaxados ao lado do corpo";
            progressoCalibracao = timer / tempoRelaxado;

            if (timer >= tempoRelaxado)
            {
                // Salva posição relaxada REAL
                relaxRight = handRight.position;
                relaxLeft = handLeft.position;

                timer = 0f;
                fase = 1;
            }
        }

        // FASE 1 = ELEVAR MÁXIMO
        else if (fase == 1)
        {
            faseAtual = "Levante os braços o máximo possível";
            progressoCalibracao = timer / tempoMaximo;

            float r = Vector3.Distance(relaxRight, handRight.position);
            float l = Vector3.Distance(relaxLeft, handLeft.position);

            if (r > maxRight) maxRight = r;
            if (l > maxLeft) maxLeft = l;

            if (timer >= tempoMaximo)
            {
                EncerrarCalibracao();
            }
        }
    }

    // ─────────────────────────────────────────────
    // FINALIZAR
    // ─────────────────────────────────────────────
    void EncerrarCalibracao()
    {
        // máximo detectado
        float maxDetectado = Mathf.Max(maxRight, maxLeft);

        // ─────────────────────────────────────────────
        // AJUSTE CLÍNICO
        //
        // reduz alguns cm porque:
        // - paciente compensa com tronco
        // - tracking do Quest exagera um pouco
        // - movimento extremo não é confortável
        //
        // 5cm = 0.05m
        // ─────────────────────────────────────────────

        float ajusteClinico = 0.05f;

        // alcance "real útil"
        alcanceMaximo = Mathf.Max(0f, maxDetectado - ajusteClinico);

        calibrando = false;
        calibrado = true;

        // salva padrão = alcance TOTAL
        GameSettings.Instance.alcanceMaximoCM = alcanceMaximo * 100f;

        Debug.Log(
            $"✔ Calibrado\n" +
            $"Detectado: {maxDetectado * 100f:F1}cm\n" +
            $"Ajustado: {alcanceMaximo * 100f:F1}cm"
        );

        OnCalibracaoConcluida?.Invoke();
    }

    // ─────────────────────────────────────────────
    // TEMPO REAL
    // ─────────────────────────────────────────────
    public float GetUsoAtualDir()
    {
        if (!calibrado || alcanceMaximo <= 0f) return 0f;

        return Mathf.Clamp((alcanceAtualDir / alcanceMaximo) * 100f, 0f, 100f);
    }

    public float GetUsoAtualEsq()
    {
        if (!calibrado || alcanceMaximo <= 0f) return 0f;

        return Mathf.Clamp((alcanceAtualEsq / alcanceMaximo) * 100f, 0f, 100f);
    }

    // ─────────────────────────────────────────────
    // RESULTADOS
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

        r.percDireito = GetUsoAtualDir();
        r.percEsquerdo = GetUsoAtualEsq();

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