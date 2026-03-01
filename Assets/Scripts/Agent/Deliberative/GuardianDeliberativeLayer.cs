using UnityEngine;

// Implementación de la "Planning Layer" para vigilancia estática.
// Nota: Aunque es un movimiento cíclico simple, reside aquí para mantener la simetría con el agente patrullero en el modelo TouringMachines.
public class GuardianDeliberativeLayer : DeliberativeLayer
{
    public float velocidadRotacionIdle = 30f;
    public float anguloVigilancia = 90f;

    private float anguloGiroActual = 0f;
    private float direccionGiro = 1f;

    // Genera la propuesta de rotación para que el Árbitro la valide.
    public override ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();
        prop.solicitaControl = true; // El guardia siempre intenta vigilar si no hay emergencias.

        // Calcula la rotación frame a frame.
        float rotacionFrame = velocidadRotacionIdle * Time.deltaTime * direccionGiro;
        anguloGiroActual += rotacionFrame;

        // Controla los límites de giro para oscilar la vista.
        if (anguloGiroActual >= anguloVigilancia) direccionGiro = -1f;
        else if (anguloGiroActual <= -anguloVigilancia) direccionGiro = 1f;

        prop.rotarEnLugar = true;
        prop.gradosRotacion = rotacionFrame;
        prop.destinoMovimiento = transform.position; // Ordena no desplazarse del sitio.
        
        return prop;
    }
}