using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class TopViewCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private float height = 15f;
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Free Roam Settings")]
    [SerializeField] private float freeRoamSpeed = 15f;
    [SerializeField] private float touchPanSensitivity = 0.03f;
    [SerializeField] private float pinchZoomSensitivity = 2f;
    [SerializeField] private float minHeight = 8f;
    [SerializeField] private float maxHeight = 35f;

    [Header("Camera Bounds")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private Vector2 minBounds = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 maxBounds = new Vector2(50f, 50f);

    private bool freeRoamMode;
    private bool rotate180ForPlayer2;

    private bool isPanning;
    private Vector2 lastPanPos;
    private float lastPinchDistance = -1f;
    private bool mobilePanActive;

    private void Start()
    {
        EnhancedTouchSupport.Enable();
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerDied += EnableFreeRoam;
    }

    public void EnableFreeRoam()
    {
        freeRoamMode = true;
        Debug.Log("[Camera] ???? ??? ??? ????");
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        freeRoamMode = false;
    }

    public void SetRotate180ForPlayer2(bool value) { rotate180ForPlayer2 = value; }

    public void SetSplitScreenLeft(bool left) { }

    private void LateUpdate()
    {
        Vector3 targetPosition;

        if (freeRoamMode)
        {
            Vector3 keyInput = GetKeyboardInput();
            if (rotate180ForPlayer2)
                keyInput = new Vector3(-keyInput.x, 0f, -keyInput.z);
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

            HandleTouchPan();
            HandlePinchZoom();
        }
        else
        {
            if (target == null) return;
            targetPosition = new Vector3(target.position.x, height, target.position.z);
        }

        if (freeRoamMode)
        {
            Vector3 pos = transform.position;
            pos.y = height;
            if (useBounds)
            {
                pos.x = Mathf.Clamp(pos.x, minBounds.x, maxBounds.x);
                pos.z = Mathf.Clamp(pos.z, minBounds.y, maxBounds.y);
            }
            transform.position = pos;
        }
        else
        {
            if (useBounds)
            {
                targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
                targetPosition.z = Mathf.Clamp(targetPosition.z, minBounds.y, maxBounds.y);
            }
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        }

        float yRot = rotate180ForPlayer2 ? 180f : 0f;
        transform.rotation = Quaternion.Euler(90f, yRot, 0f);
    }

    private void HandleTouchPan()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPlacingUnit) return;

        bool deathMode = GameManager.Instance != null && GameManager.Instance.IsPlayerDead;

        if (deathMode)
        {
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 1)
            {
                Vector2 pos = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0].screenPosition;
                if (mobilePanActive)
                {
                    Vector2 delta = pos - lastPanPos;
                    float sensitivity = touchPanSensitivity * (height / 15f);
                    if (rotate180ForPlayer2) delta = -delta;
                    Vector3 worldDelta = new Vector3(-delta.x * sensitivity, 0f, -delta.y * sensitivity);
                    Vector3 newPos = transform.position + worldDelta;
                    newPos.y = height;
                    if (useBounds)
                    {
                        newPos.x = Mathf.Clamp(newPos.x, minBounds.x, maxBounds.x);
                        newPos.z = Mathf.Clamp(newPos.z, minBounds.y, maxBounds.y);
                    }
                    transform.position = newPos;
                }
                lastPanPos = pos;
                mobilePanActive = true;
                return;
            }
            if (Touchscreen.current != null)
            {
                var primary = Touchscreen.current.primaryTouch;
                var phase = primary.phase.ReadValue();
                if (phase != UnityEngine.InputSystem.TouchPhase.Ended && phase != UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    Vector2 pos = primary.position.ReadValue();
                    if (mobilePanActive)
                    {
                        Vector2 delta = pos - lastPanPos;
                        float sensitivity = touchPanSensitivity * (height / 15f);
                        if (rotate180ForPlayer2) delta = -delta;
                        Vector3 worldDelta = new Vector3(-delta.x * sensitivity, 0f, -delta.y * sensitivity);
                        Vector3 newPos = transform.position + worldDelta;
                        newPos.y = height;
                        if (useBounds)
                        {
                            newPos.x = Mathf.Clamp(newPos.x, minBounds.x, maxBounds.x);
                            newPos.z = Mathf.Clamp(newPos.z, minBounds.y, maxBounds.y);
                        }
                        transform.position = newPos;
                    }
                    lastPanPos = pos;
                    mobilePanActive = true;
                    return;
                }
            }
            mobilePanActive = false;
        }

        int touchCount = Input.touchCount;
        if (touchCount >= 2) { isPanning = false; mobilePanActive = false; return; }

        if (touchCount == 1)
        {
            UnityEngine.Touch t = Input.GetTouch(0);
            if (t.phase == UnityEngine.TouchPhase.Began)
            {
                if (!deathMode && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    return;
                isPanning = true;
                lastPanPos = t.position;
            }
            else if (t.phase == UnityEngine.TouchPhase.Moved && isPanning)
            {
                Vector2 delta = t.position - lastPanPos;
                float sensitivity = touchPanSensitivity * (height / 15f);
                if (rotate180ForPlayer2)
                    delta = -delta;
                Vector3 worldDelta = new Vector3(-delta.x * sensitivity, 0f, -delta.y * sensitivity);
                Vector3 newPos = transform.position + worldDelta;
                newPos.y = height;
                if (useBounds)
                {
                    newPos.x = Mathf.Clamp(newPos.x, minBounds.x, maxBounds.x);
                    newPos.z = Mathf.Clamp(newPos.z, minBounds.y, maxBounds.y);
                }
                transform.position = newPos;
                lastPanPos = t.position;
            }
            else if (t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled)
                isPanning = false;
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            if (!deathMode && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            if (Touchscreen.current != null && Touchscreen.current.touches.Count >= 2)
                return;
            isPanning = true;
            lastPanPos = pointer.position.ReadValue();
        }

        if (pointer.press.isPressed && isPanning)
        {
            if (Touchscreen.current != null && Touchscreen.current.touches.Count >= 2)
            {
                isPanning = false;
                return;
            }
            Vector2 currentPos = pointer.position.ReadValue();
            Vector2 delta = currentPos - lastPanPos;
            float sensitivity = touchPanSensitivity * (height / 15f);
            if (rotate180ForPlayer2)
                delta = -delta;
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

        if (pointer.press.wasReleasedThisFrame)
        {
            isPanning = false;
        }
        if (pointer.press.wasReleasedThisFrame && Touchscreen.current != null && Touchscreen.current.touches.Count < 2)
            lastPinchDistance = -1f;
    }

    private void HandlePinchZoom()
    {
        if (Input.touchCount == 2)
        {
            UnityEngine.Touch t0 = Input.GetTouch(0);
            UnityEngine.Touch t1 = Input.GetTouch(1);
            float dist = Vector2.Distance(t0.position, t1.position);
            if (lastPinchDistance > 0f)
            {
                float delta = (lastPinchDistance - dist) * pinchZoomSensitivity * 0.01f;
                height = Mathf.Clamp(height + delta, minHeight, maxHeight);
            }
            lastPinchDistance = dist;
            return;
        }

        float scroll = 0f;
        if (Mouse.current != null)
            scroll = Mouse.current.scroll.ReadValue().y;
        else
            scroll = Input.GetAxis("Mouse ScrollWheel") * 10f;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float delta = -scroll * pinchZoomSensitivity * 0.5f;
            height = Mathf.Clamp(height + delta, minHeight, maxHeight);
        }

        lastPinchDistance = -1f;

        if (Touchscreen.current == null) return;
        var touches = Touchscreen.current.touches;
        if (touches.Count != 2) return;

        float dist2 = Vector2.Distance(touches[0].position.ReadValue(), touches[1].position.ReadValue());
        if (lastPinchDistance > 0f)
        {
            float delta = (lastPinchDistance - dist2) * pinchZoomSensitivity * 0.01f;
            height = Mathf.Clamp(height + delta, minHeight, maxHeight);
        }
        lastPinchDistance = dist2;
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
