using UnityEngine;

public class UnitBase : MonoBehaviour
{
    private UnitType unitType;
    private UnitStats stats;
    private Damageable damageable;
    private HPBar hpBar;
    private VisionRenderer visionRenderer;
    private Transform currentTarget;
    private Material unitMaterial;

    private bool isAlly = true;
    public bool IsAlly => isAlly;

    private int ownerPlayerID = 1;
    public int OwnerPlayerID => ownerPlayerID;

    // 공격 쿨
    private float lastAttackTime;

    // ??? ????
    private float speedMultiplier = 1f;
    private float buffEndTime;

    // ?? ??
    [Header("Separation")]
    [SerializeField] private float separationRadius = 2f;
    [SerializeField] private float separationForce = 2f;

    // ? ?? ?? ???
    [Header("Base Targeting")]
    [SerializeField] private float baseDetectionRange = 15f; // ? ?? ?? ??

    public UnitType Type => unitType;

    private Vector3 MoveDirection
    {
        get
        {
            if (!isAlly) return Vector3.back;
            return ownerPlayerID == 1 ? Vector3.forward : Vector3.back;
        }
    }

    public void Initialize(UnitType type, bool ally = true, int playerID = 1)
    {
        unitType = type;
        isAlly = ally;
        ownerPlayerID = playerID;
        stats = UnitDatabase.GetStats(type);

        // ??? ????
        transform.localScale = stats.scale;

        // ???: ?? ?????? "Enemy" ???? ????
        if (!isAlly)
            gameObject.tag = "Enemy";

        // ????? ???? - ???? ?? ?? ????
        Color baseColor = isAlly ? stats.unitColor : DarkenColor(stats.unitColor);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            unitMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            unitMaterial.color = baseColor;
            mr.material = unitMaterial;
        }

        // ???? ????
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "FrontIndicator";
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = new Vector3(0f, 0.3f, 0.45f);
        indicator.transform.localScale = new Vector3(0.5f, 0.3f, 0.12f);
        Object.Destroy(indicator.GetComponent<Collider>());
        Material indicatorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        indicatorMat.color = isAlly ? Color.white : Color.red;
        indicator.GetComponent<MeshRenderer>().material = indicatorMat;

        // Damageable ???????
        damageable = gameObject.AddComponent<Damageable>();
        damageable.SetMaxHP(stats.maxHP);
        damageable.OnDeath += OnUnitDeath;

        // HP ??
        GameObject hpObj = new GameObject($"HPBar_{type}");
        hpBar = hpObj.AddComponent<HPBar>();
        hpBar.Initialize(transform);

        // ??? ??????
        GameObject visionObj = new GameObject("VisionRange");
        visionObj.transform.SetParent(transform, false);
        float groundY = -(stats.scale.y * 0.5f) + 0.05f;
        visionObj.transform.localPosition = new Vector3(0f, groundY / stats.scale.y, 0f);
        visionObj.AddComponent<MeshFilter>();
        visionObj.AddComponent<MeshRenderer>();
        visionRenderer = visionObj.AddComponent<VisionRenderer>();
        Color vc = isAlly ? stats.visionColor : new Color(1f, 0.2f, 0.2f, 0.15f);
        visionRenderer.Initialize(
            stats.visionRadius / stats.scale.x,
            stats.visionAngle,
            vc
        );

        // ??? ?????? ???? ??????? ???
        transform.rotation = Quaternion.LookRotation(MoveDirection, Vector3.up);

