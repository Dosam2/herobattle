using UnityEngine;
using UnityEngine.InputSystem;

public class HeroPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;

    private VirtualJoystick virtualJoystick;
    private Vector3 moveDirection;

    private void Start()
    {
        virtualJoystick = FindAnyObjectByType<VirtualJoystick>();
    }

    private void Update()
    {
        HandleInput();
        Move();
    }

    private void HandleInput()
    {
        Vector2 keyboard = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) keyboard.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) keyboard.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) keyboard.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) keyboard.y += 1f;
            keyboard = keyboard.normalized;
        }

        float horizontal = keyboard.x;
        float vertical = keyboard.y;
        Vector3 keyboardInput = new Vector3(horizontal, 0f, vertical).normalized;

        Vector3 joystickInput = Vector3.zero;
        if (virtualJoystick != null)
        {
            Vector2 joyDir = virtualJoystick.Direction;
            joystickInput = new Vector3(joyDir.x, 0f, joyDir.y);
        }

        moveDirection = keyboardInput.sqrMagnitude > joystickInput.sqrMagnitude
            ? keyboardInput
            : joystickInput;
    }

    private void Move()
    {
        if (moveDirection.sqrMagnitude < 0.01f)
            return;

        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
