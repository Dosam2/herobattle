using UnityEngine;
using System;

public class Damageable : MonoBehaviour
{
    [SerializeField] private float maxHP = 100f;

    public float MaxHP => maxHP;
    public float CurrentHP { get; private set; }
    public float HPRatio => maxHP > 0f ? CurrentHP / maxHP : 0f;
    public bool IsDead => CurrentHP <= 0f;

    public event Action<float> OnHPChanged;
    public event Action OnDeath;

    private void Awake()
    {
        CurrentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        OnHPChanged?.Invoke(HPRatio);
        if (IsDead) OnDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHPChanged?.Invoke(HPRatio);
    }

    public void SetMaxHP(float newMax)
    {
        maxHP = newMax;
        CurrentHP = newMax;
        OnHPChanged?.Invoke(HPRatio);
    }
}
