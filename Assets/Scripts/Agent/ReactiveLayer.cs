using UnityEngine;

// Implementa la "Reactive layer" encargada de respuestas inmediatas ante estímulos.
public class ReactiveLayer : MonoBehaviour
{
    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;
    public float distanciaAtaque = 1.5f;

    private VisionSensor sensorVision;
    private MovementSensor sensorMovimiento;

    void Awake()
    {
        // Inicializa la comunicación con el subsistema de percepción.
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();
    }

    // Evalúa condiciones críticas para generar una propuesta que inhiba a las capas superiores.
    public ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();

        // Regla de condición-acción: Si detecta al objetivo, activa el modo persecución.
        if (sensorVision.PuedeVerLadron())
        {
            // Solicita el control total para activar la regla de inhibición en el árbitro.
            prop.solicitaControl = true;
            Vector3 posEnemigo = sensorVision.ObtenerPosicionObjetivo();
            
            prop.destinoMovimiento = posEnemigo;
            prop.velocidad = velocidadCorrer;
            prop.aceleracion = aceleracionCorrer;
            prop.distanciaParada = distanciaAtaque;
            prop.corriendo = true;

            // Validación de rango mediante el sensor de movimiento para ejecutar el ataque.
            if (sensorMovimiento.EstaARangoFisico(posEnemigo, distanciaAtaque))
            {
                prop.atacar = true;
                prop.objetivoAtaque = posEnemigo;
            }
        }
        return prop;
    }
}