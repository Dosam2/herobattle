using UnityEngine;

public class MainBase : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] private bool isPlayerBase = true;
    [SerializeField] private float baseHP = 500f;

    [Header("Defense")]
    [SerializeField] private float defenseRange = 8f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackCooldown = 1.2f;

    [Header("Visual")]
    [SerializeField] private Color baseColor = Color.cyan;
    [SerializeField] private Color rangeColor = new Color(1f, 0.3f, 0.3f, 0.08f);

    private Damageable damageable;
    private HPBar hpBar;
    private VisionRenderer rangeRenderer;
    private Transform currentTarget;
    private float lastAttackTime;
    private Material baseMaterial;

    public bool IsPlayerBase => isPlayerBase;

    private void Start()
    {
        // HP 설정
        damageable = gameObject.AddComponent<Damageable>();
        damageable.SetMaxHP(baseHP);
        damageable.OnDeath += OnBaseDestroyed;

        // HP 바
        GameObject hpObj = new GameObject($"BaseHPBar_{(isPlayerBase ? "Player" : "Enemy")}");
        hpBar = hpObj.AddComponent<HPBar>();
        float baseTopY = transform.localScale.y + 0.2f;
        hpBar.Initialize(transform, new Vector3(0f, baseTopY, 0f), 3f);

        // 기지 비주얼 색상
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
        {
            baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            baseMaterial.color = baseColor;
            mr.material = baseMaterial;
        }

        // 방어 사거리 시각화
        GameObject rangeObj = new GameObject("DefenseRange");
        rangeObj.transform.SetParent(transform, false);
        rangeObj.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        rangeObj.AddComponent<MeshFilter>();
        rangeObj.AddComponent<MeshRenderer>();
        rangeRenderer = rangeObj.AddComponent<VisionRenderer>();
        rangeRenderer.Initialize(defenseRange / transform.localScale.x, 360f, rangeColor);
    }

    private void Update()
    {
        if (damageable == null || damageable.IsDead) return;

        if (currentTarget == null || !IsTargetValid(currentTarget))
            currentTarget = FindClosestEnemy();

        if (currentTarget != null && Time.time >= lastAttackTime + attackCooldown)
        {
            AttackTarget(currentTarget);
            lastAttackTime = Time.time;
        }
    }

    private Transform FindClosestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, defenseRange);
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;

            bool isEnemy = false;

            if (isPlayerBase)
            {
                // 플레이어 기지는 "Enemy" 태그(적 기지 및 적 유닛)를 공격
                isEnemy = hit.CompareTag("Enemy");
            }
            else
            {
                // 적 기지는 "Player" 태그 및 아군 UnitBase를 공격
                if (hit.CompareTag("Player"))
                {
                    isEnemy = true;
                }
                else
                {
                    UnitBase unit = hit.GetComponent<UnitBase>();
                    if (unit != null && unit.IsAlly)
                        isEnemy = true;
                }
            }

            if (!isEnemy) continue;

            Damageable hp = hit.GetComponent<Damageable>();
            if (hp != null && hp.IsDead) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = hit.transform;
            }
        }

        return closest;
    }

    private bool IsTargetValid(Transform target)
    {
        if (target == null) return false;
        if (!target.gameObject.activeInHierarchy) return false;

        Damageable hp = target.GetComponent<Damageable>();
        if (hp != null && hp.IsDead) return false;

        float dist = Vector3.Distance(transform.position, target.position);
        return dist <= defenseRange;
    }

    private void AttackTarget(Transform target)
    {
        Damageable hp = target.GetComponent<Damageable>();
        if (hp != null && !hp.IsDead)
        {
            hp.TakeDamage(attackDamage);
        }
    }

    private void OnBaseDestroyed()
    {
        if (!isPlayerBase)
        {
            // 적 기지 파괴 = 승리
            Debug.Log("승리");
            if (GameManager.Instance != null)
                GameManager.Instance.EnemyBaseDestroyed();
        }
        else
        {
            Debug.Log("[Base] 아군 기지 파괴!");
        }
    }

    private void OnDestroy()
    {
        if (baseMaterial != null) Destroy(baseMaterial);
        if (hpBar != null && hpBar.gameObject != null) Destroy(hpBar.gameObject);
    }
}
