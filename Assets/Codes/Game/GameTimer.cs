using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 화면 상단 5분 타이머. 5분 경과 시 양쪽 기지 HP가 동시에 감소하는 서든데스 모드.
/// HP가 많이 남은 쪽이 승리.
/// </summary>
public class GameTimer : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private float matchDurationSeconds = 300f; // 5분
    [SerializeField] private float overtimeDamagePerSecond = 20f; // 서든데스 시 초당 HP 감소량

    private Text timerText;
    private float remainingTime;
    private bool isRunning;
    private bool isOvertime;
    private float overtimeAccum;
    private List<Damageable> baseDamageables = new List<Damageable>();

    private static Font GetKoreanFont(int size)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Font.CreateDynamicFontFromOSFont("sans-serif", size);
#else
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        return Font.CreateDynamicFontFromOSFont("Arial", size);
#endif
    }

    private void Start()
    {
        CreateTimerUI();
        remainingTime = matchDurationSeconds;

        if (MultiplayerManager.Instance != null)
            MultiplayerManager.Instance.OnMatchStarted += StartTimer;
    }

    private void OnDestroy()
    {
        if (MultiplayerManager.Instance != null)
            MultiplayerManager.Instance.OnMatchStarted -= StartTimer;
    }

    public void StartTimer()
    {
        if (!isRunning)
        {
            isRunning = true;
            Debug.Log("[GameTimer] 5분 타이머 시작");
        }
    }

    private void CreateTimerUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject container = new GameObject("GameTimerPanel");
        container.transform.SetParent(canvas.transform, false);

        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f);
        rect.sizeDelta = new Vector2(180f, 50f);

        GameObject textObj = new GameObject("TimerText");
        textObj.transform.SetParent(container.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        timerText = textObj.AddComponent<Text>();
        timerText.text = "5:00";
        timerText.alignment = TextAnchor.MiddleCenter;
        timerText.fontSize = 36;
        timerText.color = Color.white;
        timerText.font = GetKoreanFont(36);
        timerText.fontStyle = FontStyle.Bold;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, 2f);
    }

    private void Update()
    {
        if (!isRunning || timerText == null) return;

        if (GameManager.Instance != null && (GameManager.Instance.IsVictory || GameManager.Instance.IsPlayerDead))
            return;

        if (isOvertime)
        {
            UpdateOvertime();
            return;
        }

        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            EnterOvertime();
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);
        timerText.text = $"{minutes}:{seconds:D2}";

        if (remainingTime <= 30f)
            timerText.color = Color.Lerp(Color.red, Color.yellow, remainingTime / 30f);
    }

    private void EnterOvertime()
    {
        isOvertime = true;
        overtimeAccum = 0f;

        var mainBases = FindObjectsByType<MainBase>(FindObjectsSortMode.None);
        baseDamageables.Clear();
        foreach (var mb in mainBases)
        {
            var d = mb.GetComponent<Damageable>();
            if (d != null && !d.IsDead)
                baseDamageables.Add(d);
        }

        if (timerText != null)
        {
            timerText.text = "서든데스!";
            timerText.color = Color.red;
        }

        Debug.Log($"[GameTimer] 서든데스 모드! 양쪽 기지 HP 감소 (초당 {overtimeDamagePerSecond})");
    }

    private void UpdateOvertime()
    {
        baseDamageables.RemoveAll(d => d == null || d.IsDead);

        if (baseDamageables.Count == 0)
            return;

        overtimeAccum += Time.deltaTime * overtimeDamagePerSecond;
        if (overtimeAccum >= 1f)
        {
            float damage = Mathf.Floor(overtimeAccum);
            overtimeAccum -= damage;

            foreach (var d in baseDamageables)
            {
                if (d != null && !d.IsDead)
                    d.TakeDamage(damage);
            }
        }
    }
}
