using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuUI_Menu : MonoBehaviour
{
    [Header("Cenas")]
    public string cenaMenu = "Menu";
    public string cenaJogo = "TesteFase1";

    [Header("Loading UI")]
    public GameObject loadingUI;


    // ============================================================
    // BUTTON ANIMATION
    // ============================================================

    [System.Serializable]
    public class MenuButton
    {
        public RectTransform buttonText;
        public RectTransform selectionDot;
        public CanvasGroup dotCanvasGroup;

        [HideInInspector]
        public Vector2 originalTextPosition;

        [HideInInspector]
        public Vector2 originalDotPosition;

        [HideInInspector]
        public bool isHovered;
    }

    [Header("Menu Buttons")]
    public MenuButton[] buttons;

    [Header("Animation Settings")]
    public float textSlideAmount = 12f;
    public float dotSlideAmount = 15f;
    public float animationSpeed = 10f;


    // ============================================================
    // START
    // ============================================================

    void Start()
    {
        foreach (MenuButton button in buttons)
        {
            button.originalTextPosition =
                button.buttonText.anchoredPosition;

            button.originalDotPosition =
                button.selectionDot.anchoredPosition;

            button.dotCanvasGroup.alpha = 0f;
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    void Update()
    {
        foreach (MenuButton button in buttons)
        {
            // -------------------------
            // TEXT
            // -------------------------

            Vector2 targetText =
                button.originalTextPosition;

            if (button.isHovered)
            {
                targetText +=
                    new Vector2(textSlideAmount, 0);
            }

            button.buttonText.anchoredPosition =
                Vector2.Lerp(
                    button.buttonText.anchoredPosition,
                    targetText,
                    Time.deltaTime * animationSpeed
                );


            // -------------------------
            // DOT
            // -------------------------

            Vector2 targetDot =
                button.originalDotPosition;

            if (button.isHovered)
            {
                targetDot +=
                    new Vector2(-dotSlideAmount, 0);
            }

            button.selectionDot.anchoredPosition =
                Vector2.Lerp(
                    button.selectionDot.anchoredPosition,
                    targetDot,
                    Time.deltaTime * animationSpeed
                );


            // -------------------------
            // DOT FADE
            // -------------------------

            float targetAlpha =
                button.isHovered ? 1f : 0f;

            button.dotCanvasGroup.alpha =
                Mathf.Lerp(
                    button.dotCanvasGroup.alpha,
                    targetAlpha,
                    Time.deltaTime * animationSpeed
                );
        }
    }


    // ============================================================
    // HOVER
    // ============================================================

    public void ButtonHoverEnter(int buttonIndex)
    {
        buttons[buttonIndex].isHovered = true;
    }

    public void ButtonHoverExit(int buttonIndex)
    {
        buttons[buttonIndex].isHovered = false;
    }


    // ============================================================
    // INSTAGRAM
    // ============================================================

    public void ReViva()
    {
        if (SceneManager.GetActiveScene().name == cenaMenu)
        {
            Application.OpenURL(
                "https://www.instagram.com/revivavr/?utm_source=ig_web_button_share_sheet"
            );
        }
    }


    // ============================================================
    // QUIT
    // ============================================================

    public void QuitGame()
    {
        if (SceneManager.GetActiveScene().name == cenaMenu)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }


    // ============================================================
    // START GAME
    // ============================================================

    public void StartGame()
    {
        if (SceneManager.GetActiveScene().name == cenaMenu)
        {
            StartCoroutine(CarregarJogoAsync());
        }
    }


    // ============================================================
    // LOAD ASYNC
    // ============================================================

    IEnumerator CarregarJogoAsync()
    {
        if (loadingUI != null)
            loadingUI.SetActive(true);

        AsyncOperation operation =
            SceneManager.LoadSceneAsync(cenaJogo);

        while (!operation.isDone)
        {
            yield return null;
        }
    }
}