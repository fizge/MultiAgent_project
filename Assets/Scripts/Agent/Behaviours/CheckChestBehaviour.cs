// Comportamiento que ejecuta el compromiso ganado en el Contract Net: ir al cofre y comprobar
// si sigue intacto consultando GameManager.hasLoot. Notifica el resultado a la Deliberativa
// mediante el callback OnComprobacionCompletada. Vive en la Deliberativa, no en la Reactiva,
// porque solo se activa cuando el patrullero gana el contrato.
using System;
using UnityEngine;

public class CheckChestBehaviour : IBehaviour
{
    public float speed = 2f;
    public float acceleration = 2f;
    public float toleranciaCofre = 2f; // distancia mínima al cofre para considerar que se ha llegado

    private Transform agentTransform;
    private Transform cofre;
    private bool activo;

    // Callback invocado al confirmar la llegada al cofre. exito=true si el cofre sigue intacto.
    public event Action<bool> OnComprobacionCompletada;

    public void Initialize(Transform agentTransform, Transform cofre, MovementSensor movementSensor)
    {
        this.agentTransform = agentTransform;
        this.cofre = cofre;
        movementSensor.OnDestinoAlcanzado += AlLlegar;
    }

    // La Deliberativa lo activa al recibir ACCEPT_PROPOSAL.
    public void Activar() { activo = true; }

    // La Deliberativa lo desactiva al recibir CANCEL o tras notificar el resultado.
    public void Desactivar() { activo = false; }

    public bool EstaActivo => activo;

    public ActionProposal Evaluate()
    {
        if (!activo || cofre == null) return null;

        return new ActionProposal
        {
            solicitaControl = true,
            destinoMovimiento = cofre.position,
            velocidad = speed,
            aceleracion = acceleration,
            distanciaParada = 0.5f,
            corriendo = false
        };
    }

    // Se dispara cuando MovementSensor anuncia llegada a cualquier destino. Filtramos por distancia
    // al cofre para descartar llegadas espurias provocadas por la Reactiva (p.ej. tras una persecución).
    private void AlLlegar()
    {
        if (!activo || cofre == null || agentTransform == null) return;
        if (Vector3.Distance(agentTransform.position, cofre.position) > toleranciaCofre) return;

        bool cofreIntacto = !(GameManager.Instance != null && GameManager.Instance.hasLoot);
        activo = false;
        OnComprobacionCompletada?.Invoke(cofreIntacto);
    }
}
