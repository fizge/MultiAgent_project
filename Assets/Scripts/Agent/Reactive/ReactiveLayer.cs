using UnityEngine;

// Capa base para la Reactive Layer.
public abstract class ReactiveLayer : MonoBehaviour
{
    // Método para enviar propuestas reactivas al árbitro central.
    public abstract ActionProposal GenerarPropuesta();
}