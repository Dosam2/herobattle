using UnityEngine;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [SerializeField] private Transform playerTransform;

    private Dictionary<UnitType, Vector3> spawnOffsets = new Dictionary<UnitType, Vector3>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadSpawnOffsets();
    }

    public void SetPlayer(Transform player)
    {
        playerTransform = player;
    }

    public Vector3 GetPlayerPosition()
    {
        return playerTransform != null ? playerTransform.position : Vector3.zero;
    }

    /// <summary>
    /// 아군 유닛 소환 (생존 모드: 플레이어 근처, 사망 모드: 기지에서). playerId 1=P1, 2=P2.
    /// </summary>
    public void SpawnUnit(UnitType type, int playerId = 1)
    {
        UnitStats stats = UnitDatabase.GetStats(type);
        Vector3 offset = GetSpawnOffset(type);

        Vector3 spawnCenter;
        bool playerDead = GameManager.Instance != null && GameManager.Instance.IsPlayerDead;

        if (playerDead)
        {
            spawnCenter = GameManager.Instance.GetAllyBaseSpawnPoint(playerId);
            offset = new Vector3(Random.Range(-2f, 2f), 0f, playerId == 1 ? Random.Range(1f, 3f) : Random.Range(-3f, -1f));
        }
        else
        {
            Transform t = playerId == 1 ? playerTransform : GameObject.Find("Player2")?.transform;
            if (t == null)
            {
                Debug.LogWarning("[SpawnManager] Player transform not set.");
                return;
            }
            spawnCenter = t.position;
            offset = t.TransformDirection(offset);
        }

        Vector3 spawnPos = spawnCenter + offset;
        spawnPos.y = stats.scale.y * 0.5f;

        CreateAllyUnit(type, spawnPos, playerId, skipNetworkSend: false);
    }

    /// <summary>
    /// 지정된 월드 위치에 아군 유닛 소환(드래그 배치). playerId 1=P1, 2=P2.
    /// fromNetwork: true면 서버로 소환 전송 안 함 (상대방 소환 수신 시 사용, 무한 루프 방지)
    /// </summary>
    public void SpawnUnitAtPosition(UnitType type, Vector3 worldPos, int playerId = 1, bool fromNetwork = false)
    {
        UnitStats stats = UnitDatabase.GetStats(type);
        worldPos.y = stats.scale.y * 0.5f;
        CreateAllyUnit(type, worldPos, playerId, skipNetworkSend: fromNetwork);
    }

    private void CreateAllyUnit(UnitType type, Vector3 pos, int playerId = 1, bool skipNetworkSend = false)
    {
        UnitStats stats = UnitDatabase.GetStats(type);
        Vector3 safePos = FindNonOverlappingPosition(pos, stats.scale.y * 0.5f, 1.2f);

        GameObject unitObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        unitObj.name = playerId == 1 ? $"Unit_{stats.displayName}" : $"P2Unit_{stats.displayName}";
        unitObj.transform.position = safePos;
        if (playerId == 2)
            unitObj.tag = "Enemy";

        UnitBase unit = unitObj.AddComponent<UnitBase>();
        unit.Initialize(type, true, playerId);

        if (!skipNetworkSend && MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsMultiplayerMode
            && ColyseusManager.Instance != null && ColyseusManager.Instance.IsConnected)
        {
            ColyseusManager.Instance.SendSpawn(type.ToString(), safePos, playerId);
        }

        Debug.Log($"[SpawnManager] P{playerId} 아군 {stats.displayName} 소환!" + (skipNetworkSend ? " (원격)" : ""));
    }


    /// <summary>
    /// 다른 유닛과 겹치지 않는 안전한 위치 찾기
    /// </summary>
    private Vector3 FindNonOverlappingPosition(Vector3 desiredPos, float unitRadius, float minSeparation)
    {
        float checkRadius = unitRadius + minSeparation;
        int maxAttempts = 10;
        Vector3 currentPos = desiredPos;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Collider[] overlaps = Physics.OverlapSphere(currentPos, checkRadius);
            bool hasOverlap = false;

            foreach (Collider col in overlaps)
            {
                // 다른 유닛이나 플레이어와 겹치는지 확인
                UnitBase unit = col.GetComponent<UnitBase>();
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                
                if (unit != null || (player != null && col.transform == player.transform))
                {
                    hasOverlap = true;
                    break;
                }
            }

            if (!hasOverlap)
                return currentPos;

            // 겹치면 약간 옆으로 이동
            float angle = (attempt * 60f) * Mathf.Deg2Rad; // 60도씩 회전
            float offset = minSeparation * (attempt + 1);
            currentPos = desiredPos + new Vector3(
                Mathf.Cos(angle) * offset,
                0f,
                Mathf.Sin(angle) * offset
            );
        }

        // 최대 시도 후에도 안전한 위치를 못 찾으면 원래 위치 반환
        return desiredPos;
    }

    /// <summary>
    /// 적 기지에서 적 유닛 소환
    /// </summary>
    public void SpawnEnemyUnit(UnitType type)
    {
        if (GameManager.Instance == null) return;

        UnitStats stats = UnitDatabase.GetStats(type);
        Vector3 basePos = GameManager.Instance.GetEnemyBasePosition();
        Vector3 spawnPos = basePos + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, -1f));
        spawnPos.y = stats.scale.y * 0.5f;

        // 겹침 방지
        Vector3 safePos = FindNonOverlappingPosition(spawnPos, stats.scale.y * 0.5f, 1.2f);

        GameObject unitObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        unitObj.name = $"Enemy_{stats.displayName}";
        unitObj.tag = "Enemy";
        unitObj.transform.position = safePos;

        UnitBase unit = unitObj.AddComponent<UnitBase>();
        unit.Initialize(type, false);

        Debug.Log($"[SpawnManager] 적 {stats.displayName} 소환!");
    }

    public void SpawnRandomEnemy()
    {
        UnitType[] allTypes = UnitDatabase.AllTypes;
        UnitType randomType = allTypes[Random.Range(0, allTypes.Length)];
        SpawnEnemyUnit(randomType);
    }

    public Vector3 GetSpawnOffset(UnitType type)
    {
        if (spawnOffsets.TryGetValue(type, out Vector3 offset))
            return offset;
        return UnitDatabase.GetStats(type).defaultSpawnOffset;
    }

    public void SetSpawnOffset(UnitType type, Vector3 offset)
    {
        spawnOffsets[type] = offset;
        SaveSpawnOffsets();
    }

    private void SaveSpawnOffsets()
    {
        foreach (var kvp in spawnOffsets)
        {
            string key = $"SpawnOffset_{kvp.Key}";
            PlayerPrefs.SetFloat($"{key}_x", kvp.Value.x);
            PlayerPrefs.SetFloat($"{key}_y", kvp.Value.y);
            PlayerPrefs.SetFloat($"{key}_z", kvp.Value.z);
        }
        PlayerPrefs.Save();
    }

    private void LoadSpawnOffsets()
    {
        foreach (UnitType type in UnitDatabase.AllTypes)
        {
            string key = $"SpawnOffset_{type}";
            if (PlayerPrefs.HasKey($"{key}_x"))
            {
                Vector3 offset = new Vector3(
                    PlayerPrefs.GetFloat($"{key}_x"),
                    PlayerPrefs.GetFloat($"{key}_y"),
                    PlayerPrefs.GetFloat($"{key}_z")
                );
                spawnOffsets[type] = offset;
            }
        }
    }
}
