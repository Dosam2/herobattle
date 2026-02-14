using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// 클래시로얄 스타일 1:1 PVP 멀티플레이.
/// 각 플레이어는 항상 "내가 아래, 적이 위"인 시점을 가짐.
/// 
/// 서버 백엔드 옵션:
///   1. Colyseus (NAS Docker) ← 기본
///   2. 로컬 매칭 (테스트용)
/// </summary>
public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }

    public enum MatchState { Idle, Matchmaking, Matched, InGame }

    [Header("Map Layout (Clash Royale style)")]
    [SerializeField] private float mapHalfLength = 35f;
    [SerializeField] private float baseZ = 35f;
    [SerializeField] private float playerSpawnZ = 30f;

    public MatchState CurrentState { get; private set; } = MatchState.Idle;
    public bool IsMultiplayerMode => CurrentState == MatchState.InGame;
    public int LocalPlayerID { get; private set; } = 1;

    public event Action<MatchState> OnMatchStateChanged;
    public event Action OnMatchStarted;

    private Coroutine matchmakingRoutine;
    private GameObject player2Root;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [Header("Online - 서버 선택")]
    [Tooltip("Colyseus 서버 사용 (NAS Docker)")]
    [SerializeField] private bool useColyseusServer = true;

    // ═══════════════════════════════════════
    // 매치메이킹 시작
    // ═══════════════════════════════════════

    public void StartMatchmaking()
    {
        if (CurrentState == MatchState.Matchmaking) return;
        CurrentState = MatchState.Matchmaking;
        OnMatchStateChanged?.Invoke(CurrentState);

        // Colyseus 서버 사용
        if (useColyseusServer && ColyseusManager.Instance != null)
        {
            SubscribeColyseus();
            ColyseusManager.Instance.Connect();
            return;
        }

        // 로컬 매칭 (테스트용 폴백)
        matchmakingRoutine = StartCoroutine(MatchmakingCoroutine());
    }

    // ═══════════════════════════════════════
    // Colyseus 이벤트 구독
    // ═══════════════════════════════════════

    private void SubscribeColyseus()
    {
        if (ColyseusManager.Instance == null) return;
        ColyseusManager.Instance.OnJoinedRoom += OnColyseusJoinedRoom;
        ColyseusManager.Instance.OnGameStart += OnColyseusGameStart;
        ColyseusManager.Instance.OnPlayerLeft += OnColyseusPlayerLeft;
        ColyseusManager.Instance.OnGameEnded += OnColyseusGameEnded;
        ColyseusManager.Instance.OnError += OnColyseusError;
    }

    private void UnsubscribeColyseus()
    {
        if (ColyseusManager.Instance == null) return;
        ColyseusManager.Instance.OnJoinedRoom -= OnColyseusJoinedRoom;
        ColyseusManager.Instance.OnGameStart -= OnColyseusGameStart;
        ColyseusManager.Instance.OnPlayerLeft -= OnColyseusPlayerLeft;
        ColyseusManager.Instance.OnGameEnded -= OnColyseusGameEnded;
        ColyseusManager.Instance.OnError -= OnColyseusError;
    }

    private void OnColyseusJoinedRoom(int playerNumber)
    {
        LocalPlayerID = playerNumber;
        CurrentState = MatchState.Matched;
        OnMatchStateChanged?.Invoke(CurrentState);
        Debug.Log($"[Multiplayer] Colyseus 룸 입장 P{playerNumber}. (2명 모이면 게임 시작)");
    }

    private void OnColyseusGameStart()
    {
        CurrentState = MatchState.InGame;
        OnMatchStateChanged?.Invoke(CurrentState);
        OnMatchStarted?.Invoke();
        CreatePlayer2Only();
        Debug.Log("[Multiplayer] Colyseus 게임 시작!");
    }

    private void OnColyseusPlayerLeft(int playerNumber)
    {
        Debug.Log($"[Multiplayer] Colyseus: Player {playerNumber} 퇴장");
        // 게임 중 상대방이 나가면 승리 처리
        if (CurrentState == MatchState.InGame && playerNumber != LocalPlayerID)
        {
            Debug.Log("[Multiplayer] 상대 퇴장으로 승리!");
            if (GameManager.Instance != null)
                GameManager.Instance.EnemyBaseDestroyed();
        }
    }

    private void OnColyseusGameEnded(int winner)
    {
        Debug.Log($"[Multiplayer] Colyseus 게임 종료! 승자: Player {winner}");
        if (winner == LocalPlayerID)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.EnemyBaseDestroyed();
        }
    }

    private void OnColyseusError(string message)
    {
        Debug.LogWarning($"[Multiplayer] Colyseus 에러: {message}");
        CurrentState = MatchState.Idle;
        OnMatchStateChanged?.Invoke(CurrentState);
    }

    // ═══════════════════════════════════════
    // 로컬 매칭 (폴백)
    // ═══════════════════════════════════════

    private IEnumerator MatchmakingCoroutine()
    {
        Debug.Log("[Multiplayer] 로컬 매칭 중...");
        yield return new WaitForSeconds(1.5f);

        CurrentState = MatchState.Matched;
        OnMatchStateChanged?.Invoke(CurrentState);
        Debug.Log("[Multiplayer] 매칭 완료!");

        yield return new WaitForSeconds(0.3f);
        StartMultiplayerGame();
        matchmakingRoutine = null;
    }

    private void StartMultiplayerGame()
    {
        LocalPlayerID = 1;
        CurrentState = MatchState.InGame;
        OnMatchStateChanged?.Invoke(CurrentState);
        OnMatchStarted?.Invoke();

        CreatePlayer2Only();
        Debug.Log("[Multiplayer] 1:1 PVP 시작 (EnemyBase = P2 기지)");
    }

    // ═══════════════════════════════════════
    // Player2 생성
    // ═══════════════════════════════════════

    /// <summary>
    /// Player2 캐릭터·기지만 생성. 화면 분할 없음.
    /// 한 화면 전체는 P1 뷰, P2는 방향키로 조작.
    /// </summary>
    [Header("Base (P2 기지 = EnemyBase)")]
    [SerializeField] private float baseDefenseRange = 15f;
    [SerializeField] private float baseAttackDamage = 20f;
    [SerializeField] private float baseAttackCooldown = 1.2f;

    private void CreatePlayer2Only()
    {
        GameObject playerBase = GameObject.Find("PlayerBase");
        Vector3 baseScale = playerBase != null ? playerBase.transform.localScale : new Vector3(6f, 2f, 4f);

        // 1:1 매칭 시 씬에 있던 기존 EnemyBase 제거 → 2P 기지만 존재
        GameObject existingEnemyBase = GameObject.Find("EnemyBase");
        if (existingEnemyBase != null)
        {
            UnityEngine.Object.Destroy(existingEnemyBase);
            Debug.Log("[Multiplayer] 씬 기존 EnemyBase 제거, 2P 기지로 대체.");
        }

        player2Root = new GameObject("Player2Root");

        // Player2 캐릭터 (맵 위쪽 = Z+)
        GameObject player2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player2.name = "Player2";
        player2.tag = "Enemy";
        player2.transform.SetParent(player2Root.transform);
        player2.transform.position = new Vector3(0f, 0.5f, playerSpawnZ);
        player2.transform.localScale = new Vector3(1f, 1f, 1f);

        MeshRenderer p2Mr = player2.GetComponent<MeshRenderer>();
        if (p2Mr != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.9f, 0.3f, 0.2f);
            p2Mr.material = mat;
        }

        player2.AddComponent<HeroPlayerController>();
        player2.AddComponent<PlayerAutoAttack>();
        player2.AddComponent<PlayerSetup>();

        Damageable p2Hp = player2.GetComponent<Damageable>();
        if (p2Hp == null) p2Hp = player2.AddComponent<Damageable>();
        p2Hp.SetMaxHP(100f);

        // P2 기지 = P1 기지(PlayerBase)와 동일 크기·시각으로 통일
        GameObject p2BaseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p2BaseObj.name = "EnemyBase";
        p2BaseObj.tag = "Enemy";
        p2BaseObj.transform.SetParent(player2Root.transform);
        p2BaseObj.transform.position = new Vector3(0f, baseScale.y * 0.5f, baseZ);
        p2BaseObj.transform.localScale = baseScale;
        Collider col = p2BaseObj.GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        Damageable baseHp = p2BaseObj.AddComponent<Damageable>();
        baseHp.SetMaxHP(500f);

        MainBase p2Base = p2BaseObj.AddComponent<MainBase>();
        p2Base.SetIsPlayerBase(true);
        p2Base.SetBaseOwnerId(2);
        p2Base.SetDefenseRange(baseDefenseRange);
        p2Base.SetAttackParams(baseAttackDamage, baseAttackCooldown);

        if (playerBase != null)
            playerBase.tag = "Player";

        // 방 시작 시 양쪽 모두 기지 앞에서 시작 (P1·P2 클라이언트 모두 적용)
        GameObject p1 = GameObject.FindGameObjectWithTag("Player");
        if (p1 != null)
            p1.transform.position = GetPlayerSpawnPosition(1);
        GameObject p2go = GameObject.Find("Player2");
        if (p2go != null)
            p2go.transform.position = GetPlayerSpawnPosition(2);

        // 멀티플레이 시 카드/스킬은 로컬 플레이어 번호 사용
        var cardSystem = UnityEngine.Object.FindAnyObjectByType<CardSystem>();
        if (cardSystem != null)
            cardSystem.SetPlayerID(LocalPlayerID);

        // P1 기지도 동일한 방어 사거리/공격력 적용 (레이더·사거리 통일)
        if (playerBase != null)
        {
            MainBase p1Base = playerBase.GetComponent<MainBase>();
            if (p1Base != null)
            {
                p1Base.SetDefenseRange(baseDefenseRange);
                p1Base.SetAttackParams(baseAttackDamage, baseAttackCooldown);
            }
        }

        // 조종 대상: P1은 Player, P2는 Player2. Start() 타이밍에 의존하지 않고 명시 설정
        HeroPlayerController p1Ctrl = p1 != null ? p1.GetComponent<HeroPlayerController>() : null;
        HeroPlayerController p2Ctrl = p2go != null ? p2go.GetComponent<HeroPlayerController>() : null;
        if (p1Ctrl != null) p1Ctrl.SetRemotePlayer(LocalPlayerID != 1);
        if (p2Ctrl != null) p2Ctrl.SetRemotePlayer(LocalPlayerID != 2);

        // 카메라는 로컬 플레이어만 추적
        Transform localPlayerTransform = LocalPlayerID == 1 ? (p1 != null ? p1.transform : null) : (p2go != null ? p2go.transform : null);
        if (localPlayerTransform != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var topView = cam.GetComponent<TopViewCamera>();
                if (topView != null)
                {
                    topView.SetTarget(localPlayerTransform);
                    topView.SetRotate180ForPlayer2(LocalPlayerID == 2);
                }
            }
        }
    }

    // ═══════════════════════════════════════
    // 멀티플레이 종료
    // ═══════════════════════════════════════

    public void EndMultiplayer()
    {
        UnsubscribeColyseus();
        if (ColyseusManager.Instance != null)
            ColyseusManager.Instance.Disconnect();

        if (matchmakingRoutine != null)
        {
            StopCoroutine(matchmakingRoutine);
            matchmakingRoutine = null;
        }

        // P2 유닛이 로컬 플레이어를 공격하는 것 방지: 먼저 P2 소유 유닛 전부 제거
        var allUnits = UnityEngine.Object.FindObjectsByType<UnitBase>(FindObjectsSortMode.None);
        foreach (var u in allUnits)
        {
            if (u != null && u.OwnerPlayerID == 2)
                UnityEngine.Object.Destroy(u.gameObject);
        }

        if (player2Root != null)
        {
            UnityEngine.Object.Destroy(player2Root);
            player2Root = null;
        }

        Camera main = Camera.main;
        if (main != null)
        {
            main.rect = new Rect(0f, 0f, 1f, 1f);
            var topView = main.GetComponent<TopViewCamera>();
            if (topView != null)
                topView.SetSplitScreenLeft(false);
        }

        CurrentState = MatchState.Idle;
        OnMatchStateChanged?.Invoke(CurrentState);
        Debug.Log("[Multiplayer] 멀티플레이 종료 → 씬 재시작");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ═══════════════════════════════════════
    // 유틸리티 (GameManager에서 사용)
    // ═══════════════════════════════════════

    public float GetMapHalfLength() => mapHalfLength;
    public Vector3 GetPlayerBasePosition(int playerId) => playerId == 1 ? new Vector3(0f, 1f, -baseZ) : new Vector3(0f, 1f, baseZ);
    public Vector3 GetEnemyBasePosition(int playerId) => playerId == 1 ? new Vector3(0f, 1f, baseZ) : new Vector3(0f, 1f, -baseZ);
    public Vector3 GetPlayerSpawnPosition(int playerId) => playerId == 1 ? new Vector3(0f, 0.5f, -playerSpawnZ) : new Vector3(0f, 0.5f, playerSpawnZ);
}
