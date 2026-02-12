using UnityEngine;

public class PlayerAutoAttack : MonoBehaviour
{
    [Header("Auto Attack Settings")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackDamage = 12f;
    [SerializeField] private float attackCooldown = 0.8f;

    [Header("Range Visual")]
    [SerializeField] private Color rangeColor = new Color(0.3f, 0.6f, 1f, 0.12f);

    private Transform currentTarget;
    private float lastAttackTime;
    private VisionRenderer rangeRenderer;

    private void Start()
    {
        CreateRangeVisual();
    }

    private void Update()
    {
        // Find or validate target
        if (currentTarget == null || !IsTargetValid(currentTarget))
            currentTarget = FindClosestEnemy();

        if (currentTarget != null && Time.time >= lastAttackTime + attackCooldown)
        {
            Attack(currentTarget);
            lastAttackTime = Time.time;
        }
    }

    private void CreateRangeVisual()
    {
        GameObject rangeObj = new GameObject("AttackRange");
        rangeObj.transform.SetParent(transform, false);
        rangeObj.transform.localPosition = new Vector3(0f, -0.4f, 0f);
        rangeObj.AddComponent<MeshFilter>();
        rangeObj.AddComponent<MeshRenderer>();
        rangeRenderer = rangeObj.AddComponent<VisionRenderer>();
        rangeRenderer.Initialize(attackRange, 360f, rangeColor);
    }

    private Transform FindClosestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;
            if (!hit.CompareTag("Enemy")) continue;

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
        return dist <= attackRange;
    }

    private void Attack(Transform target)
    {
        Damageable hp = target.GetComponent<Damageable>();
        if (hp != null && !hp.IsDead)
        {
            hp.TakeDamage(attackDamage);
        }
    }
}
