using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CardSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int cardSlotCount = 3;

    [Header("Skill Settings")]
    [SerializeField] private float skill1Cooldown = 10f;
    [SerializeField] private float skill1Duration = 5f;
    [SerializeField] private float skill1SpeedMultiplier = 1.5f;
    [SerializeField] private float skill2Cooldown = 12f;
    [SerializeField] private float skill2Damage = 50f;
    [SerializeField] private float skill2Range = 3f;

    [Header("Placement Visual")]
    [SerializeField] private Color zoneColor = new Color(0.2f, 0.8f, 0.3f, 0.15f);
    [SerializeField] private Color ghostValidColor = new Color(0.3f, 1f, 0.3f, 0.5f);
    [SerializeField] private Color ghostInvalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

    [Header("Multiplayer")]
    [SerializeField] private int playerID = 1;

    // Alive 모드
    private UnitType[] cardSlots;
    private Button[] cardButtons;
    private Button skill1Button;
    private Button skill2Button;
    private Text skill1Text;
    private Text skill2Text;
    private GameObject skillContainer;
    private GameObject aliveCardContainer;

    // Dead 모드
    private GameObject deadCardContainer;
    private Image[] deadCardImages;
    private Text[] deadCardTexts;
    private UnitType[] deadCardSlots;

    // 드래그-투-플레이스 상태
    private int draggingCardIndex = -1;
    private GameObject placementZone;
    private GameObject ghostUnit;
    private Material ghostMaterial;
    private Material zoneMaterial;

    private float skill1LastUsed = -999f;
    private float skill2LastUsed = -999f;

    private static readonly Dictionary<UnitType, Color> CardColors = new Dictionary<UnitType, Color>
    {
        { UnitType.Warrior, new Color(0.85f, 0.25f, 0.25f) },
        { UnitType.Archer, new Color(0.25f, 0.75f, 0.25f) },
        { UnitType.Rogue, new Color(0.55f, 0.2f, 0.85f) },
        { UnitType.Turret, new Color(0.85f, 0.85f, 0.2f) }
    };

    private static readonly Dictionary<UnitType, string> CardNames = new Dictionary<UnitType, string>
    {
        { UnitType.Warrior, "전사" },
        { UnitType.Archer, "궁수" },
        { UnitType.Rogue, "도적" },
        { UnitType.Turret, "포탑" }
    };

    public void SetPlayerID(int id) { playerID = id; }

    /// <summary>한글 지원 폰트 반환 (Android/Editor 공용)</summary>
    private static Font GetKoreanFont(int size)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android 시스템 기본 폰트 (CJK 포함)
        return Font.CreateDynamicFontFromOSFont("sans-serif", size);
