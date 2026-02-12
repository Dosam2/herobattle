using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Settings")]
    [SerializeField] private float joystickRadius = 80f;
    [SerializeField] private float deadZone = 0.1f;

    [Header("Visual Settings")]
    [SerializeField] private Color outerColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color innerColor = new Color(1f, 1f, 1f, 0.6f);

    [Header("Haptic Settings")]
    [SerializeField] private bool enableHaptic = true;

    private RectTransform joystickBackground;
    private RectTransform joystickHandle;
    private Canvas parentCanvas;
    private Camera uiCamera;

    private Vector2 inputDirection;
    private bool isDragging;
    private Vector2 touchStartPos;

    public Vector2 Direction => inputDirection;
    public bool IsDragging => isDragging;

    private void Awake()
    {
        SetupJoystickUI();
    }

    private void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        // 초기에는 조이스틱을 숨깁니다
        joystickBackground.gameObject.SetActive(false);
    }

    private void SetupJoystickUI()
    {
        // 조이스틱 배경 생성 (바깥 원)
        GameObject bgObj = new GameObject("JoystickBackground");
        bgObj.transform.SetParent(transform, false);
        joystickBackground = bgObj.AddComponent<RectTransform>();
        joystickBackground.sizeDelta = new Vector2(joystickRadius * 2f, joystickRadius * 2f);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = outerColor;
        bgImage.raycastTarget = false;

        // 조이스틱 핸들 생성 (안쪽 원)
        GameObject handleObj = new GameObject("JoystickHandle");
        handleObj.transform.SetParent(joystickBackground, false);
        joystickHandle = handleObj.AddComponent<RectTransform>();
        joystickHandle.sizeDelta = new Vector2(joystickRadius * 0.8f, joystickRadius * 0.8f);

        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = innerColor;
        handleImage.raycastTarget = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 화면 하단 절반에서의 터치에만 반응합니다
        if (eventData.position.y > Screen.height * 0.5f)
            return;

        isDragging = true;
        touchStartPos = eventData.position;

        // 터치 위치에 조이스틱 표시
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform, eventData.position, uiCamera, out localPos);

        joystickBackground.gameObject.SetActive(true);
        joystickBackground.anchoredPosition = localPos;
        joystickHandle.anchoredPosition = Vector2.zero;

        // 터치 시작 시 햅틱 피드백
        if (enableHaptic)
        {
            TriggerHaptic();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        Vector2 delta = eventData.position - touchStartPos;
        float distance = delta.magnitude;

        if (distance > joystickRadius)
        {
            delta = delta.normalized * joystickRadius;
        }

        joystickHandle.anchoredPosition = delta;

        // 정규화된 방향 계산
        inputDirection = delta / joystickRadius;

        if (inputDirection.magnitude < deadZone)
        {
            inputDirection = Vector2.zero;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        inputDirection = Vector2.zero;
        joystickHandle.anchoredPosition = Vector2.zero;
        joystickBackground.gameObject.SetActive(false);
    }

    private void TriggerHaptic()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator != null)
                {
                    vibrator.Call("vibrate", 20L); // 20ms 짧은 진동
                }
            }
        }
        catch (System.Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
