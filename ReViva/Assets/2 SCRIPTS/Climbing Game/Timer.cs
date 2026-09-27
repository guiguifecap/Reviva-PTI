using TMPro;
using UnityEngine;

public class Timer : MonoBehaviour
{
    public static Timer Instance;

    public TMP_Text timerText;

    private float startTime;
    public bool isRunning = false;
    public bool finished = false;

    void Update()
    {
        if (isRunning && !finished)
        {
            float time = Time.time - startTime;

            int minutes = Mathf.FloorToInt(time / 60);
            int seconds = Mathf.FloorToInt(time % 60);
            int milliseconds = Mathf.FloorToInt((time * 100) % 100);

            timerText.text = string.Format(
                "{0:00}:{1:00}.{2:00}",
                minutes,
                seconds,
                milliseconds
            );
        }
    }

    public void StartTimer()
    {
        if (isRunning || finished)
            return;

        startTime = Time.time;
        isRunning = true;
    }

    public void StopTimer()
    {
        if (!isRunning || finished)
            return;

        isRunning = false;
        finished = true;
    }

    public void ResetTimer()
    {
        startTime = 0;
        isRunning = false;
        finished = false;

        timerText.text = "00:00.00";
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isRunning || finished)
            return;

        if (CompareTag("Mao") && collision.gameObject.CompareTag("Pedra"))
        {
            StartTimer();
        }
        else if (CompareTag("Pedra") && collision.gameObject.CompareTag("Mao"))
        {
            StartTimer();
        }
    }
}
