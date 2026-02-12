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

    // 팀
    private bool isAlly = true;
    public bool IsAlly => isAlly;

    // 공격
    private float lastAttackTime;

    // 속도 버프
    private float speedMultiplier = 1f;
    private float buffEndTime;

    public UnitType Type => unitType;

    private Vector3 MoveDirection => isAlly ? Vector3.forward : Vector3.back;

    public void Initialize(UnitType type, bool ally = true)
    {
        unitType = type;
        isAlly = ally;
        stats = UnitDatabase.GetStats(type);

        // 크기 설정
        transform.localScale = stats.scale;

        // 태그: 적 유닛은 "Enemy" 태그를 받음
        if (!isAlly)
            gameObject.tag = "Enemy";

        // 비주얼 색상 - 적은 약간 더 어둡게
        Color baseColor = isAlly ? stats.unitColor : DarkenColor(stats.unitColor);

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            unitMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            unitMaterial.color = baseColor;
            mr.material = unitMaterial;
        }

        // 전방 표시기
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "FrontIndicator";
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = new Vector3(0f, 0.3f, 0.45f);
        indicator.transform.localScale = new Vector3(0.5f, 0.3f, 0.12f);
        Object.Destroy(indicator.GetComponent<Collider>());
        Material indicatorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        indicatorMat.color = isAlly ? Color.white : Color.red;
        indicator.GetComponent<MeshRenderer>().material = indicatorMat;

        // Damageable 컴포넌트
        damageable = gameObject.AddComponent<Damageable>();
        damageable.SetMaxHP(stats.maxHP);
        damageable.OnDeath += OnUnitDeath;

        // HP 바
        GameObject hpObj = new GameObject($"HPBar_{type}");
        hpBar = hpObj.AddComponent<HPBar>();
        hpBar.Initialize(transform);

        // 시야 렌더링
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

        // 목표 기지를 향해 향하도록 회전
        transform.rotation = Quaternion.LookRotation(MoveDirection, Vector3.up);

        // GameManager에 등록
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

        // 포탑 등 HP 감소 지속 처리
        if (stats.hpDecayPerSecond > 0f)
        {
            damageable.TakeDamage(stats.hpDecayPerSecond * Time.deltaTime);
            if (damageable.IsDead) return;
        }

        // 속도 버프 만료
        if (speedMultiplier > 1f && Time.time >= buffEndTime)
            speedMultiplier = 1f;

        // 타겟 찾기
        currentTarget = FindTargetInVision();

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

            if (isAlly)
            {
                // 아군은 "Enemy" 태그를 가진 객체를 타겟으로 함
                isValidTarget = hit.CompareTag("Enemy");
            }
            else
            {
                // 적은 "Player" 태그를 가진 객체를 타겟으로 함
                if (hit.CompareTag("Player"))
                {
                    isValidTarget = true;
                }
                else
                {
                    // 또한 아군 UnitBase(동맹)를 타겟
                    UnitBase otherUnit = hit.GetComponent<UnitBase>();
                    if (otherUnit != null && otherUnit.IsAlly)
                        isValidTarget = true;
                    // 플레이어 기지(MainBase)도 타겟
                    MainBase mb = hit.GetComponent<MainBase>();
                    if (mb != null && mb.IsPlayerBase)
                        isValidTarget = true;
                }
            }

            if (!isValidTarget) continue;

            // 타겟이 살아있는지 확인
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

    private void ChaseAndAttack()
    {
        if (currentTarget == null) return;

        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;

        if (dir.magnitude > stats.attackRange)
        {
            Vector3 moveDir = dir.normalized;
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
            // 사거리 내일 때 공격
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
        transform.position += MoveDirection * CurrentMoveSpeed * Time.deltaTime;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(MoveDirection, Vector3.up),
            360f * Time.deltaTime);
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
