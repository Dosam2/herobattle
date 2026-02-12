using UnityEngine;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.1f, 0.7f);
    [SerializeField] private float barWidth = 0.8f;
    [SerializeField] private float barHeight = 0.12f;

    private Transform target;
    private Damageable damageable;
    private Transform fillTransform;
    private Material fillMaterial;
    private Material bgMaterial;

    private static readonly Color FullColor = Color.green;
    private static readonly Color EmptyColor = Color.red;
    private static readonly Color BgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);

    public void Initialize(Transform owner, Vector3? customOffset = null, float? customWidth = null)
    {
        target = owner;
        damageable = owner.GetComponent<Damageable>();
        if (damageable == null) return;

        if (customOffset.HasValue) offset = customOffset.Value;
        if (customWidth.HasValue) barWidth = customWidth.Value;

        CreateBar();
        damageable.OnHPChanged += UpdateBar;
        damageable.OnDeath += () => gameObject.SetActive(false);
    }

    private void CreateBar()
    {
        Material CreateFlatMat(Color col)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.color = col;
            return m;
        }

        // 배경
        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "HP_BG";
        bg.transform.SetParent(transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bg.transform.localScale = new Vector3(barWidth + 0.05f, barHeight + 0.03f, 1f);
        Object.Destroy(bg.GetComponent<Collider>());
        bgMaterial = CreateFlatMat(BgColor);
        bg.GetComponent<MeshRenderer>().material = bgMaterial;

        // 채움(바)
        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "HP_Fill";
        fill.transform.SetParent(transform, false);
        fill.transform.localPosition = new Vector3(0f, 0.005f, 0f);
        fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        fill.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        Object.Destroy(fill.GetComponent<Collider>());
        fillMaterial = CreateFlatMat(FullColor);
        fill.GetComponent<MeshRenderer>().material = fillMaterial;
        fillTransform = fill.transform;
    }

    private void UpdateBar(float ratio)
    {
        if (fillTransform == null) return;
        ratio = Mathf.Clamp01(ratio);
        fillTransform.localScale = new Vector3(barWidth * ratio, barHeight, 1f);
        fillTransform.localPosition = new Vector3(
            -barWidth * (1f - ratio) * 0.5f,
            0.005f,
            0f
        );
        fillMaterial.color = Color.Lerp(EmptyColor, FullColor, ratio);
    }

    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + offset;
    }

    private void OnDestroy()
    {
        if (fillMaterial != null) Destroy(fillMaterial);
        if (bgMaterial != null) Destroy(bgMaterial);
    }
}
