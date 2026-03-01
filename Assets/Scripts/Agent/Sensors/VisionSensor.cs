using UnityEngine;
using UnityEngine.AI;

// Proporciona los "ojos" al agente (Perception Subsystem) para detectar objetivos y medir el entorno.
public class VisionSensor : MonoBehaviour
{
    // Mantiene la referencia privada al objetivo pero visible en el editor.
    [SerializeField] private Transform ladron; 

    public float viewDistance = 12f;
    public float viewAngle = 180f;
    public Transform eyePoint; // Punto de origen de la visión (ojos).
    public LayerMask obstacleMask;
    public LayerMask targetMask;
    
    public float alturaTorso = 0.2f;

    // Detecta si el objetivo está dentro del rango, ángulo y línea de visión.
    public bool PuedeVerLadron()
    {
        if (!ladron) return false;

        Vector3 origin = eyePoint.position;
        Vector3 targetPos = ladron.position;
        targetPos.y += alturaTorso;

        Vector3 toTarget = targetPos - origin;

        // Comprobación de distancia.
        if (toTarget.magnitude > viewDistance)
            return false;

        // Comprobación de ángulo de visión.
        float ang = Vector3.Angle(transform.forward, toTarget);
        if (ang > viewAngle * 0.5f)
            return false;

        // Raycast físico para detectar obstáculos entre el guardia y el objetivo.
        int mask = obstacleMask | targetMask;
        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, viewDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == ladron || hit.transform.IsChildOf(ladron);
        }

        return false;
    }

    // Calcula el espacio despejado en una dirección usando el NavMesh (usado para planificación).
    public float MedirDistanciaLibre(Vector3 direccion, float distanciaMax)
    {
        Vector3 origen = transform.position;
        Vector3 destino = origen + (direccion.normalized * distanciaMax);

        if (NavMesh.Raycast(origen, destino, out NavMeshHit hit, NavMesh.AllAreas))
        {
            return Vector3.Distance(origen, hit.position);
        }

        return distanciaMax;
    }

    // Abstracción: devuelve las coordenadas del objetivo sin exponer el objeto real.
    public Vector3 ObtenerPosicionObjetivo()
    {
        if (ladron != null) return ladron.position;
        return Vector3.zero;
    }
}