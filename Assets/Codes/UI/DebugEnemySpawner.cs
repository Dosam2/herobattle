using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 디버그 전용: 적 기지에서 랜덤 적을 소환하는 버튼.
/// 멀티플레이로 전환할 때 이 스크립트를 제거하세요.
/// </summary>
public class DebugEnemySpawner : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private float autoSpawnInterval = 8f;
    [SerializeField] private bool autoSpawnEnabled = false;

    private Button spawnButton;
    private float lastAutoSpawn;

    private void Start()
    {
        CreateDebugUI();
    }

    private void Update()
    {
        if (autoSpawnEnabled && Time.time >= lastAutoSpawn + autoSpawnInterval)
        {
            SpawnRandomEnemy();
            lastAutoSpawn = Time.time;
        }
    }

    private void CreateDebugUI()
    {
        // 캔버스 찾기 (없으면 반환)
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // 좌상단에 컨테이너
        GameObject container = new GameObject("DebugSpawnPanel");
        container.transform.SetParent(canvas.transform, false);
        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(10f, -10f);
        rect.sizeDelta = new Vector2(120f, 80f);

        // 소환 버튼
        GameObject btnObj = new GameObject("EnemySpawnBtn");
        btnObj.transform.SetParent(container.transform, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = new Vector2(1f, 0.55f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        Color btnColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        img.color = btnColor;

        spawnButton = btnObj.AddComponent<Button>();
        ColorBlock cb = spawnButton.colors;
        cb.normalColor = btnColor;
        cb.highlightedColor = btnColor * 1.2f;
        cb.pressedColor = btnColor * 0.6f;
        cb.selectedColor = btnColor;
        spawnButton.colors = cb;
        spawnButton.onClick.AddListener(SpawnRandomEnemy);

        // 레이블
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text uiText = textObj.AddComponent<Text>();
        uiText.text = "적 소환";
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.fontSize = 16;
        uiText.color = Color.white;
        uiText.fontStyle = FontStyle.Bold;
        uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;

        // 자동 소환 토글
        GameObject toggleObj = new GameObject("AutoToggle");
        toggleObj.transform.SetParent(container.transform, false);
        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 0.6f);
        toggleRect.anchorMax = Vector2.one;
        toggleRect.offsetMin = Vector2.zero;
        toggleRect.offsetMax = Vector2.zero;

        Image toggleImg = toggleObj.AddComponent<Image>();
        Color toggleColor = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        toggleImg.color = toggleColor;

        Button toggleBtn = toggleObj.AddComponent<Button>();
        toggleBtn.onClick.AddListener(ToggleAutoSpawn);

        GameObject toggleText = new GameObject("ToggleLabel");
        toggleText.transform.SetParent(toggleObj.transform, false);
        RectTransform ttRect = toggleText.AddComponent<RectTransform>();
        ttRect.anchorMin = Vector2.zero;
        ttRect.anchorMax = Vector2.one;
        ttRect.offsetMin = Vector2.zero;
        ttRect.offsetMax = Vector2.zero;

        Text tt = toggleText.AddComponent<Text>();
        tt.text = "자동: OFF";
        tt.alignment = TextAnchor.MiddleCenter;
        tt.fontSize = 12;
        tt.color = Color.white;
        tt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tt.horizontalOverflow = HorizontalWrapMode.Overflow;
        tt.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void SpawnRandomEnemy()
    {
        if (SpawnManager.Instance != null)
            SpawnManager.Instance.SpawnRandomEnemy();
    }

    private void ToggleAutoSpawn()
    {
        autoSpawnEnabled = !autoSpawnEnabled;
        lastAutoSpawn = Time.time;

        // 레이블 업데이트
        Text label = transform.Find("DebugSpawnPanel")?.Find("AutoToggle")?.GetComponentInChildren<Text>();
        if (label == null)
        {
            // 캔버스 자식에서 찾기 시도
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform panel = canvas.transform.Find("DebugSpawnPanel");
                if (panel != null)
                {
                    Transform toggle = panel.Find("AutoToggle");
                    if (toggle != null)
                        label = toggle.GetComponentInChildren<Text>();
                }
            }
        }

        if (label != null)
            label.text = autoSpawnEnabled ? "자동: ON" : "자동: OFF";

        Debug.Log($"[Debug] 적 자동 소환: {(autoSpawnEnabled ? "ON" : "OFF")} (간격: {autoSpawnInterval}초)");
    }
}
