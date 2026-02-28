using UnityEngine;
using UnityEngine.AI;

public class VisionSensor : MonoBehaviour
{
    public Transform ladron;
    
    [Header("Configuración Visual")]
    public float viewDistance = 5f;
    public float viewAngle = 250f;
    public Transform eyePoint;
    public LayerMask obstacleMask;
    public LayerMask targetMask;
    
    [Tooltip("Suma esta altura para apuntar al pecho en lugar de a los pies")]
    

    public bool PuedeVerLadron()
    {
        if (!ladron) return false;

        Vector3 origin = eyePoint.position;
        Vector3 targetPos = ladron.position;
        targetPos.y += 0.2f; // Ahora usa la variable del Inspector

        Vector3 toTarget = targetPos - origin;

        if (toTarget.magnitude > viewDistance)
            return false;

        float ang = Vector3.Angle(transform.forward, toTarget);
        if (ang > viewAngle * 0.5f)
            return false;

        int mask = obstacleMask | targetMask;

        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, viewDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == ladron || hit.transform.IsChildOf(ladron);
        }

        return false;
    }

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
}