// Behaviour activado por la Deliberativa cuando el patrullero gana el contrato investigarAvistamiento.
// Mueve al agente a una posición arbitraria (Vector3) y notifica al llegar.
// Suscribe a MovementSensor igual que CheckChestBehaviour; filtra llegadas espurias por distancia.
using System;
using UnityEngine;

public class GoToPositionBehaviour : IBehaviour
{
    public float speed = 2f;
    public float acceleration = 2f;
    public float tolerancia = 1.5f; // Distancia mínima para considerar que se ha llegado al destino

    private Transform agentTransform; // Referencia al transform del agente
    private Vector3 destino; // Posición objetivo a la que el agente debe moverse
    private bool activo; // Indica si el comportamiento está activo o no

    public event Action OnLlegadaCompletada;

    public void Initialize(Transform agentTransform, MovementSensor movSensor)
    {
        this.agentTransform = agentTransform;
        movSensor.OnDestinoAlcanzado += AlLlegar; // Suscribirse al evento del sensor de movimiento para detectar cuando se alcanza el destino
    }

    public void Activar(Vector3 posicion)
    {
        destino = posicion;
        activo = true;
    }

    // Método para desactivar el comportamiento, por ejemplo, si se cancela la acción o se asigna una nueva tarea
    public void Desactivar() { activo = false; }

    // Propiedad para verificar si el comportamiento está activo
    public bool EstaActivo => activo;

    public ActionProposal Evaluate()
    {
        // Si el comportamiento no está activo, no se propone ninguna acción
        if (!activo) return null;

        return new ActionProposal
        {
            solicitaControl = true,
            destinoMovimiento = destino,
            velocidad = speed,
            aceleracion = acceleration,
            distanciaParada = 0.5f,
            corriendo = false
        };
    }

    // Método llamado por el MovementSensor cuando se detecta que el agente ha alcanzado su destino
    // Se dispara en cada OnDestinoAlcanzado, no solo para este behaviour, por eso las 2 condiciones
    private void AlLlegar() 
    {
        // El comportamiento debe estar activo y el agente debe estar dentro de la distancia de tolerancia para considerar que ha llegado al destino
        if (!activo) return;
        if (Vector3.Distance(agentTransform.position, destino) > tolerancia) return;
        activo = false;
        OnLlegadaCompletada?.Invoke();
    }
}
