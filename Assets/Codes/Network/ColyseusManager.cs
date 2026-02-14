// ═══════════════════════════════════════════════════════════
// HeroBattle Colyseus 클라이언트 매니저
// NasServerClient를 대체하며, Colyseus SDK를 사용하여
// 시놀로지 NAS Docker 서버에 접속합니다.
//
// 필수: Colyseus Unity SDK (UPM)
//   Window → Package Manager → "+" → Add package from git URL
//   https://github.com/colyseus/colyseus-unity-sdk.git#upm
// ═══════════════════════════════════════════════════════════
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;

/// <summary>
/// Colyseus 서버 연결, 룸 입장, 메시지 송수신을 담당하는 싱글톤 매니저.
/// </summary>
public class ColyseusManager : MonoBehaviour
{
    public static ColyseusManager Instance { get; private set; }

    // ─── Inspector 설정 ───
    [Header("Colyseus Server (NAS Docker)")]
    [Tooltip("시놀로지 NAS 주소 (도메인 또는 IP)")]
    [SerializeField] private string serverHost = "mousemoong.synology.me";
    [Tooltip("Colyseus 게임 서버 포트")]
    [SerializeField] private int serverPort = 7777;
    [Tooltip("룸 이름 (서버에서 define한 이름)")]
    [SerializeField] private string roomName = "game_room";

    // ─── 상태 ───
    public bool IsConnected => _room != null;
    public int LocalPlayerNumber { get; private set; }
    public bool IsInRoom => _room != null;
    public string SessionId => _room?.SessionId ?? "";
    public NetworkGameState GameState => _room?.State;

    // ─── 이벤트 ───
    /// <summary>룸에 성공적으로 입장했을 때 (playerNumber 전달)</summary>
    public event Action<int> OnJoinedRoom;
    /// <summary>2명 매칭 완료, 게임 시작</summary>
    public event Action OnGameStart;
    /// <summary>상대방 퇴장</summary>
    public event Action<int> OnPlayerLeft;
    /// <summary>게임 종료 (winner playerNumber)</summary>
    public event Action<int> OnGameEnded;
    /// <summary>상태 변경 시</summary>
    public event Action<NetworkGameState> OnStateChanged;
    /// <summary>에러 발생</summary>
    public event Action<string> OnError;
    /// <summary>범용 메시지 수신 (type, payload)</summary>
    public event Action<string, Dictionary<string, object>> OnMessage;

    // ─── 내부 ───
    private Client _client;
    private Room<NetworkGameState> _room;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>서버 주소 설정 (인스펙터 외에서 변경 시)</summary>
    public void SetServer(string host, int port)
    {
        serverHost = host;
        serverPort = port;
    }

    /// <summary>서버 URL 생성 (Colyseus SDK는 HTTP URL을 받아서 내부에서 WS로 변환)</summary>
    private string GetServerUrl()
    {
        return $"http://{serverHost}:{serverPort}";
    }

    // ═══════════════════════════════════════
    // 접속 & 룸 입장
    // ═══════════════════════════════════════