        // GameManager?? ???
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterUnit(this);
    }

    private Color DarkenColor(Color c)
    {
        return new Color(c.r * 0.6f, c.g * 0.4f, c.b * 0.4f);
    }

    private float CurrentMoveSpeed => stats.moveSpeed * speedMultiplier;

    private void Update()
    {
        if (damageable != null && damageable.IsDead) return;
        if (stats == null) return;

        // ??? ?? HP ???? ???? ???
        if (stats.hpDecayPerSecond > 0f)
        {
            damageable.TakeDamage(stats.hpDecayPerSecond * Time.deltaTime);
            if (damageable.IsDead) return;
        }

        // ??? ???? ????
        if (speedMultiplier > 1f && Time.time >= buffEndTime)
            speedMultiplier = 1f;

        // ?? ??: ?? ? ? ? ? ?? ?? ??? ? ??? ??
        currentTarget = FindTargetInVision();

        // ?? ? ?? ??, ? ??? ????? ? ?? ?? ???
        if (currentTarget == null && isAlly)
        {
            currentTarget = FindEnemyBaseInRange();
        }

        if (currentTarget != null)
        {
            ChaseAndAttack();
        }
        else if (CurrentMoveSpeed > 0f)
        {
            MoveTowardTargetBase();
        }
    }

    private Transform FindTargetInVision()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, stats.visionRadius);
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;

            bool isValidTarget = false;

            if (ownerPlayerID == 2)
            {
                if (hit.CompareTag("Player") && !hit.name.Contains("Player2")) isValidTarget = true;
                else if (hit.name.Contains("PlayerBase") && !hit.name.Contains("Player2")) isValidTarget = true;
                else { var u = hit.GetComponent<UnitBase>(); if (u != null && u.OwnerPlayerID == 1) isValidTarget = true; }
            }
            else if (isAlly)
            {
                if (hit.CompareTag("Enemy") || hit.name.Contains("Player2")) isValidTarget = true;
                else if (hit.name == "EnemyBase" || hit.name.Contains("Player2Base")) isValidTarget = true;
                else { var u = hit.GetComponent<UnitBase>(); if (u != null && u.OwnerPlayerID == 2) isValidTarget = true; }
            }
            else
            {
                if (hit.CompareTag("Player")) isValidTarget = true;
                else { var u = hit.GetComponent<UnitBase>(); if (u != null && u.IsAlly) isValidTarget = true; var mb = hit.GetComponent<MainBase>(); if (mb != null && mb.IsPlayerBase) isValidTarget = true; }
            }

            if (!isValidTarget) continue;

            // ????? ???????? ???
            Damageable targetHP = hit.GetComponent<Damageable>();
            if (targetHP != null && targetHP.IsDead) continue;

            Vector3 dir = hit.transform.position - transform.position;
            dir.y = 0f;

            if (stats.visionAngle < 360f)
            {
                float angleToTarget = Vector3.Angle(transform.forward, dir);
                if (angleToTarget > stats.visionAngle * 0.5f) continue;
            }

            float dist = dir.magnitude;
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = hit.transform;
            }
        }

        return closest;
    }

    /// <summary>
    /// ? ??? ?? ?? ?? ??? ?? (?? ??? ??)
    /// </summary>
    private Transform FindEnemyBaseInRange()
    {
        if (GameManager.Instance == null || !isAlly) return null;

        Vector3 enemyBasePos = GameManager.Instance.GetEnemyBasePosition(ownerPlayerID);

        Vector3 toBase = enemyBasePos - transform.position;
        toBase.y = 0f;

        float distToBase = toBase.magnitude;
        bool baseIsAhead = ownerPlayerID == 1 ? enemyBasePos.z > transform.position.z : enemyBasePos.z < transform.position.z;

        if (distToBase <= baseDetectionRange && baseIsAhead)
        {
            string targetName = ownerPlayerID == 1 ? "EnemyBase" : "PlayerBase";
            GameObject go = GameObject.Find(targetName);
            if (go != null)
            {
                Damageable baseHP = go.GetComponent<Damageable>();
                if (baseHP != null && !baseHP.IsDead)
                    return go.transform;
            }
        }

        return null;
    }

    private void ChaseAndAttack()
    {
        if (currentTarget == null) return;

        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;

        if (dir.magnitude > stats.attackRange)
        {
            Vector3 moveDir = dir.normalized;
            
            // ?? ?? ??
            Vector3 separation = CalculateSeparation();
            moveDir = (moveDir + separation).normalized;
            
            transform.position += moveDir * CurrentMoveSpeed * Time.deltaTime;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, rot, 720f * Time.deltaTime);
            }
        }
        else
        {
            // ???? ???? ?? ????
            if (Time.time >= lastAttackTime + stats.attackCooldown)
            {
                Damageable targetHP = currentTarget.GetComponent<Damageable>();
                if (targetHP != null && !targetHP.IsDead)
                {
                    targetHP.TakeDamage(stats.attackDamage);
                }
                lastAttackTime = Time.time;
            }
        }
    }

    private void MoveTowardTargetBase()
    {
        Vector3 moveDir = MoveDirection;
        
        // ?? ?? ??
        Vector3 separation = CalculateSeparation();
        moveDir = (moveDir + separation).normalized;
        
        transform.position += moveDir * CurrentMoveSpeed * Time.deltaTime;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(moveDir, Vector3.up),
            360f * Time.deltaTime);
    }

    /// <summary>
    /// ?? ???? ?? ?? ?? ?? (Separation force)
    /// </summary>
    private Vector3 CalculateSeparation()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, separationRadius);
        Vector3 separation = Vector3.zero;
        int count = 0;

        foreach (Collider col in nearby)
        {
            if (col.transform == transform) continue;

            // ?? ?? ???? ?????? ?? ??
            UnitBase otherUnit = col.GetComponent<UnitBase>();
            bool isSameTeam = false;
            
            if (otherUnit != null)
            {
                isSameTeam = (otherUnit.IsAlly == isAlly);
            }
            else if (col.CompareTag("Player") && isAlly)
            {
                isSameTeam = true;
            }

            if (!isSameTeam) continue;

            Vector3 toOther = transform.position - col.transform.position;
            toOther.y = 0f;
            float dist = toOther.magnitude;

            if (dist > 0.01f && dist < separationRadius)
            {
                // ??? ????? ?? ???
                float strength = separationForce * (1f - dist / separationRadius);
                separation += toOther.normalized * strength;
                count++;
            }
        }

        return count > 0 ? separation / count : Vector3.zero;
    }

    public void ApplySpeedBuff(float multiplier, float duration)
    {
        speedMultiplier = multiplier;
        buffEndTime = Time.time + duration;
    }

    private void OnUnitDeath()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterUnit(this);
        if (hpBar != null) Destroy(hpBar.gameObject);
        Destroy(gameObject, 0.3f);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterUnit(this);
        if (unitMaterial != null) Destroy(unitMaterial);
        if (hpBar != null && hpBar.gameObject != null) Destroy(hpBar.gameObject);
    }
}
