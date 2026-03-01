using UnityEngine;

[RequireComponent(typeof(ReactiveLayer), typeof(DeliberativeLayer), typeof(AgentActuator))]
public class ControlSubsystem : MonoBehaviour
{
    private ReactiveLayer capaReactiva;
    private DeliberativeLayer capaDeliberativa;
    private AgentActuator actuador;

    void Awake()
    {
        capaReactiva = GetComponent<ReactiveLayer>();
        capaDeliberativa = GetComponent<DeliberativeLayer>();
        actuador = GetComponent<AgentActuator>();
    }

    void Update()
    {
        if (actuador.EstaAtacando) return;

        ActionProposal propReactiva = capaReactiva.GenerarPropuesta();
        ActionProposal propDeliberativa = capaDeliberativa.GenerarPropuesta();

        ActionProposal elegida = null;

        // Regla de mediación: Reactiva tiene prioridad (Inhibición)
        if (propReactiva.solicitaControl) 
        {
            elegida = propReactiva;
        }
        else if (propDeliberativa.solicitaControl)
        {
            elegida = propDeliberativa;
        }

        if (elegida != null)
        {
            if (elegida.atacar)
            {
                actuador.IniciarAtaque(elegida.objetivoAtaque);
            }
            else if (elegida.rotarEnLugar)
            {
                actuador.Rotar(elegida.gradosRotacion);
            }
            else
            {
                actuador.MoverA(elegida.destinoMovimiento, elegida.velocidad, 
                               elegida.aceleracion, elegida.distanciaParada, elegida.corriendo);
            }
        }
    }
}