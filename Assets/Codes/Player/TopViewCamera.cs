using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class TopViewCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private float height = 15f;
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Free Roam Settings")]
    [SerializeField] private float freeRoamSpeed = 15f;
    [SerializeField] private float touchPanSensitivity = 0.03f;

    [Header("Camera Bounds")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minBounds = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 maxBounds = new Vector2(50f, 50f);

    private bool freeRoamMode;

    // 터치/마우스 패닝 상태
    private bool isPanning;
    private Vector2 lastPanPos;

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied += EnableFreeRoam;
    }

    public void EnableFreeRoam()
    {
        freeRoamMode = true;
        Debug.Log("[Camera] 자유 이동 모드 활성화");
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        freeRoamMode = false;
    }

    private void LateUpdate()
    {
        Vector3 targetPosition;

        if (freeRoamMode)
        {
            // 1) WASD 키보드 입력(PC)
            Vector3 keyInput = GetKeyboardInput();
            if (keyInput.sqrMagnitude > 0.01f)
            {
                Vector3 current = transform.position;
                current.x += keyInput.x * freeRoamSpeed * Time.deltaTime;
                current.z += keyInput.z * freeRoamSpeed * Time.deltaTime;
                current.y = height;
                targetPosition = current;
            }
            else
            {
                targetPosition = transform.position;
                targetPosition.y = height;
            }

            // 2) 터치/마우스 드래그 패닝(모바일)
            HandleTouchPan();
        }
        else
        {
            if (target == null) return;
            targetPosition = new Vector3(target.position.x, height, target.position.z);
        }

        if (!freeRoamMode)
        {
            if (useBounds)
            {
                targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
                targetPosition.z = Mathf.Clamp(targetPosition.z, minBounds.y, maxBounds.y);
            }
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        }

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void HandleTouchPan()
    {
        // 유닛 배치 중에는 패닝하지 않음
        if (GameManager.Instance != null && GameManager.Instance.IsPlacingUnit) return;

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        // 누름 시작
        if (pointer.press.wasPressedThisFrame)
        {
            // UI를 터치 중이면 건너뜀
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            isPanning = true;
            lastPanPos = pointer.position.ReadValue();
        }

        // 누르는 동안
        if (pointer.press.isPressed && isPanning)
        {
            Vector2 currentPos = pointer.position.ReadValue();
            Vector2 delta = currentPos - lastPanPos;

            // 화면 이동량을 월드 이동으로 변환 (반대 방향 - 오른쪽으로 드래그하면 카메라가 왼쪽으로 이동)
            float sensitivity = touchPanSensitivity * (height / 15f); // 줌에 비례해 감도 스케일링
            Vector3 worldDelta = new Vector3(-delta.x * sensitivity, 0f, -delta.y * sensitivity);

            Vector3 newPos = transform.position + worldDelta;
            newPos.y = height;

            if (useBounds)
            {
                newPos.x = Mathf.Clamp(newPos.x, minBounds.x, maxBounds.x);
                newPos.z = Mathf.Clamp(newPos.z, minBounds.y, maxBounds.y);
            }

            transform.position = newPos;
            lastPanPos = currentPos;
        }

        // 해제
        if (pointer.press.wasReleasedThisFrame)
        {
            isPanning = false;
        }
    }

    private Vector3 GetKeyboardInput()
    {
        Vector2 kb = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) kb.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kb.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) kb.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) kb.y += 1f;
            kb = kb.normalized;
        }
        return new Vector3(kb.x, 0f, kb.y);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied -= EnableFreeRoam;
    }
}
