using UnityEngine;

// Este módulo actúa como el "Control Subsystem" del modelo TOURINGMACHINES.
// Su objetivo es decidir qué capa de la IA (Reactiva o Deliberativa) toma el control del agente en cada fotograma.
public class ControlSubsystem : MonoBehaviour
{
    private ReactiveLayer capaReactiva;
    private DeliberativeLayer capaDeliberativa;
    private AgentActuator actuador;

    void Awake()
    {
        // Al quitar el 'RequireComponent', es vital que estos tres scripts estén presentes en el Inspector.
        capaReactiva = GetComponent<ReactiveLayer>();
        capaDeliberativa = GetComponent<DeliberativeLayer>();
        actuador = GetComponent<AgentActuator>();
    }

    void Update()
    {
        // Prioridad física: Si el cuerpo está ejecutando un ataque, pausamos el razonamiento de las capas.
        if (actuador.EstaAtacando) return;

        // FASE DE PROPUESTA:
        // Ambas capas analizan la situación actual y devuelven su propuesta de acción (ActionProposal).
        ActionProposal propReactiva = capaReactiva.GenerarPropuesta();
        ActionProposal propDeliberativa = capaDeliberativa.GenerarPropuesta();

        // FASE DE ARBITRAJE:
        // Implementamos la regla de "Inhibición": la capa reactiva (supervivencia) tiene prioridad absoluta.
        ActionProposal elegida = null;

        if (propReactiva.solicitaControl) 
        {
            // Si la reactiva detecta una emergencia (ej. ve al ladrón), se ignora el plan de patrulla.
            elegida = propReactiva;
        }
        else if (propDeliberativa.solicitaControl)
        {
            // Si no hay amenazas, el agente sigue su plan estratégico (patrulla o vigilancia).
            elegida = propDeliberativa;
        }

        // FASE DE EJECUCIÓN:
        // Se envían las órdenes de la propuesta ganadora al subsistema de acción (Actuador).
        if (elegida != null)
        {
            if (elegida.atacar)
            {
                // Acción de ataque (prioridad reactiva).
                actuador.IniciarAtaque(elegida.objetivoAtaque);
            }
            else if (elegida.rotarEnLugar)
            {
                // Acción de rotación (típica de vigilancia deliberativa).
                actuador.Rotar(elegida.gradosRotacion);
            }
            else
            {
                // Acción de movimiento estándar (patrulla o persecución).
                actuador.MoverA(elegida.destinoMovimiento, elegida.velocidad, 
                               elegida.aceleracion, elegida.distanciaParada, elegida.corriendo);
            }
        }
    }
}