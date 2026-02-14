using UnityEngine;
using UnityEngine.InputSystem;

public class HeroPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;

    private VirtualJoystick virtualJoystick;
    private Vector3 moveDirection;
    private bool isDead;
    private int playerID = 1;
    private bool isRemotePlayer = false; // 원격 플레이어(상대방)인지 여부

    private void Start()
    {
        bool isP2 = gameObject.name.Contains("Player2");
        if (isP2)
            playerID = 2;
        virtualJoystick = FindAnyObjectByType<VirtualJoystick>();

        // 멀티플레이 시: 게임 시작 후 MultiplayerManager가 SetRemotePlayer로 덮어씀
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsMultiplayerMode)
        {
            int localId = MultiplayerManager.Instance.LocalPlayerID;
            isRemotePlayer = (playerID != localId);
        }

        Damageable hp = GetComponent<Damageable>();
        if (hp != null)
            hp.OnDeath += OnPlayerDeath;
    }

    private void Update()
    {
        if (isDead) return;
        if (isRemotePlayer) return; // 원격 플레이어는 NetworkSync가 위치를 관리

        HandleInput();
        Move();
    }

    private void HandleInput()
    {
        Vector2 keyboard = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (playerID == 1)
            {
                if (Keyboard.current.aKey.isPressed) keyboard.x -= 1f;
                if (Keyboard.current.dKey.isPressed) keyboard.x += 1f;
                if (Keyboard.current.sKey.isPressed) keyboard.y -= 1f;
                if (Keyboard.current.wKey.isPressed) keyboard.y += 1f;
            }
            else
            {
                if (Keyboard.current.leftArrowKey.isPressed) keyboard.x -= 1f;
                if (Keyboard.current.rightArrowKey.isPressed) keyboard.x += 1f;
                if (Keyboard.current.downArrowKey.isPressed) keyboard.y -= 1f;
                if (Keyboard.current.upArrowKey.isPressed) keyboard.y += 1f;
            }
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

        if (playerID == 2)
            moveDirection = new Vector3(-moveDirection.x, 0f, -moveDirection.z);
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

    /// <summary>멀티플레이 시 누가 로컬/원격인지 명시적으로 설정 (Start 타이밍 무관)</summary>
    public void SetRemotePlayer(bool remote)
    {
        isRemotePlayer = remote;
        if (!remote && virtualJoystick == null)
            virtualJoystick = FindAnyObjectByType<VirtualJoystick>();
    }

    private void OnPlayerDeath()
    {
        isDead = true;

        // 비주얼 비활성화
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers) r.enabled = false;

        // 콜라이더 비활성화
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 자동 공격 비활성화
        PlayerAutoAttack autoAttack = GetComponent<PlayerAutoAttack>();
        if (autoAttack != null) autoAttack.enabled = false;

        // 로컬 플레이어가 죽었을 때만 사망 모드 전환
        if (!isRemotePlayer)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PlayerDied();

            if (virtualJoystick != null)
                virtualJoystick.gameObject.SetActive(false);

            Debug.Log("[Player] 로컬 플레이어 사망 - 사망 모드 전환");
        }
        else
        {
            Debug.Log("[Player] 상대 플레이어 사망");
        }
    }
}
