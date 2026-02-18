using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;

    public Vector3 offset = new Vector3(0f, 2f, -4f);
    public float smooth = 6f;

    void LateUpdate()
    {
        if (target == null) return;

        // posición deseada detrás del personaje
        Vector3 desiredPosition =
            target.position +
            target.forward * offset.z +
            Vector3.up * offset.y;

        // movimiento suave
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smooth * Time.deltaTime);

        // mirar siempre al personaje
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
