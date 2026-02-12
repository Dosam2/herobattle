using UnityEngine;

public class TopViewCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private float height = 15f;
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Camera Bounds (Optional)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 maxBounds = new Vector2(50f, 50f);

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = new Vector3(target.position.x, height, target.position.z);

        if (useBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.z = Mathf.Clamp(targetPosition.z, minBounds.y, maxBounds.y);
        }

        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // 항상 아래보게 설정값
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
