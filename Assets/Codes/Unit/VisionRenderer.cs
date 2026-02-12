using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VisionRenderer : MonoBehaviour
{
    // VisionRenderer
    // - 유닛의 시야(원형 또는 부채꼴)를 Mesh로 생성하여 투명 재질로 시각화합니다.
    // - Initialize(반경, 각도, 색)로 초기화하고, SetEnabled로 표시를 켜고 끌 수 있습니다.
    // - 내부에서 메쉬와 재질을 생성/해제하여 런타임에 시야를 렌더링합니다.

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh visionMesh;
    private Material visionMaterial;
    private float radius;
    private float angle;
    private const int Segments = 40;

    // 초기화: 시야 반경, 시야 각도(360이면 원형), 색을 받아 메쉬와 재질을 생성합니다.
    public void Initialize(float visionRadius, float visionAngle, Color color)
    {
        radius = visionRadius;
        angle = visionAngle;

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        visionMaterial = CreateTransparentMat(color);
        meshRenderer.material = visionMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        GenerateMesh();
    }

    // 투명 재질 생성: URP Unlit 셰이더를 사용해 투명 재질 속성을 설정합니다.
    private Material CreateTransparentMat(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.color = color;
        return mat;
    }

    // 메쉬 생성: 지정된 각도와 반경을 바탕으로 삼각형 팬으로 구성된 시야 메쉬를 생성합니다.
    private void GenerateMesh()
    {
        if (visionMesh != null) Destroy(visionMesh);

        visionMesh = new Mesh { name = "VisionMesh" };

        bool isCircle = angle >= 360f;
        float halfAngle = angle * 0.5f;
        int vertCount = Segments + 2;

        Vector3[] vertices = new Vector3[vertCount];
        int[] triangles = new int[Segments * 3];

        vertices[0] = Vector3.zero;

        float angleStep = angle / Segments;
        float startAngle = isCircle ? 0f : -halfAngle;

        for (int i = 0; i <= Segments; i++)
        {
            float current = startAngle + angleStep * i;
            float rad = current * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * radius;
        }

        for (int i = 0; i < Segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        visionMesh.vertices = vertices;
        visionMesh.triangles = triangles;
        visionMesh.RecalculateNormals();
        meshFilter.mesh = visionMesh;
    }

    // 렌더러 활성화/비활성화
    public void SetEnabled(bool on)
    {
        meshRenderer.enabled = on;
    }

    // 객체 파괴 시 생성한 리소스 정리
    private void OnDestroy()
    {
        if (visionMaterial != null) Destroy(visionMaterial);
        if (visionMesh != null) Destroy(visionMesh);
    }
}
