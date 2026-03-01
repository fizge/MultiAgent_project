using UnityEngine;

// Implementa la planificación estratégica de rutas para la vigilancia (Planning Layer).
public class PatrolDeliberativeLayer : DeliberativeLayer
{
    public float velocidadCaminar = 2f;
    public float aceleracionCaminar = 2f;
    public int direccionesEscaneo = 16;
    public float distanciaMaxEscaneo = 50f;
    public float margenPared = 1.5f;

    private VisionSensor sensorVision;
    private MovementSensor sensorMovimiento;
    private Vector3 puntoA, puntoB, destinoActual;
    private bool rutaCalculada = false;

    void Awake()
    {
        // Obtiene referencias a los sensores del agente.
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();
    }

    // Elabora y envía el plan de patrulla al árbitro central.
    public override ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();
        prop.solicitaControl = true; // Desea ejecutar la patrulla si no hay emergencias.

        if (!rutaCalculada) { PlanificarNuevaRuta(); rutaCalculada = true; }
        
        // Si el sensor detecta que llegamos al destino, cambiamos de sentido.
        if (sensorMovimiento.HaLlegadoAlDestino()) CambiarDestinoDePatrulla();

        prop.destinoMovimiento = destinoActual;
        prop.velocidad = velocidadCaminar;
        prop.aceleracion = aceleracionCaminar;
        prop.distanciaParada = 0f;
        prop.corriendo = false;
        
        return prop;
    }

    // Analiza el entorno mediante rayos para encontrar el camino más despejado.
    void PlanificarNuevaRuta()
    {
        puntoA = transform.position;
        float maxDistanciaEncontrada = 0f;
        Vector3 mejorPuntoB = puntoA;

        for (int i = 0; i < direccionesEscaneo; i++)
        {
            float angulo = i * (360f / direccionesEscaneo);
            Vector3 direccion = Quaternion.Euler(0, angulo, 0) * Vector3.forward;
            float distanciaLibre = sensorVision.MedirDistanciaLibre(direccion, distanciaMaxEscaneo);

            if (distanciaLibre > margenPared)
            {
                distanciaLibre -= margenPared;
                if (distanciaLibre > maxDistanciaEncontrada)
                {
                    maxDistanciaEncontrada = distanciaLibre;
                    mejorPuntoB = puntoA + direccion * distanciaLibre;
                }
            }
        }
        puntoB = mejorPuntoB;
        destinoActual = puntoB;
    }

    // Alterna el objetivo entre el punto de origen (A) y el punto explorado (B).
    void CambiarDestinoDePatrulla() => destinoActual = (Vector3.SqrMagnitude(destinoActual - puntoA) < 0.1f) ? puntoB : puntoA;
}