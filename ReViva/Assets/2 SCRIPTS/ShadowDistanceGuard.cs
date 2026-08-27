using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Garante que a distância de sombra do URP nunca fique curta demais em runtime,
/// não importa qual URP Asset (PC_RPAsset / Mobile_RPAsset) esteja ativo na
/// Quality atual. Corrige o sintoma de "sombras somem quando o jogador se move":
/// isso acontece quando o jogador (ou a montanha, que é reescalada dinamicamente
/// pelo Degrais.cs) sai do raio de 'Shadow Distance' configurado no asset — nesse
/// ponto o URP simplesmente para de desenhar a sombra daquele objeto.
///
/// Também reage caso a Quality mude em runtime (ex: troca de PC_RPAsset pra
/// Mobile_RPAsset), reaplicando a distância mínima no asset que passou a valer.
///
/// Uso: coloque este script em um GameObject persistente da cena (ex: o mesmo
/// que tem o GameSettings/AudioManager). Não precisa configurar nada — os
/// valores padrão já cobrem o tamanho normal da montanha.
/// </summary>
public class ShadowDistanceGuard : MonoBehaviour
{
    [Tooltip("Distância mínima de sombra (metros). Se o URP Asset ativo estiver configurado com um valor menor, ele é elevado até este.")]
    public float distanciaMinima = 100f;

    [Tooltip("Reaplica a distância mínima sempre que a Quality (URP Asset) ativa mudar em runtime.")]
    public bool observarTrocaDeQuality = true;

    int qualityAnterior = -1;

    void Awake()
    {
        AplicarDistanciaMinima();
    }

    void Update()
    {
        if (!observarTrocaDeQuality)
            return;

        int qualityAtual = QualitySettings.GetQualityLevel();
        if (qualityAtual != qualityAnterior)
        {
            qualityAnterior = qualityAtual;
            AplicarDistanciaMinima();
        }
    }

    void AplicarDistanciaMinima()
    {
        qualityAnterior = QualitySettings.GetQualityLevel();

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
        {
            if (urpAsset.shadowDistance < distanciaMinima)
            {
                Debug.Log(
                    $"[ShadowDistanceGuard] '{urpAsset.name}' tinha Shadow Distance = " +
                    $"{urpAsset.shadowDistance:F0}m — elevando para {distanciaMinima:F0}m."
                );

                urpAsset.shadowDistance = distanciaMinima;
            }
        }
        else
        {
            Debug.LogWarning(
                "[ShadowDistanceGuard] Nenhum Universal Render Pipeline Asset ativo foi encontrado " +
                "(o projeto pode não estar usando URP, ou o asset ainda não carregou)."
            );
        }
    }
}
