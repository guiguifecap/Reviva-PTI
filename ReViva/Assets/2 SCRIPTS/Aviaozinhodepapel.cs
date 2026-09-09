using UnityEngine;

public class Aviaozinhodepapel : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Configurações de Aerodinâmica")]
    [Tooltip("Força que empurra o avião para cima baseado na velocidade dele")]
    public float forcaSustentacao = 2.5f;

    [Tooltip("O quanto o bico do avião se alinha com a direção da queda")]
    public float estabilidadeBico = 3.5f;

    [Header("Gravidade Customizada (Mais Leve)")]
    [Tooltip("Gravidade real simulada para o papel (A da Unity padrão é pesada demais)")]
    public float gravidadePapel = 3.0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Desativamos a gravidade global da Unity para este objeto 
        // e aplicamos a nossa própria gravidade de papel controlada por código
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        // Se o Rigidbody for IsKinematic, significa que a mão do VR ainda está segurando ele.
        // Portanto, não aplicamos física de voo enquanto estiver na mão.
        if (rb.isKinematic) return;

        // 1. Aplica a gravidade suave de papel
        rb.AddForce(Vector3.down * gravidadePapel, ForceMode.Acceleration);

        // 2. Calcula a velocidade atual do avião no eixo para frente dele
        Vector3 velocidade = rb.linearVelocity;
        float velocidadeFrente = Vector3.Dot(velocidade, transform.forward);

        // Se ele tiver velocidade avançando para frente (resultado do seu arremesso de VR)
        if (velocidadeFrente > 0.1f)
        {
            // 3. Efeito de Sustentação (Lift): Empurra o avião para CIMA com base na velocidade de avanço
            Vector3 forcaSubida = transform.up * (velocidadeFrente * velocidadeFrente * forcaSustentacao);
            rb.AddForce(forcaSubida, ForceMode.Force);

            // 4. Efeito de Arrasto Dinâmico: Cria resistência do ar realista para ele perder velocidade aos poucos
            Vector3 arrastoAr = -velocidade * (velocidadeFrente * 0.05f);
            rb.AddForce(arrastoAr, ForceMode.Force);

            // 5. Alinhamento do Bico (Efeito Catavento): Gira o bico na direção exata do vetor de queda/voo
            Vector3 direcaoDoVoo = velocidade.normalized;
            Quaternion rotacaoAlvo = Quaternion.LookRotation(direcaoDoVoo, transform.up);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, rotacaoAlvo, estabilidadeBico * Time.fixedDeltaTime));
        }
    }
}
