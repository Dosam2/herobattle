using UnityEngine;
using System;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsPlayerDead { get; private set; }
    public bool IsPlacingUnit { get; set; }
    public bool IsVictory { get; private set; }

    public event Action OnPlayerDied;
    public event Action OnVictory;

    [Header("Map Settings")]
    [SerializeField] private Vector3 allyBaseSpawnPoint = new Vector3(0f, 0.5f, -30f);
    [SerializeField] private Vector3 enemyBasePosition = new Vector3(0f, 1f, 35f);
    [SerializeField] private float placementZoneMinZ = -35f;
    [SerializeField] private float placementZoneMaxZ = 0f;
    [SerializeField] private float placementZoneMinX = -15f;
    [SerializeField] private float placementZoneMaxX = 15f;

    // 활성 아군 유닛 레지스트리
    private readonly List<UnitBase> activeUnits = new List<UnitBase>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (MultiplayerManager.Instance == null)
            gameObject.AddComponent<MultiplayerManager>();
        if (ColyseusManager.Instance == null)
            gameObject.AddComponent<ColyseusManager>();
        if (NetworkSync.Instance == null)
            gameObject.AddComponent<NetworkSync>();
        if (GetComponent<GameTimer>() == null)
            gameObject.AddComponent<GameTimer>();

        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
    }

    public void PlayerDied()
    {
        if (IsPlayerDead) return;
        IsPlayerDead = true;
        Debug.Log("[GameManager] Player has died!");
        OnPlayerDied?.Invoke();
    }

    public void EnemyBaseDestroyed()
    {
        if (IsVictory) return;
        IsVictory = true;
        Debug.Log("승리");
        OnVictory?.Invoke();
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsMultiplayerMode)
            MultiplayerManager.Instance.EndMultiplayer();
    }

    public Vector3 GetAllyBaseSpawnPoint() => GetAllyBaseSpawnPoint(1);
    public Vector3 GetEnemyBasePosition() => GetEnemyBasePosition(1);

    public Vector3 GetAllyBaseSpawnPoint(int playerId)
    {
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsMultiplayerMode)
            return MultiplayerManager.Instance.GetPlayerBasePosition(playerId) + new Vector3(0f, -0.5f, playerId == 1 ? 5f : -5f);
        return allyBaseSpawnPoint;
    }

    public Vector3 GetEnemyBasePosition(int playerId)
    {
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsMultiplayerMode)
            return MultiplayerManager.Instance.GetEnemyBasePosition(playerId);
        return enemyBasePosition;
    }

    public float PlacementMinZ => GetPlacementMinZ(1);
    public float PlacementMaxZ => GetPlacementMaxZ(1);
    public float PlacementMinX => GetPlacementMinX(1);
    public float PlacementMaxX => GetPlacementMaxX(1);

    public float GetPlacementMinZ(int playerId)
    {
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsMultiplayerMode)
            return playerId == 1 ? -35f : 0f;
        return placementZoneMinZ;
    }
    public float GetPlacementMaxZ(int playerId)
    {
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsMultiplayerMode)
            return playerId == 1 ? 0f : 35f;
        return placementZoneMaxZ;
    }
    public float GetPlacementMinX(int playerId) => placementZoneMinX;
    public float GetPlacementMaxX(int playerId) => placementZoneMaxX;

    public bool IsPositionInPlacementZone(Vector3 pos) => IsPositionInPlacementZone(pos, 1);
    public bool IsPositionInPlacementZone(Vector3 pos, int playerId)
    {
        return pos.x >= GetPlacementMinX(playerId) && pos.x <= GetPlacementMaxX(playerId)
            && pos.z >= GetPlacementMinZ(playerId) && pos.z <= GetPlacementMaxZ(playerId);
    }

    public Vector3 ClampToPlacementZone(Vector3 pos) => ClampToPlacementZone(pos, 1);
    public Vector3 ClampToPlacementZone(Vector3 pos, int playerId)
    {
        pos.x = Mathf.Clamp(pos.x, GetPlacementMinX(playerId), GetPlacementMaxX(playerId));
        pos.z = Mathf.Clamp(pos.z, GetPlacementMinZ(playerId), GetPlacementMaxZ(playerId));
        return pos;
    }

    // --- 유닛 레지스트리 ---
    public void RegisterUnit(UnitBase unit)
    {
        if (!activeUnits.Contains(unit))
            activeUnits.Add(unit);
    }

    public void UnregisterUnit(UnitBase unit)
    {
        activeUnits.Remove(unit);
    }

    public void BuffAllUnitsSpeed(float multiplier, float duration) => BuffAllUnitsSpeed(multiplier, duration, 1);

    public void BuffAllUnitsSpeed(float multiplier, float duration, int playerId)
    {
        activeUnits.RemoveAll(u => u == null);
        int buffCount = 0;
        foreach (UnitBase unit in activeUnits)
        {
            if (unit.IsAlly && unit.OwnerPlayerID == playerId)
            {
                unit.ApplySpeedBuff(multiplier, duration);
                buffCount++;
            }
        }
        Debug.Log($"[Skill] P{playerId} 아군 유닛 {buffCount}개에 이동속도 {multiplier}x 버프! ({duration}초)");
    }
}
