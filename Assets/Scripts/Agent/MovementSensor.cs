using UnityEngine;
using UnityEngine.AI;

public class MovementSensor : MonoBehaviour
{
    private NavMeshAgent agente;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    public bool HaLlegadoAlDestino()
    {
        return !agente.pathPending && agente.remainingDistance <= 0.3f;
    }

    public bool EstaCercaDelObjetivo(float distancia)
    {
        return !agente.pathPending && agente.remainingDistance <= distancia;
    }

    // NUEVO SENSOR: Le dice al cerebro dónde está su propio cuerpo
    public Vector3 ObtenerPosicionActual()
    {
        return transform.position;
    }
}