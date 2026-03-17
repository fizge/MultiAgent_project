using System;
using UnityEngine;
using UnityEngine.AI;

// Proporciona información sobre el estado físico y la posición del agente.
public class MovementSensor : MonoBehaviour
{
    // Evento de estado 
    public event Action OnDestinoAlcanzado;

    private NavMeshAgent agente;
    private bool llegadoAntes = false;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        bool llegado = HaLlegadoAlDestino();
        if (llegado && !llegadoAntes) OnDestinoAlcanzado?.Invoke();
        llegadoAntes = llegado;
    }

    // Verifica si el agente ha completado su ruta de navegación.
    public bool HaLlegadoAlDestino()
    {
        return agente.hasPath && !agente.pathPending && agente.remainingDistance <= 0.3f;
    }

}