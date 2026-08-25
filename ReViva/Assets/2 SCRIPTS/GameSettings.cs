using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance;

    public float difficulty = 0f;
    public float alcanceMaximoCM = 150f;

    public bool usarMetadeDoAlcance = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}