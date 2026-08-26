using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI_Escolha : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] private RectTransform object1;
    [SerializeField] private RectTransform object2;
    [SerializeField] private RectTransform object3;

    [Header("Positions")]
    [SerializeField] private Vector2 offscreenLeftPos = new Vector2(-1400f, 0f);
    [SerializeField] private Vector2 offscreenRightPos = new Vector2(1400f, 0f);

    [Header("Scale")]
    [SerializeField] private float centerScale = 1f;
    [SerializeField] private float sideScale = 0.75f;

    [Header("Animation")]
    [SerializeField] private float duration = 0.35f;
    [SerializeField]
    private AnimationCurve ease =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Buttons")]
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;

    [Header("Focus")]
    [SerializeField] private float centerAlpha = 1f;
    [SerializeField] private float sideAlpha = 0.55f;

    private Vector2 leftPos;
    private Vector2 centerPos;
    private Vector2 rightPos;

    private bool isAnimating;

    private void Start()
    {
        // Get the EXACT positions from the objects in the Inspector.
        leftPos = object1.anchoredPosition;
        centerPos = object2.anchoredPosition;
        rightPos = object3.anchoredPosition;

        // Initial state:
        // 1 = LEFT
        // 2 = CENTER
        // 3 = RIGHT

        object1.anchoredPosition = leftPos;
        object2.anchoredPosition = centerPos;
        object3.anchoredPosition = rightPos;

        object1.localScale = Vector3.one * sideScale;
        object2.localScale = Vector3.one * centerScale;
        object3.localScale = Vector3.one * sideScale;

        if (leftArrowButton != null)
            leftArrowButton.onClick.AddListener(ClickLeft);

        if (rightArrowButton != null)
            rightArrowButton.onClick.AddListener(ClickRight);


        SetAlpha(object1, sideAlpha);
        SetAlpha(object2, centerAlpha);
        SetAlpha(object3, sideAlpha);
    }

    public void ClickRight()
    {
        if (isAnimating)
            return;

        StartCoroutine(MoveRight());
    }

    public void ClickLeft()
    {
        if (isAnimating)
            return;

        StartCoroutine(MoveLeft());
    }

    private IEnumerator MoveRight()
    {
        isAnimating = true;
        SetButtons(false);

        // Current:
        // object1 = LEFT
        // object2 = CENTER
        // object3 = RIGHT

        StartCoroutine(Animate(
            object1,
            leftPos,
            offscreenLeftPos,
            sideScale,
            sideScale,
            sideAlpha,
            0f
        ));

        StartCoroutine(Animate(
            object2,
            centerPos,
            leftPos,
            centerScale,
            sideScale,
            centerAlpha,
            sideAlpha
        ));

        StartCoroutine(Animate(
            object3,
            rightPos,
            centerPos,
            sideScale,
            centerScale,
            sideAlpha,
            centerAlpha
        ));

        yield return new WaitForSeconds(duration);

        // Object 1 is offscreen LEFT
        // Teleport to RIGHT
        object1.anchoredPosition = offscreenRightPos;
        object1.localScale = Vector3.one * sideScale;
        SetAlpha(object1, 0f);

        yield return StartCoroutine(Animate(
            object1,
            offscreenRightPos,
            rightPos,
            sideScale,
            sideScale,
            0f,
            sideAlpha
        ));

        // ==========================================
        // UPDATE ORDER
        //
        // Before:
        // 1  2  3
        //
        // After:
        // 2  3  1
        // ==========================================

        RectTransform temp = object1;

        object1 = object2;
        object2 = object3;
        object3 = temp;

        isAnimating = false;
        SetButtons(true);
    }

    private IEnumerator MoveLeft()
    {
        isAnimating = true;
        SetButtons(false);

        // Current:
        // object1 = LEFT
        // object2 = CENTER
        // object3 = RIGHT

        StartCoroutine(Animate(
            object3,
            rightPos,
            offscreenRightPos,
            sideScale,
            sideScale,
            sideAlpha,
            0f
        ));

        StartCoroutine(Animate(
            object2,
            centerPos,
            rightPos,
            centerScale,
            sideScale,
            centerAlpha,
            sideAlpha
        ));

        StartCoroutine(Animate(
            object1,
            leftPos,
            centerPos,
            sideScale,
            centerScale,
            sideAlpha,
            centerAlpha
        ));

        yield return new WaitForSeconds(duration);

        // Object 3 is offscreen RIGHT
        // Teleport to LEFT
        object3.anchoredPosition = offscreenLeftPos;
        object3.localScale = Vector3.one * sideScale;
        SetAlpha(object3, 0f);

        yield return StartCoroutine(Animate(
            object3,
            offscreenLeftPos,
            leftPos,
            sideScale,
            sideScale,
            0f,
            sideAlpha
        ));

        // ==========================================
        // UPDATE ORDER
        //
        // Before:
        // 1  2  3
        //
        // After:
        // 3  1  2
        // ==========================================

        RectTransform temp = object3;

        object3 = object2;
        object2 = object1;
        object1 = temp;

        isAnimating = false;
        SetButtons(true);
    }

    private IEnumerator Animate(
     RectTransform obj,
     Vector2 start,
     Vector2 end,
     float startScale,
     float endScale,
     float startAlpha,
     float endAlpha)
    {
        CanvasGroup group = obj.GetComponent<CanvasGroup>();

        if (group == null)
            group = obj.gameObject.AddComponent<CanvasGroup>();

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float eased = ease.Evaluate(t);

            obj.anchoredPosition =
                Vector2.Lerp(start, end, eased);

            float scale =
                Mathf.Lerp(startScale, endScale, eased);

            group.alpha =
                Mathf.Lerp(startAlpha, endAlpha, eased);

            obj.localScale = Vector3.one * scale;

            yield return null;
        }

        obj.anchoredPosition = end;
        obj.localScale = Vector3.one * endScale;
        group.alpha = endAlpha;
    }

    private void SetButtons(bool value)
    {
        if (leftArrowButton != null)
            leftArrowButton.interactable = value;

        if (rightArrowButton != null)
            rightArrowButton.interactable = value;
    }

    private void SetAlpha(RectTransform obj, float alpha)
    {
        CanvasGroup group = obj.GetComponent<CanvasGroup>();

        if (group == null)
            group = obj.gameObject.AddComponent<CanvasGroup>();

        group.alpha = alpha;
    }

    public void VoltarButton()
    {
        SceneManager.LoadScene("Menu");
    }

    public void ClimbingGame()
    {
        SceneManager.LoadScene("TesteFase1");
    }
}