#else
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        return Font.CreateDynamicFontFromOSFont("Arial", size);
#endif
    }

    private void Start()
    {
        CreateAliveUI();
        CreateDeadUI();
        CreatePlacementVisuals();
        RandomizeCards();

        deadCardContainer.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied += OnPlayerDied;
    }

    private void Update()
    {
        bool dead = GameManager.Instance != null && GameManager.Instance.IsPlayerDead;

        if (!dead)
        {
            UpdateSkillCooldown(skill1Button, skill1Text, "가속", skill1LastUsed, skill1Cooldown);
            UpdateSkillCooldown(skill2Button, skill2Text, "폭발", skill2LastUsed, skill2Cooldown);
        }
    }

    // ============================================================
    // 생존 모드 UI (우측 패널: 스킬 + 카드)
    // ============================================================
    private void CreateAliveUI()
    {
        RectTransform container = GetComponent<RectTransform>();

        aliveCardContainer = new GameObject("AliveContainer");
        aliveCardContainer.transform.SetParent(container, false);
        RectTransform aliveRect = aliveCardContainer.AddComponent<RectTransform>();
        aliveRect.anchorMin = Vector2.zero;
        aliveRect.anchorMax = Vector2.one;
        aliveRect.offsetMin = Vector2.zero;
        aliveRect.offsetMax = Vector2.zero;

        cardSlots = new UnitType[cardSlotCount];
        cardButtons = new Button[cardSlotCount];

        // 하단에 스킬
        skillContainer = new GameObject("SkillContainer");
        skillContainer.transform.SetParent(aliveRect, false);
        RectTransform skillRect = skillContainer.AddComponent<RectTransform>();
        skillRect.anchorMin = new Vector2(0f, 0f);
        skillRect.anchorMax = new Vector2(1f, 0f);
        skillRect.pivot = new Vector2(0.5f, 0f);
        skillRect.anchoredPosition = new Vector2(0f, 10f);
        skillRect.sizeDelta = new Vector2(0f, 40f);

        skill1Button = CreateSkillButton("Skill1Btn", "가속", new Color(0.2f, 0.7f, 0.9f),
            new Vector2(0.02f, 0f), new Vector2(0.48f, 1f), skillRect, out skill1Text);
        skill1Button.onClick.AddListener(OnSkill1Pressed);

        skill2Button = CreateSkillButton("Skill2Btn", "폭발", new Color(0.9f, 0.3f, 0.15f),
            new Vector2(0.52f, 0f), new Vector2(0.98f, 1f), skillRect, out skill2Text);
        skill2Button.onClick.AddListener(OnSkill2Pressed);

        // 스킬 위에 카드 배치
        float cardStartY = 10f + 40f + 8f;
        for (int i = 0; i < cardSlotCount; i++)
        {
            int idx = i;
            float yPos = cardStartY + i * (60f + 8f);
            cardButtons[i] = CreateCardButton($"Card_{i}", "", Color.gray, yPos, aliveRect);
            cardButtons[i].onClick.AddListener(() => OnAliveCardPressed(idx));
        }
    }

    // ============================================================
    // 사망 모드 UI - 하단 중앙의 3개 컴팩트 카드 (드래그로 배치)
    // ============================================================
    private void CreateDeadUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform uiParent = canvas != null ? canvas.transform : transform;

        deadCardContainer = new GameObject("DeadContainer");
        deadCardContainer.transform.SetParent(uiParent, false);
        RectTransform deadRect = deadCardContainer.AddComponent<RectTransform>();
        // 컴팩트: 하단 중앙, 높이 55px (Android 제스처 영역 회피를 위해 위로 올림)
        deadRect.anchorMin = new Vector2(0.1f, 0f);
        deadRect.anchorMax = new Vector2(0.9f, 0f);
        deadRect.pivot = new Vector2(0.5f, 0f);
        deadRect.anchoredPosition = new Vector2(0f, 80f); // 80px 위로 올려서 네비게이션 바 회피
        deadRect.sizeDelta = new Vector2(0f, 55f);

        deadCardSlots = new UnitType[cardSlotCount];
        deadCardImages = new Image[cardSlotCount];
        deadCardTexts = new Text[cardSlotCount];

        for (int i = 0; i < cardSlotCount; i++)
        {
            int idx = i;
            float xMin = (float)i / cardSlotCount + 0.008f;
            float xMax = (float)(i + 1) / cardSlotCount - 0.008f;

            GameObject cardObj = new GameObject($"DeadCard_{i}");
            cardObj.transform.SetParent(deadRect, false);

            RectTransform rect = cardObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            deadCardImages[i] = cardObj.AddComponent<Image>();
            deadCardImages[i].color = Color.gray;

            // 드래그-투-배치용 EventTrigger (Button 불필요)
            EventTrigger trigger = cardObj.AddComponent<EventTrigger>();
            AddTriggerEvent(trigger, EventTriggerType.PointerDown, (data) => OnDeadCardPointerDown(idx, (PointerEventData)data));
            AddTriggerEvent(trigger, EventTriggerType.Drag, (data) => OnDeadCardDrag(idx, (PointerEventData)data));
            AddTriggerEvent(trigger, EventTriggerType.PointerUp, (data) => OnDeadCardPointerUp(idx, (PointerEventData)data));

            // 레이블
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(cardObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            deadCardTexts[i] = textObj.AddComponent<Text>();
            deadCardTexts[i].text = "";
            deadCardTexts[i].alignment = TextAnchor.MiddleCenter;
            deadCardTexts[i].fontSize = 18;
            deadCardTexts[i].color = Color.white;
            deadCardTexts[i].fontStyle = FontStyle.Bold;
            deadCardTexts[i].font = GetKoreanFont(18);
            deadCardTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            deadCardTexts[i].verticalOverflow = VerticalWrapMode.Overflow;
        }
    }

    private void AddTriggerEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    // ============================================================
    // 버튼에서 드래그-투-배치 처리 (PointerDown → Drag → PointerUp)
    // ============================================================
    private void OnDeadCardPointerDown(int cardIndex, PointerEventData eventData)
    {
        draggingCardIndex = cardIndex;

        RefreshPlacementZoneBounds();
        placementZone.SetActive(true);
        ghostUnit.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.IsPlacingUnit = true;

        // 눌린 카드 강조, 나머지는 어둡게
        for (int i = 0; i < cardSlotCount; i++)
        {
            deadCardImages[i].color = (i == cardIndex)
                ? CardColors[deadCardSlots[i]] * 1.4f
                : CardColors[deadCardSlots[i]] * 0.5f;
        }
    }

    private void OnDeadCardDrag(int cardIndex, PointerEventData eventData)
    {
        if (draggingCardIndex < 0) return;

        Vector3 worldPos = ScreenToGround(eventData.position);
        if (worldPos == Vector3.zero) return;

        // 유령 유닛 표시
        if (!ghostUnit.activeSelf)
            ghostUnit.SetActive(true);

        Vector3 clamped = GameManager.Instance != null
            ? GameManager.Instance.ClampToPlacementZone(worldPos, playerID)
            : worldPos;

        UnitStats stats = UnitDatabase.GetStats(deadCardSlots[draggingCardIndex]);
        ghostUnit.transform.position = new Vector3(clamped.x, stats.scale.y * 0.5f, clamped.z);
        ghostUnit.transform.localScale = stats.scale;

        bool valid = GameManager.Instance != null
            ? GameManager.Instance.IsPositionInPlacementZone(worldPos, playerID)
            : true;
        ghostMaterial.color = valid ? ghostValidColor : ghostInvalidColor;
    }

    private void OnDeadCardPointerUp(int cardIndex, PointerEventData eventData)
    {
        if (draggingCardIndex < 0) return;

        Vector3 worldPos = ScreenToGround(eventData.position);
        bool valid = worldPos != Vector3.zero
            && (GameManager.Instance == null || GameManager.Instance.IsPositionInPlacementZone(worldPos, playerID));

        if (valid && ghostUnit.activeSelf)
        {
            Vector3 clamped = GameManager.Instance != null
                ? GameManager.Instance.ClampToPlacementZone(worldPos, playerID)
                : worldPos;

            if (SpawnManager.Instance != null)
                SpawnManager.Instance.SpawnUnitAtPosition(deadCardSlots[draggingCardIndex], clamped, playerID);

            // 해당 카드를 재무작위화
            UnitType[] allTypes = UnitDatabase.AllTypes;
            deadCardSlots[draggingCardIndex] = allTypes[Random.Range(0, allTypes.Length)];
            UpdateDeadCardVisual(draggingCardIndex);
        }

        // 배치 종료
        draggingCardIndex = -1;
        placementZone.SetActive(false);
        ghostUnit.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.IsPlacingUnit = false;

        // 모든 카드 비주얼 리셋
        for (int i = 0; i < cardSlotCount; i++)
            UpdateDeadCardVisual(i);
    }

    // ============================================================
    // 배치 시각화
    // ============================================================
    /// <summary>현재 playerID 기준으로 배치 존 위치/크기 갱신 (P2 사망 시 등)</summary>
    private void RefreshPlacementZoneBounds()
    {
        float gm_minZ = -35f, gm_maxZ = 0f, gm_minX = -15f, gm_maxX = 15f;
        if (GameManager.Instance != null)
        {
            gm_minZ = GameManager.Instance.GetPlacementMinZ(playerID);
            gm_maxZ = GameManager.Instance.GetPlacementMaxZ(playerID);
            gm_minX = GameManager.Instance.GetPlacementMinX(playerID);
            gm_maxX = GameManager.Instance.GetPlacementMaxX(playerID);
        }
        if (placementZone != null)
        {
            float centerX = (gm_minX + gm_maxX) * 0.5f;
            float centerZ = (gm_minZ + gm_maxZ) * 0.5f;
            float sizeX = (gm_maxX - gm_minX) / 10f;
            float sizeZ = (gm_maxZ - gm_minZ) / 10f;
            placementZone.transform.position = new Vector3(centerX, 0.05f, centerZ);
            placementZone.transform.localScale = new Vector3(sizeX, 1f, sizeZ);
        }
    }

    private void CreatePlacementVisuals()
    {
        float gm_minZ = -35f, gm_maxZ = 0f, gm_minX = -15f, gm_maxX = 15f;
        if (GameManager.Instance != null)
        {
            gm_minZ = GameManager.Instance.GetPlacementMinZ(playerID);
            gm_maxZ = GameManager.Instance.GetPlacementMaxZ(playerID);
            gm_minX = GameManager.Instance.GetPlacementMinX(playerID);
            gm_maxX = GameManager.Instance.GetPlacementMaxX(playerID);
        }

        placementZone = GameObject.CreatePrimitive(PrimitiveType.Plane);
        placementZone.name = "PlacementZone";
        UnityEngine.Object.Destroy(placementZone.GetComponent<Collider>());

        float centerX = (gm_minX + gm_maxX) * 0.5f;
        float centerZ = (gm_minZ + gm_maxZ) * 0.5f;
        float sizeX = (gm_maxX - gm_minX) / 10f;
        float sizeZ = (gm_maxZ - gm_minZ) / 10f;
        placementZone.transform.position = new Vector3(centerX, 0.05f, centerZ);
        placementZone.transform.localScale = new Vector3(sizeX, 1f, sizeZ);

        zoneMaterial = CreateTransparentMaterial(zoneColor);
        placementZone.GetComponent<MeshRenderer>().material = zoneMaterial;
        placementZone.SetActive(false);

        // 유령 유닛
        ghostUnit = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghostUnit.name = "GhostUnit";
        Object.Destroy(ghostUnit.GetComponent<Collider>());
        ghostMaterial = CreateTransparentMaterial(ghostValidColor);
        ghostUnit.GetComponent<MeshRenderer>().material = ghostMaterial;
        ghostUnit.SetActive(false);
    }

    private Material CreateTransparentMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = color;
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_AlphaClip", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        return mat;
    }

    private Vector3 ScreenToGround(Vector2 screenPos)
    {
        Camera cam = playerID == 2 ? GetPlayer2Camera() : null;
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector3.zero;
        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float dist))
            return ray.GetPoint(dist);
        return Vector3.zero;
    }

    private Camera GetPlayer2Camera()
    {
        GameObject go = GameObject.Find("Player2Camera");
        return go != null ? go.GetComponent<Camera>() : null;
    }

    // ============================================================
    // 버튼 생성 헬퍼
    // ============================================================
    private Button CreateSkillButton(string objName, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax, RectTransform parent, out Text labelText)
    {
        GameObject btnObj = new GameObject(objName);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = color;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color * 1.15f;
        cb.pressedColor = color * 0.7f;
        cb.selectedColor = color;
        cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        btn.colors = cb;

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        labelText = textObj.AddComponent<Text>();
        labelText.text = label;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 16;
        labelText.color = Color.white;
        labelText.fontStyle = FontStyle.Bold;
        labelText.font = GetKoreanFont(16);
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelText.verticalOverflow = VerticalWrapMode.Overflow;

        return btn;
    }

    private Button CreateCardButton(string objName, string label, Color color, float yPos, RectTransform parent)
    {
        GameObject btnObj = new GameObject(objName);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0f);
        rect.anchorMax = new Vector2(0.95f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, yPos);
        rect.sizeDelta = new Vector2(0f, 60f);

        Image img = btnObj.AddComponent<Image>();
        img.color = color;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = color;
        cb.highlightedColor = color * 1.15f;
        cb.pressedColor = color * 0.7f;
        cb.selectedColor = color;
        btn.colors = cb;

        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text uiText = textObj.AddComponent<Text>();
        uiText.text = label;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.fontSize = 24;
        uiText.color = Color.white;
        uiText.fontStyle = FontStyle.Bold;
        uiText.font = GetKoreanFont(24);
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;

        return btn;
    }

    // ============================================================
    // 쿨다운 표시
    // ============================================================
    private void UpdateSkillCooldown(Button btn, Text label, string skillName, float lastUsed, float cooldown)
    {
        if (btn == null || label == null) return;
        float remaining = cooldown - (Time.time - lastUsed);
        if (remaining > 0f)
        {
            btn.interactable = false;
            label.text = $"{skillName}\n{remaining:F0}s";
        }
        else
        {
            btn.interactable = true;
            label.text = skillName;
        }
    }

    // ============================================================
    // 카드 로직
    // ============================================================
    public void RandomizeCards()
    {
        UnitType[] allTypes = UnitDatabase.AllTypes;
        for (int i = 0; i < cardSlotCount; i++)
        {
            cardSlots[i] = allTypes[Random.Range(0, allTypes.Length)];
            UpdateAliveCardVisual(i);
        }
        for (int i = 0; i < cardSlotCount; i++)
        {
            deadCardSlots[i] = allTypes[Random.Range(0, allTypes.Length)];
            UpdateDeadCardVisual(i);
        }
    }

    private void UpdateAliveCardVisual(int index)
    {
        UnitType type = cardSlots[index];
        Color c = CardColors[type];
        Image img = cardButtons[index].GetComponent<Image>();
        img.color = c;
        Text text = cardButtons[index].GetComponentInChildren<Text>();
        if (text != null) text.text = CardNames[type];
        ColorBlock cb = cardButtons[index].colors;
        cb.normalColor = c; cb.highlightedColor = c * 1.15f;
        cb.pressedColor = c * 0.7f; cb.selectedColor = c;
        cardButtons[index].colors = cb;
    }

    private void UpdateDeadCardVisual(int index)
    {
        UnitType type = deadCardSlots[index];
        Color c = CardColors[type];
        deadCardImages[index].color = c;
        deadCardTexts[index].text = CardNames[type];
    }

    // ============================================================
    // 버튼 콜백
    // ============================================================
    private void OnAliveCardPressed(int index)
    {
        if (SpawnManager.Instance == null) return;
        SpawnManager.Instance.SpawnUnit(cardSlots[index], playerID);
        UnitType[] allTypes = UnitDatabase.AllTypes;
        cardSlots[index] = allTypes[Random.Range(0, allTypes.Length)];
        UpdateAliveCardVisual(index);
    }

    private void OnSkill1Pressed()
    {
        if (Time.time - skill1LastUsed < skill1Cooldown) return;
        skill1LastUsed = Time.time;
        if (GameManager.Instance != null)
            GameManager.Instance.BuffAllUnitsSpeed(skill1SpeedMultiplier, skill1Duration, playerID);
    }

    private void OnSkill2Pressed()
    {
        if (Time.time - skill2LastUsed < skill2Cooldown) return;
        skill2LastUsed = Time.time;

        GameObject player = playerID == 1 ? GameObject.FindGameObjectWithTag("Player") : GameObject.Find("Player2");
        if (player == null) return;

        Collider[] hits = Physics.OverlapSphere(player.transform.position, skill2Range);
        int hitCount = 0;
        foreach (Collider hit in hits)
        {
            if (hit.transform == player.transform) continue;
            bool isEnemy = playerID == 1 && (hit.CompareTag("Enemy") || hit.name.Contains("Player2"))
                || playerID == 2 && (hit.name.Contains("Player") && !hit.name.Contains("Player2") || hit.GetComponent<UnitBase>()?.OwnerPlayerID == 1);
            if (!isEnemy) continue;
            Damageable dmg = hit.GetComponent<Damageable>();
            if (dmg != null && !dmg.IsDead)
            {
                dmg.TakeDamage(skill2Damage);
                hitCount++;
            }
        }
        Debug.Log($"[Skill2] 폭발! {hitCount}명에게 {skill2Damage} 데미지!");
    }

    // ============================================================
    // 플레이어 사망 전환
    // ============================================================
    private void OnPlayerDied()
    {
        // 생존 모드(스킬 + 우측 카드) 숨김
        if (aliveCardContainer != null)
            aliveCardContainer.SetActive(false);

        // CardPanel 배경(hidden)
        Image panelBg = GetComponent<Image>();
        if (panelBg != null) panelBg.enabled = false;

        // 사망 모드(하단 카드) 표시
        if (deadCardContainer != null)
            deadCardContainer.SetActive(true);

        Debug.Log("[CardSystem] 사망 모드 - 버튼 드래그로 유닛 배치!");
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied -= OnPlayerDied;

        if (ghostMaterial != null) Object.Destroy(ghostMaterial);
        if (zoneMaterial != null) Object.Destroy(zoneMaterial);
        if (ghostUnit != null) Object.Destroy(ghostUnit);
        if (placementZone != null) Object.Destroy(placementZone);
    }
}
