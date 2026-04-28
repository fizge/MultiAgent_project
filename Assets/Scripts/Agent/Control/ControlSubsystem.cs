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
        // Implementamos la regla de "Inhibición" con prioridad dinámica:
        // - Reactiva urgente (ataque, persecución...) siempre gana.
        // - Si la reactiva solo está patrullando (no urgente), la Deliberativa puede tomar el control.
        // - Si la Deliberativa tampoco tiene tarea, la patrulla actúa como fallback.
        ActionProposal elegida = null;

        if (propReactiva != null && propReactiva.solicitaControl && propReactiva.esPrioridadAlta)
        {
            // Tarea urgente: reactiva gana sin importar lo que proponga la deliberativa.
            elegida = propReactiva;
        }
        else if (propDeliberativa != null && propDeliberativa.solicitaControl)
        {
            // Sin urgencia reactiva: la deliberativa toma el control si tiene tarea pendiente.
            elegida = propDeliberativa;
        }
        else if (propReactiva != null && propReactiva.solicitaControl)
        {
            // Fallback: solo patrulla o vigilancia idle, ninguna tarea deliberativa activa.
            elegida = propReactiva;
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