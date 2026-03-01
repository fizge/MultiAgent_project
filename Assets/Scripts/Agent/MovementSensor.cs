using UnityEngine;
using UnityEngine.AI;

// Proporciona información sobre el estado físico y la posición del agente 
public class MovementSensor : MonoBehaviour
{
    private NavMeshAgent agente;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    // Verifica si el agente ha completado su ruta de navegación (usado en patrulla).
    public bool HaLlegadoAlDestino()
    {
        return !agente.pathPending && agente.remainingDistance <= 0.3f;
    }

    // Comprueba la distancia física real contra un objetivo para validar el rango de ataque.
    public bool EstaARangoFisico(Vector3 posicionObjetivo, float rango)
    {
        // Optimizado usando magnitud al cuadrado para evitar el coste del cálculo de raíz cuadrada.
        float distanciaSqr = (transform.position - posicionObjetivo).sqrMagnitude;
        return distanciaSqr <= (rango * rango);
    }

    // Devuelve las coordenadas actuales del cuerpo en el mundo.
    public Vector3 ObtenerPosicionActual()
    {
        return transform.position;
    }
}