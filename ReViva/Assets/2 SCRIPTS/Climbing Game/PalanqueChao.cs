using UnityEngine;

public class PalanqueChao : MonoBehaviour
{
    public GameObject palanqueChao;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            palanqueChao.SetActive(true);
        }
    }
}
