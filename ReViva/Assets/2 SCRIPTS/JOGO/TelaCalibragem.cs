using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla a tela de calibração: uma imagem de fundo + texto de status,
/// mostrada enquanto o paciente está sendo calibrado.
///
/// Essa tela deve ficar dentro de um Canvas World Space posicionado na
/// frente da câmera REAL do player no VR (dentro de 'XR origin'). Assim,
/// qualquer câmera que espelhe a visão do player (como a 'Camera do vr'
/// usada no painel do médico) mostra a mesma tela automaticamente — sem
/// precisar duplicar a UI em dois lugares.
///
/// Ao terminar a calibração:
/// - A tela se esconde (SetActive(false)).
/// - As pedras podem ser geradas imediatamente (opcional — ver 'Comportamento').
/// </summary>
public class TelaCalibragem : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("GoniometriaClimb da cena. Se vazio, tenta encontrar sozinho.")]
    public GoniometriaClimb goniometria;
    [Tooltip("Degrais da cena, usado pra gerar as pedras assim que a calibração terminar (se 'Gerar Pedras Ao Terminar Calibracao' estiver ligado). Se vazio, tenta encontrar sozinho.")]
    public Degrais degrais;

    [Header("UI da Tela de Calibração")]
    [Tooltip("GameObject raiz da tela (o Canvas/Painel inteiro, imagem + texto). Ativado ao começar a calibrar, desativado ao terminar.")]
    public GameObject painel;
    [Tooltip("Imagem de fundo da tela de calibração (opcional — só necessário se quiser trocar o sprite por código).")]
    public Image imagemFundo;
    [Tooltip("Texto (TMP) que mostra a fase/status atual da calibração.")]
    public TMP_Text textoStatus;

    [Header("Verificação (opcional, mas recomendado)")]
    [Tooltip(
        "Arraste aqui a câmera que espelha a visão do player pro painel do médico (a 'Camera do vr' " +
        "dentro de 'Menu_Medico'). Se atribuída, o script confere no Start() se essa câmera enxerga a " +
        "layer do painel — e avisa no Console se não enxergar, ANTES de você perder tempo testando."
    )]
    public Camera cameraEspelhoMedico;

    [Header("Comportamento")]
    [Tooltip(
        "Se true, gera as pedras automaticamente assim que a calibração terminar, além do que já " +
        "acontece via MenuUI_Jogo (que assina 'GoniometriaClimb.OnCalibracaoConcluida'). Como esse " +
        "evento JÁ dispara a geração de forma confiável, deixe FALSE (padrão) pra não gerar duas vezes " +
        "seguidas. Só ligue se, por algum motivo, o MenuUI_Jogo não estiver gerando as pedras sozinho."
    )]
    public bool gerarPedrasAoTerminarCalibracao = false;

    bool calibrandoAnteriormente = false;

    void Start()
    {
        GarantirReferencias();

        // começa escondida — só aparece quando a calibração realmente começar
        if (painel != null)
            painel.SetActive(false);
        else
            Debug.LogWarning("[TelaCalibragem] 'Painel' não foi atribuído no Inspector.");

        VerificarCullingMaskDoEspelho();

        calibrandoAnteriormente = goniometria != null && goniometria.calibrando;
    }

    void GarantirReferencias()
    {
        if (goniometria == null)
            goniometria = FindFirstObjectByType<GoniometriaClimb>();

        if (degrais == null)
        {
            degrais = FindFirstObjectByType<Degrais>();
            if (degrais == null)
                degrais = Degrais.Instance;
        }

        if (goniometria == null)
            Debug.LogWarning("[TelaCalibragem] GoniometriaClimb não encontrada na cena — a tela de calibração não vai funcionar.");

        if (degrais == null && gerarPedrasAoTerminarCalibracao)
            Debug.LogWarning("[TelaCalibragem] Degrais não encontrado na cena — as pedras não serão geradas automaticamente ao terminar a calibração.");
    }

    /// <summary>
    /// Checagem só pra ajudar a diagnosticar: se a câmera que alimenta o
    /// preview do médico não tiver a layer do painel marcada no Culling
    /// Mask, a tela nunca vai aparecer lá (mesmo aparecendo certinho no VR).
    /// </summary>
    void VerificarCullingMaskDoEspelho()
    {
        if (cameraEspelhoMedico == null || painel == null)
            return;

        int layerPainel = painel.layer;
        bool camaraEnxergaLayer = (cameraEspelhoMedico.cullingMask & (1 << layerPainel)) != 0;

        if (!camaraEnxergaLayer)
        {
            Debug.LogWarning(
                $"[TelaCalibragem] A câmera '{cameraEspelhoMedico.name}' NÃO está com a layer " +
                $"'{LayerMask.LayerToName(layerPainel)}' marcada no Culling Mask. A tela de calibração vai " +
                "aparecer no VR do paciente, mas NÃO vai aparecer no preview do médico. Marque essa layer " +
                $"no Culling Mask de '{cameraEspelhoMedico.name}' pra corrigir."
            );
        }
    }

    void Update()
    {
        // se algo ainda não foi encontrado no Start (ex: ordem de carregamento), tenta de novo
        if (goniometria == null || (degrais == null && gerarPedrasAoTerminarCalibracao))
            GarantirReferencias();

        if (goniometria == null) return;

        bool calibrandoAgora = goniometria.calibrando;

        // ── acabou de COMEÇAR a calibrar ──
        if (calibrandoAgora && !calibrandoAnteriormente)
            MostrarTela();

        // ── estava calibrando e agora TERMINOU ──
        if (!calibrandoAgora && calibrandoAnteriormente)
            EsconderTelaEGerarPedras();

        // atualiza o texto de status continuamente enquanto calibra
        if (calibrandoAgora && textoStatus != null)
            textoStatus.text = goniometria.faseAtual;

        calibrandoAnteriormente = calibrandoAgora;
    }

    void MostrarTela()
    {
        if (painel != null)
            painel.SetActive(true);

        if (textoStatus != null)
            textoStatus.text = goniometria.faseAtual;

        Debug.Log("[TelaCalibragem] Calibração iniciada — mostrando tela.");
    }

    void EsconderTelaEGerarPedras()
    {
        if (painel != null)
            painel.SetActive(false);

        Debug.Log("[TelaCalibragem] Calibração concluída — escondendo tela.");

        if (!gerarPedrasAoTerminarCalibracao)
            return;

        if (degrais != null)
        {
            Debug.Log("[TelaCalibragem] Gerando pedras imediatamente após a calibração.");
            degrais.GerarDegraus();
        }
        else
        {
            Debug.LogWarning("[TelaCalibragem] Calibração concluída, mas 'Degrais' não foi encontrado — pedras não foram geradas automaticamente.");
        }
    }
}
