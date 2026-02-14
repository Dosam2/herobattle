using UnityEngine;
using System;

#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
using Photon.Realtime;
#endif

/// <summary>
/// 실시간 온라인 1:1 매칭. Photon PUN 2 사용.
/// 에셋 스토어: "PUN 2" 검색 후 FREE 다운로드.
/// </summary>
#if PHOTON_UNITY_NETWORKING
public class OnlineRoomManager : MonoBehaviourPunCallbacks
#else
public class OnlineRoomManager : MonoBehaviour
#endif
{
    public static OnlineRoomManager Instance { get; private set; }

    public bool IsOnlineMode { get; private set; }
    public int LocalPlayerNumber { get; private set; } = 1;

#pragma warning disable CS0067
    public event Action OnConnected;
    public event Action OnJoinedRoomAsP1;
    public event Action OnJoinedRoomAsP2;
    public event Action OnOtherPlayerJoined;
#pragma warning restore CS0067

#if PHOTON_UNITY_NETWORKING
    private bool connecting;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        PhotonNetwork.AutomaticallySyncScene = false;
    }

    private void Start()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            LocalPlayerNumber = PhotonNetwork.IsMasterClient ? 1 : 2;
            IsOnlineMode = true;
            return;
        }
        if (!PhotonNetwork.IsConnected) Connect();
    }

    public void Connect()
    {
        if (connecting || PhotonNetwork.IsConnected) return;
        connecting = true;
        PhotonNetwork.ConnectUsingSettings();
    }

    public void JoinOrCreateRoom()
    {
        if (!PhotonNetwork.IsConnected) return;
        var opts = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.JoinOrCreateRoom("1v1", opts, TypedLobby.Default);
    }

    public override void OnConnectedToMaster()
    {
        connecting = false;
        IsOnlineMode = true;
        OnConnected?.Invoke();
        JoinOrCreateRoom();
    }

    public override void OnJoinedRoom()
    {
        LocalPlayerNumber = PhotonNetwork.IsMasterClient ? 1 : 2;
        if (PhotonNetwork.IsMasterClient) OnJoinedRoomAsP1?.Invoke();
        else OnJoinedRoomAsP2?.Invoke();
        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2) OnOtherPlayerJoined?.Invoke();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2) OnOtherPlayerJoined?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsInRoom => PhotonNetwork.InRoom;
    public int RoomPlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;
#else
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Connect() { }
    public void JoinOrCreateRoom() { UnityEngine.Debug.LogWarning("[Online] Photon PUN 2 미설치. 에셋 스토어에서 PUN 2 설치 후 PHOTON_UNITY_NETWORKING 정의."); }
    public bool IsInRoom => false;
    public int RoomPlayerCount => 0;
#endif
}
