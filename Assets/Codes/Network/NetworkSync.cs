using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 멀티플레이어 실시간 동기화.
/// 로컬 플레이어의 위치를 서버에 전송하고, 상대방 위치를 수신하여 적용.
/// </summary>
public class NetworkSync : MonoBehaviour
{
    public static NetworkSync Instance { get; private set; }

    [Header("Sync Settings")]
    [SerializeField] private float sendInterval = 0.05f; // 20Hz
    [SerializeField] private float interpolationSpeed = 12f;

    private float lastSendTime;

    // 원격 플레이어(상대방) 추적
    private Transform remotePlayer;
    private Vector3 remoteTargetPos;
    private Quaternion remoteTargetRot;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (ColyseusManager.Instance != null)
            ColyseusManager.Instance.OnMessage += OnServerMessage;
    }

    private void OnDisable()
    {
        if (ColyseusManager.Instance != null)
            ColyseusManager.Instance.OnMessage -= OnServerMessage;
    }

    private void Update()
    {
        if (MultiplayerManager.Instance == null || !MultiplayerManager.Instance.IsMultiplayerMode)
            return;

        // ── 로컬 플레이어 위치 전송 ──
        if (Time.time - lastSendTime >= sendInterval)
        {
            SendLocalPlayerPosition();
            lastSendTime = Time.time;
        }

        // ── 원격 플레이어 보간 이동 ──
        if (remotePlayer != null)
        {
            remotePlayer.position = Vector3.Lerp(remotePlayer.position, remoteTargetPos, interpolationSpeed * Time.deltaTime);
            remotePlayer.rotation = Quaternion.Slerp(remotePlayer.rotation, remoteTargetRot, interpolationSpeed * Time.deltaTime);
        }
    }

    private void SendLocalPlayerPosition()
    {
        if (ColyseusManager.Instance == null || !ColyseusManager.Instance.IsConnected) return;

        int localId = MultiplayerManager.Instance.LocalPlayerID;
        GameObject localPlayer = localId == 1
            ? GameObject.FindGameObjectWithTag("Player")
            : GameObject.Find("Player2");

        if (localPlayer == null) return;

        Vector3 pos = localPlayer.transform.position;
        Vector3 fwd = localPlayer.transform.forward;

        // 숫자를 double로 보내 플랫폼별 JSON 직렬화 일치
        ColyseusManager.Instance.Send("player_position", new Dictionary<string, object>
        {
            { "x", (double)pos.x }, { "y", (double)pos.y }, { "z", (double)pos.z },
            { "fx", (double)fwd.x }, { "fz", (double)fwd.z }
        });
    }

    private void OnServerMessage(string type, Dictionary<string, object> data)
    {
        switch (type)
        {
            case "player_position":
                ApplyRemotePosition(data);
                break;
            case "spawn":
                ApplyRemoteSpawn(data);
                break;
        }
    }

    private void ApplyRemotePosition(Dictionary<string, object> data)
    {
        // 내가 보낸 메시지는 서버가 except로 제외하지만, 방어적으로 상대방 것만 적용
        if (data.TryGetValue("from", out var fromObj))
        {
            int from = (int)System.Convert.ToInt64(fromObj);
            if (from == MultiplayerManager.Instance.LocalPlayerID) return;
        }

        // 원격 플레이어 찾기 (상대방)
        if (remotePlayer == null)
        {
            int localId = MultiplayerManager.Instance.LocalPlayerID;
            GameObject go = localId == 1
                ? GameObject.Find("Player2")
                : GameObject.FindGameObjectWithTag("Player");
            if (go != null) remotePlayer = go.transform;
        }

        if (remotePlayer == null) return;

        float x = data.TryGetValue("x", out var vx) ? System.Convert.ToSingle(vx) : remotePlayer.position.x;
        float y = data.TryGetValue("y", out var vy) ? System.Convert.ToSingle(vy) : remotePlayer.position.y;
        float z = data.TryGetValue("z", out var vz) ? System.Convert.ToSingle(vz) : remotePlayer.position.z;
        remoteTargetPos = new Vector3(x, y, z);

        float fx = data.TryGetValue("fx", out var vfx) ? System.Convert.ToSingle(vfx) : 0;
        float fz = data.TryGetValue("fz", out var vfz) ? System.Convert.ToSingle(vfz) : 1;
        if (fx != 0 || fz != 0)
            remoteTargetRot = Quaternion.LookRotation(new Vector3(fx, 0, fz), Vector3.up);
    }

    private void ApplyRemoteSpawn(Dictionary<string, object> data)
    {
        // 상대방이 유닛 소환한 것을 로컬에서 재현
        if (!data.TryGetValue("unitType", out var ut)) return;
        if (!data.TryGetValue("x", out var sx)) return;
        if (!data.TryGetValue("z", out var sz)) return;

        string unitTypeName = ut.ToString();
        float spawnX = System.Convert.ToSingle(sx);
        float spawnZ = System.Convert.ToSingle(sz);

        if (System.Enum.TryParse<UnitType>(unitTypeName, out UnitType unitType))
        {
            // 서버가 붙인 from = 소환한 플레이어 번호
            int fromPlayer = data.TryGetValue("from", out var fromObj) ? (int)System.Convert.ToInt64(fromObj)
                : (data.TryGetValue("playerId", out var pid) ? System.Convert.ToInt32(pid) : 2);
            Vector3 pos = new Vector3(spawnX, 0, spawnZ);
            if (SpawnManager.Instance != null)
                SpawnManager.Instance.SpawnUnitAtPosition(unitType, pos, fromPlayer, fromNetwork: true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
