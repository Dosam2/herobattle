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
    /// 아군 유닛 소환 (생존 모드: 플레이어 근처, 사망 모드: 기지에서)
    /// </summary>
    public void SpawnUnit(UnitType type)
    {
        UnitStats stats = UnitDatabase.GetStats(type);
        Vector3 offset = GetSpawnOffset(type);

        Vector3 spawnCenter;
        bool playerDead = GameManager.Instance != null && GameManager.Instance.IsPlayerDead;

        if (playerDead)
        {
            spawnCenter = GameManager.Instance.GetAllyBaseSpawnPoint();
            offset = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(1f, 3f));
        }
        else
        {
            if (playerTransform == null)
            {
                Debug.LogWarning("[SpawnManager] Player transform not set.");
                return;
            }
            spawnCenter = playerTransform.position;
            offset = playerTransform.TransformDirection(offset);
        }

        Vector3 spawnPos = spawnCenter + offset;
        spawnPos.y = stats.scale.y * 0.5f;

        CreateAllyUnit(type, spawnPos);
    }

    /// <summary>
    /// 지정된 월드 위치에 아군 유닛 소환(드래그 배치)
    /// </summary>
    public void SpawnUnitAtPosition(UnitType type, Vector3 worldPos)
    {
        UnitStats stats = UnitDatabase.GetStats(type);
        worldPos.y = stats.scale.y * 0.5f;
        CreateAllyUnit(type, worldPos);
    }

    private void CreateAllyUnit(UnitType type, Vector3 pos)
    {
        UnitStats stats = UnitDatabase.GetStats(type);
        GameObject unitObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        unitObj.name = $"Unit_{stats.displayName}";
        unitObj.transform.position = pos;

        UnitBase unit = unitObj.AddComponent<UnitBase>();
        unit.Initialize(type, true);

        Debug.Log($"[SpawnManager] 아군 {stats.displayName} 소환!");
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

        GameObject unitObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        unitObj.name = $"Enemy_{stats.displayName}";
        unitObj.tag = "Enemy";
        unitObj.transform.position = spawnPos;

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
