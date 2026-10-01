using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostra a tela de calibração (painel + texto da fase + barra de progresso opcional)
/// enquanto o paciente está calibrando, e esconde quando termina. Nada além disso.
///
/// O painel deve ser um Canvas World Space na frente da câmera do player.
/// Não coloque este script no próprio objeto do painel (ele seria desativado junto).
/// </summary>
public class TelaCalibragemGenerico : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("GoniometriaGenerico da cena. Se vazio, é procurado sozinho.")]
    public GoniometriaGenerico goniometria;
    [Tooltip("Objeto raiz da tela (Canvas/Painel). Aparece ao calibrar e some ao terminar.")]
    public GameObject painel;
    [Tooltip("Texto que mostra a fase atual da calibração.")]
    public TMP_Text textoStatus;
    [Tooltip("Barra de progresso da fase atual (opcional).")]
    public Slider barraProgresso;

    private bool calibrandoAnteriormente = false;

    void Start()
    {
        if (goniometria == null)
            goniometria = FindFirstObjectByType<GoniometriaGenerico>();

        if (painel != null)
            painel.SetActive(false); // só aparece durante a calibração
        else
            Debug.LogWarning("[TelaCalibragemGenerico] 'Painel' não foi atribuído no Inspector.", this);

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
            calibrandoAnteriormente = calibrandoAgora;
        }

        if (calibrandoAgora)
        {
            if (textoStatus != null) textoStatus.text = goniometria.faseAtual;
            if (barraProgresso != null) barraProgresso.value = goniometria.progressoCalibracao;
        }
    }
}