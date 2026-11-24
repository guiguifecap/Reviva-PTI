using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostra a tela de calibração enquanto o paciente está calibrando, e esconde quando termina.
/// Existem duas telas independentes: a do jogador (VR) e a do PC (tela do fisioterapeuta).
///
/// Não coloque este script em nenhum dos painéis (ele seria desativado junto).
/// </summary>
public class TelaCalibragemGenerico : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("GoniometriaGenerico da cena. Se vazio, é procurado sozinho.")]
    public GoniometriaGenerico goniometria;

    [Header("Tela do Jogador (VR)")]
    [Tooltip("Objeto raiz da tela do jogador (Canvas World Space na frente da câmera).")]
    public GameObject painel;
    [Tooltip("Texto que mostra a fase atual da calibração.")]
    public TMP_Text textoStatus;
    [Tooltip("Barra de progresso da fase atual (opcional).")]
    public Slider barraProgresso;

    [Header("Tela do PC")]
    [Tooltip("Painel da tela do PC (com o backdrop). Aparece ao calibrar e some ao terminar.")]
    public GameObject painelPC;
    [Tooltip("Texto que mostra a fase atual da calibração na tela do PC.")]
    public TMP_Text textoStatusPC;
    [Tooltip("Barra de progresso da fase atual na tela do PC (opcional).")]
    public Slider barraProgressoPC;

    private bool calibrandoAnteriormente = false;

    void Start()
    {
        if (goniometria == null)
            goniometria = FindFirstObjectByType<GoniometriaGenerico>();

        if (painel != null)
            painel.SetActive(false); // só aparece durante a calibração
        else
            Debug.LogWarning("[TelaCalibragemGenerico] 'Painel' (jogador) não foi atribuído no Inspector.", this);

        if (painelPC != null)
            painelPC.SetActive(false);
        else
            Debug.LogWarning("[TelaCalibragemGenerico] 'Painel PC' não foi atribuído no Inspector.", this);

        if (goniometria == null)
            Debug.LogWarning("[TelaCalibragemGenerico] GoniometriaGenerico não encontrada na cena.", this);
    }

    void Update()
    {
        if (goniometria == null)
        {
            goniometria = FindFirstObjectByType<GoniometriaGenerico>();
            if (goniometria == null) return;
        }

        bool calibrandoAgora = goniometria.calibrando;

        if (calibrandoAgora != calibrandoAnteriormente)
        {
            if (painel != null) painel.SetActive(calibrandoAgora);
            if (painelPC != null) painelPC.SetActive(calibrandoAgora);
            calibrandoAnteriormente = calibrandoAgora;
        }

        if (calibrandoAgora)
        {
            string fase = goniometria.faseAtual;
            float progresso = goniometria.progressoCalibracao;

            // Jogador
            if (textoStatus != null) textoStatus.text = fase;
            if (barraProgresso != null) barraProgresso.value = progresso;

            // PC
            if (textoStatusPC != null) textoStatusPC.text = fase;
            if (barraProgressoPC != null) barraProgressoPC.value = progresso;
        }
    }
}