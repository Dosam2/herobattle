using UnityEngine;
using System.Collections.Generic;

public enum UnitType
{
    Warrior = 0,
    Archer = 1,
    Rogue = 2,
    Turret = 3
}

[System.Serializable]
public class UnitStats
{
    public UnitType type;
    public string displayName;
    public float maxHP;
    public float moveSpeed;
    public float visionRadius;
    public float visionAngle; // 360 = 원형 시야, 360보다 작으면 부채꼴
    public float attackRange;
    public float attackDamage;
    public float attackCooldown;
    public Vector3 scale;
    public Color unitColor;
    public Color visionColor;
    public Vector3 defaultSpawnOffset;
    public float hpDecayPerSecond; // 0 = 감소 없음, 포탑 등이 사용
}

public static class UnitDatabase
{
    private static Dictionary<UnitType, UnitStats> database;

    public static UnitStats GetStats(UnitType type)
    {
        if (database == null) Init();
        return database[type];
    }

    public static UnitType[] AllTypes => (UnitType[])System.Enum.GetValues(typeof(UnitType));

    private static void Init()
    {
        database = new Dictionary<UnitType, UnitStats>
        {
            {
                UnitType.Warrior, new UnitStats
                {
                    type = UnitType.Warrior,
                    displayName = "전사",
                    maxHP = 80f,
                    moveSpeed = 3f,
                    visionRadius = 5f,
                    visionAngle = 120f,
                    attackRange = 1.5f,
                    attackDamage = 15f,
                    attackCooldown = 1f,
                    scale = new Vector3(0.5f, 0.5f, 0.5f),
                    unitColor = new Color(0.85f, 0.25f, 0.25f),
                    visionColor = new Color(1f, 0.3f, 0.3f, 0.18f),
                    defaultSpawnOffset = new Vector3(-1.2f, 0f, 0.8f)
                }
            },
            {
                UnitType.Archer, new UnitStats
                {
                    type = UnitType.Archer,
                    displayName = "궁수",
                    maxHP = 50f,
                    moveSpeed = 2.5f,
                    visionRadius = 8f,
                    visionAngle = 90f,
                    attackRange = 7f,
                    attackDamage = 10f,
                    attackCooldown = 1.5f,
                    scale = new Vector3(0.4f, 0.4f, 0.4f),
                    unitColor = new Color(0.25f, 0.8f, 0.25f),
                    visionColor = new Color(0.3f, 1f, 0.3f, 0.18f),
                    defaultSpawnOffset = new Vector3(1.2f, 0f, 0.8f)
                }
            },
            {
                UnitType.Rogue, new UnitStats
                {
                    type = UnitType.Rogue,
                    displayName = "도적",
                    maxHP = 40f,
                    moveSpeed = 5f,
                    visionRadius = 6f,
                    visionAngle = 60f,
                    attackRange = 1.2f,
                    attackDamage = 25f,
                    attackCooldown = 0.8f,
                    scale = new Vector3(0.35f, 0.35f, 0.35f),
                    unitColor = new Color(0.55f, 0.2f, 0.85f),
                    visionColor = new Color(0.6f, 0.3f, 1f, 0.18f),
                    defaultSpawnOffset = new Vector3(0f, 0f, 1.5f)
                }
            },
            {
                UnitType.Turret, new UnitStats
                {
                    type = UnitType.Turret,
                    displayName = "포탑",
                    maxHP = 100f,
                    moveSpeed = 0f,
                    visionRadius = 10f,
                    visionAngle = 360f,
                    attackRange = 9f,
                    attackDamage = 8f,
                    attackCooldown = 0.5f,
                    scale = new Vector3(0.45f, 0.45f, 0.45f),
                    unitColor = new Color(0.85f, 0.85f, 0.2f),
                    visionColor = new Color(1f, 1f, 0.3f, 0.14f),
                    defaultSpawnOffset = new Vector3(0f, 0f, -1f),
                    hpDecayPerSecond = 5f
                }
            }
        };
    }
}
