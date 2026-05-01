using System;
using UnityEngine;

// Sensor de visión exclusivo para cámaras de vigilancia.
// Versión simplificada de VisionSensor: solo detecta al ladrón y dispara eventos.
// No incluye lógica de cofre, rejas, NavMesh ni escaneo de entorno.
public class CameraSensor : MonoBehaviour
{
    [SerializeField] private Transform ladron;

    public float viewDistance = 15f;
    public float viewAngle = 120f;
    public Transform eyePoint;
    public LayerMask obstacleMask;
    public LayerMask targetMask;
    public float alturaTorso = 0.2f;

    public event Action OnLadronAvistado;
    public event Action OnLadronPerdido;

    public Vector3 PosicionLadron { get; private set; }

    private bool ladronVisibleAntes = false;

    void Update()
    {
        bool visible = PuedeVerLadron(); // Verificar si el ladrón es visible
        if (visible) PosicionLadron = ladron.position;

        if (visible && !ladronVisibleAntes) OnLadronAvistado?.Invoke(); // Si el ladrón se vuelve visible, disparar evento
        if (!visible && ladronVisibleAntes) OnLadronPerdido?.Invoke(); // Si el ladrón no es visible, disparar evento
        ladronVisibleAntes = visible;
    }

    bool PuedeVerLadron()
    {
        if (!ladron) return false;

        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position;
        Vector3 targetPos = ladron.position;
        targetPos.y += alturaTorso;
        Vector3 toTarget = targetPos - origin;

        // Verificar distancia
        if (toTarget.magnitude > viewDistance) return false;

        // Verificar ángulo de visión
        if (Vector3.Angle(transform.forward, toTarget) > viewAngle * 0.5f) return false;

        // Verificar línea de visión
        int mask = obstacleMask | targetMask;
        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, viewDistance, mask, QueryTriggerInteraction.Ignore))
            return hit.transform == ladron || hit.transform.IsChildOf(ladron);

        return false;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origen = eyePoint != null ? eyePoint.position : transform.position;
        float semiAngulo = viewAngle * 0.5f;
        int segmentos = 24;

        Gizmos.color = Color.yellow;

        // Rayo central
        Gizmos.DrawRay(origen, transform.forward * viewDistance);

        // Círculo en el extremo del cono (plano perpendicular a forward)
        float radioCirculo = Mathf.Tan(semiAngulo * Mathf.Deg2Rad) * viewDistance;
        Vector3 centroCirculo = origen + transform.forward * viewDistance;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        Vector3 puntoAnterior = centroCirculo + right * radioCirculo;
        for (int i = 1; i <= segmentos; i++)
        {
            float angulo = i * (360f / segmentos);
            Vector3 puntoActual = centroCirculo + (right * Mathf.Cos(angulo * Mathf.Deg2Rad) + up * Mathf.Sin(angulo * Mathf.Deg2Rad)) * radioCirculo;
            Gizmos.DrawLine(puntoAnterior, puntoActual);
            puntoAnterior = puntoActual;
        }

        // Líneas desde el origen hasta el borde del círculo (arriba, abajo, izquierda, derecha)
        Gizmos.DrawLine(origen, centroCirculo + right * radioCirculo);
        Gizmos.DrawLine(origen, centroCirculo - right * radioCirculo);
        Gizmos.DrawLine(origen, centroCirculo + up * radioCirculo);
        Gizmos.DrawLine(origen, centroCirculo - up * radioCirculo);
    }
}
