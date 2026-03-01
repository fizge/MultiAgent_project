using UnityEngine;
using UnityEngine.AI;

public class MovementSensor : MonoBehaviour
{
    private NavMeshAgent agente;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    // Para la patrulla (usa la ruta del NavMesh)
    public bool HaLlegadoAlDestino()
    {
        return !agente.pathPending && agente.remainingDistance <= 0.3f;
    }

    // NUEVA FUNCIÓN: Comprueba la distancia real en el mundo 3D
    // Evita que ataque desde lejos si el NavMesh aún no ha calculado la ruta
    public bool EstaARangoFisico(Vector3 posicionObjetivo, float rango)
    {
        float distanciaSqr = (transform.position - posicionObjetivo).sqrMagnitude;
        return distanciaSqr <= (rango * rango);
    }

    public Vector3 ObtenerPosicionActual()
    {
        return transform.position;
    }
}