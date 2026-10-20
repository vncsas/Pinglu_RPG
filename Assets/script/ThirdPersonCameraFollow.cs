using UnityEngine;
public class ThirdPersonCameraFollow : MonoBehaviour
{
    [Header("Alvo a seguir")]
    [SerializeField] private Transform target;

    [Header("Posicionamento")]
    [SerializeField] private float distance = 5f;     
    [SerializeField] private float height = 2.5f;     
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Mira")]
    [SerializeField] private float lookAtHeightOffset = 1.5f;

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (target == null) return;

   
        Vector3 desiredPosition = target.position
            - target.forward * distance
            + Vector3.up * height;

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothTime);

        Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeightOffset;
        transform.LookAt(lookAtPoint);
    }
}