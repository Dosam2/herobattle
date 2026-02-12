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
    }

    public Vector3 GetAllyBaseSpawnPoint() => allyBaseSpawnPoint;
    public Vector3 GetEnemyBasePosition() => enemyBasePosition;

    // 배치 영역 경계
    public float PlacementMinZ => placementZoneMinZ;
    public float PlacementMaxZ => placementZoneMaxZ;
    public float PlacementMinX => placementZoneMinX;
    public float PlacementMaxX => placementZoneMaxX;

    public bool IsPositionInPlacementZone(Vector3 pos)
    {
        return pos.x >= placementZoneMinX && pos.x <= placementZoneMaxX
            && pos.z >= placementZoneMinZ && pos.z <= placementZoneMaxZ;
    }

    public Vector3 ClampToPlacementZone(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, placementZoneMinX, placementZoneMaxX);
        pos.z = Mathf.Clamp(pos.z, placementZoneMinZ, placementZoneMaxZ);
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

    public void BuffAllUnitsSpeed(float multiplier, float duration)
    {
        activeUnits.RemoveAll(u => u == null);
        int buffCount = 0;
        foreach (UnitBase unit in activeUnits)
        {
            // 아군 유닛만 버프 (태그로도 추가 확인)
            if (unit.IsAlly && !unit.CompareTag("Enemy"))
            {
                unit.ApplySpeedBuff(multiplier, duration);
                buffCount++;
            }
        }
        Debug.Log($"[Skill] 아군 유닛 {buffCount}개에 이동속도 {multiplier}x 버프! ({duration}초)");
    }
}
