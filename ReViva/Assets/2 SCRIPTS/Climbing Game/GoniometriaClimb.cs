// GoniometriaClimb.cs (CALIBRAÇÃO EM ESPAÇO RELATIVO À CABEÇA)
//
// PROBLEMA CORRIGIDO:
// relaxRight/relaxLeft eram salvos como posição ABSOLUTA de mundo
// (handRight.position) no momento da calibragem. No VR, o player
// se move pelo cenário (o rig anda), então depois de qualquer
// deslocamento a mão fica "longe" do ponto salvo só por causa do
// movimento do corpo inteiro — não porque o braço esticou. Isso
// fazia o uso em tempo real sempre bater perto de 100%.
//
// CORREÇÃO:
// Os pontos de referência agora são salvos em espaço LOCAL à
// 'head' (InverseTransformPoint). Como a head se move junto com
// o player, o offset mão↔cabeça continua correto não importa
// onde o player esteja no cenário.

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

    // ─────────────────────────────────────────────
    // Pontos de referência em espaço LOCAL À CABEÇA
    // (não mais posições absolutas de mundo). É isso
    // que faz eles "andarem junto" com o player no VR.
    // ─────────────────────────────────────────────
    private Vector3 relaxRightLocal;
    private Vector3 relaxLeftLocal;

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

        AtualizarUsoAtual();
    }

    void AtualizarUsoAtual()
    {
        Vector3 atualDirLocal = head.InverseTransformPoint(handRight.position);
        Vector3 atualEsqLocal = head.InverseTransformPoint(handLeft.position);

        alcanceAtualDir = Vector3.Distance(relaxRightLocal, atualDirLocal);
        alcanceAtualEsq = Vector3.Distance(relaxLeftLocal, atualEsqLocal);
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
                // Salva posição relaxada em espaço LOCAL da cabeça
                relaxRightLocal = head.InverseTransformPoint(handRight.position);
                relaxLeftLocal = head.InverseTransformPoint(handLeft.position);

                timer = 0f;
                fase = 1;
            }
        }

        // FASE 1 = ELEVAR MÁXIMO
        else if (fase == 1)
        {
            faseAtual = "Levante os braços o máximo possível";
            progressoCalibracao = timer / tempoMaximo;

            Vector3 atualDirLocal = head.InverseTransformPoint(handRight.position);
            Vector3 atualEsqLocal = head.InverseTransformPoint(handLeft.position);

            float r = Vector3.Distance(relaxRightLocal, atualDirLocal);
            float l = Vector3.Distance(relaxLeftLocal, atualEsqLocal);

            if (r > maxRight) maxRight = r;
            if (l > maxLeft) maxLeft = l;

            if (timer >= tempoMaximo)
            {
                EncerrarCalibracao();
            }
        }
    }

    // ─────────────────────────────────────────────
    // FINALIZAR CALIBRAÇÃO
    // ─────────────────────────────────────────────
    void EncerrarCalibracao()
    {
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

        alcanceMaximo = Mathf.Max(0f, maxDetectado - ajusteClinico);

        calibrando = false;
        calibrado = true;

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