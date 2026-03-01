using UnityEngine;

// Capa base para la Planning Layer.
public abstract class DeliberativeLayer : MonoBehaviour
{
    // Método para enviar planes al árbitro central.
    public abstract ActionProposal GenerarPropuesta();
}