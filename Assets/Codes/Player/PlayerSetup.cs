using UnityEngine;

public class PlayerSetup : MonoBehaviour
{
    [SerializeField] private float playerHP = 150f;

    private void Start()
    {
        // Damageable 존재 보장
        Damageable damageable = GetComponent<Damageable>();
        if (damageable == null)
        {
            damageable = gameObject.AddComponent<Damageable>();
            damageable.SetMaxHP(playerHP);
        }

        // 플레이어용 HP 바 생성
        GameObject hpBarObj = new GameObject("PlayerHPBar");
        HPBar hpBar = hpBarObj.AddComponent<HPBar>();
        hpBar.Initialize(transform);
    }
}