    /// <summary>
    /// Colyseus 서버에 연결하고 game_room에 JoinOrCreate합니다.
    /// </summary>
    public async void Connect()
    {
        if (_room != null)
        {
            Debug.Log("[Colyseus] 이미 룸에 접속 중입니다.");
            return;
        }

        try
        {
            string url = GetServerUrl();
            Debug.Log($"[Colyseus] 서버 접속 시도: {url}");

            _client = new Client(url);

            // JoinOrCreate: 빈 룸이 있으면 입장, 없으면 새로 생성
            _room = await _client.JoinOrCreate<NetworkGameState>(roomName);

            Debug.Log($"[Colyseus] 룸 입장 성공! RoomId={_room.RoomId}, SessionId={_room.SessionId}");

            // ── 이벤트 바인딩 ──
            RegisterRoomEvents();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Colyseus] 접속 실패: {e.Message}");
            OnError?.Invoke(e.Message);
            _room = null;
        }
    }

    /// <summary>
    /// 특정 룸 ID로 직접 입장
    /// </summary>
    public async void JoinById(string roomId)
    {
        if (_room != null)
        {
            Debug.Log("[Colyseus] 이미 룸에 접속 중입니다.");
            return;
        }

        try
        {
            string url = GetServerUrl();
            _client = new Client(url);
            _room = await _client.JoinById<NetworkGameState>(roomId);

            Debug.Log($"[Colyseus] 룸 ID로 입장 성공! RoomId={_room.RoomId}");
            RegisterRoomEvents();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Colyseus] JoinById 실패: {e.Message}");
            OnError?.Invoke(e.Message);
            _room = null;
        }
    }

    // ═══════════════════════════════════════
    // 룸 이벤트 등록
    // ═══════════════════════════════════════

    private void RegisterRoomEvents()
    {
        if (_room == null) return;

        // ── 서버에서 "joined" 메시지 수신 ──
        _room.OnMessage<Dictionary<string, object>>("joined", (message) =>
        {
            if (message.TryGetValue("playerNumber", out object pNum))
            {
                LocalPlayerNumber = Convert.ToInt32(pNum);
                Debug.Log($"[Colyseus] 플레이어 번호: {LocalPlayerNumber}");
                OnJoinedRoom?.Invoke(LocalPlayerNumber);
            }
        });

        // ── 게임 시작 ──
        _room.OnMessage<Dictionary<string, object>>("game_start", (message) =>
        {
            Debug.Log("[Colyseus] 게임 시작!");
            OnGameStart?.Invoke();
        });

        // ── 플레이어 퇴장 ──
        _room.OnMessage<Dictionary<string, object>>("player_left", (message) =>
        {
            if (message.TryGetValue("playerNumber", out object pNum))
            {
                int leftPlayer = Convert.ToInt32(pNum);
                Debug.Log($"[Colyseus] Player {leftPlayer} 퇴장");
                OnPlayerLeft?.Invoke(leftPlayer);
            }
        });

        // ── 게임 종료 ──
        _room.OnMessage<Dictionary<string, object>>("game_ended", (message) =>
        {
            if (message.TryGetValue("winner", out object w))
            {
                int winner = Convert.ToInt32(w);
                Debug.Log($"[Colyseus] 게임 종료! 승자: Player {winner}");
                OnGameEnded?.Invoke(winner);
            }
        });

        // ── 기지 파괴 ──
        _room.OnMessage<Dictionary<string, object>>("base_destroyed", (message) =>
        {
            OnMessage?.Invoke("base_destroyed", message);
        });

        // ── 게임 메시지 릴레이 ──
        RegisterRelayMessage("move");
        RegisterRelayMessage("spawn");
        RegisterRelayMessage("attack");
        RegisterRelayMessage("base_hp");
        RegisterRelayMessage("unit_hp");
        RegisterRelayMessage("player_hp");
        RegisterRelayMessage("player_position");
        RegisterRelayMessage("sync");

        // ── 상태 동기화 ──
        _room.OnStateChange += OnRoomStateChange;

        // ── 에러 ──
        _room.OnError += (code, message) =>
        {
            Debug.LogError($"[Colyseus] 룸 에러: code={code}, msg={message}");
            OnError?.Invoke(message);
        };

        // ── 퇴장 ──
        _room.OnLeave += (code) =>
        {
            Debug.Log($"[Colyseus] 룸 퇴장: code={code}");
            _room = null;
        };
    }

    private void RegisterRelayMessage(string messageType)
    {
        _room.OnMessage<Dictionary<string, object>>(messageType, (message) =>
        {
            OnMessage?.Invoke(messageType, message);
        });
    }

    private void OnRoomStateChange(NetworkGameState state, bool isFirstState)
    {
        if (isFirstState)
        {
            Debug.Log($"[Colyseus] 초기 상태 수신: phase={state.phase}, players={state.players.Count}");
        }
        OnStateChanged?.Invoke(state);
    }

    // ═══════════════════════════════════════
    // 메시지 전송
    // ═══════════════════════════════════════

    /// <summary>서버에 메시지 전송</summary>
    public void Send(string type, Dictionary<string, object> payload)
    {
        if (_room == null)
        {
            Debug.LogWarning("[Colyseus] 룸에 접속되어 있지 않습니다.");
            return;
        }

        try
        {
            // Fire-and-forget (Room.Send는 async Task)
            _ = _room.Send(type, payload);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Colyseus] Send 실패: {e.Message}");
        }
    }

    /// <summary>서버에 간단한 메시지 전송 (payload 없음)</summary>
    public void Send(string type)
    {
        if (_room == null)
        {
            Debug.LogWarning("[Colyseus] 룸에 접속되어 있지 않습니다.");
            return;
        }

        try
        {
            _ = _room.Send(type);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Colyseus] Send 실패: {e.Message}");
        }
    }

    // ── 편의 메서드: 이동 동기화 ──
    public void SendMove(Vector3 position, Vector3 direction)
    {
        Send("move", new Dictionary<string, object>
        {
            { "x", position.x },
            { "y", position.y },
            { "z", position.z },
            { "dx", direction.x },
            { "dz", direction.z }
        });
    }

    // ── 편의 메서드: 유닛 소환 동기화 ──
    public void SendSpawn(string unitType, Vector3 position, int playerId)
    {
        Send("spawn", new Dictionary<string, object>
        {
            { "unitType", unitType },
            { "x", (double)position.x },
            { "y", (double)position.y },
            { "z", (double)position.z },
            { "playerId", playerId }
        });
    }

    // ── 편의 메서드: 기지 HP 동기화 ──
    public void SendBaseHp(float hp)
    {
        Send("base_hp", new Dictionary<string, object>
        {
            { "hp", hp }
        });
    }

    // ── 편의 메서드: 기지 파괴 알림 ──
    public void SendBaseDestroyed()
    {
        Send("base_destroyed", new Dictionary<string, object>
        {
            { "playerNumber", LocalPlayerNumber }
        });
    }

    // ═══════════════════════════════════════
    // 접속 해제
    // ═══════════════════════════════════════

    /// <summary>룸에서 나가고 연결 해제</summary>
    public async void Disconnect()
    {
        if (_room != null)
        {
            try
            {
                await _room.Leave();
                Debug.Log("[Colyseus] 룸 퇴장 완료");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Colyseus] Leave 오류: {e.Message}");
            }
            _room = null;
        }
        _client = null;
        LocalPlayerNumber = 0;
    }

    private void OnDestroy()
    {
        Disconnect();
        if (Instance == this) Instance = null;
    }
}
