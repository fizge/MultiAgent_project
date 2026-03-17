using UnityEngine;

// Reactive Layer para guardias patrulleros.
// Gestiona: persecución y ataque al avistar al ladrón, planificación de ruta y movimiento de patrulla.
public class PatrolReactiveLayer : ReactiveLayer
{

    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;
    public float distanciaAtaque = 1.5f;
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
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();
    }

    public override ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();
        prop.solicitaControl = true;

        // Si detecta al ladrón -> perseguir y atacar.
        if (sensorVision.PuedeVerLadron())
        {
            Vector3 posEnemigo = sensorVision.ObtenerPosicionObjetivo();
            prop.destinoMovimiento = posEnemigo;
            prop.velocidad = velocidadCorrer;
            prop.aceleracion = aceleracionCorrer;
            prop.distanciaParada = distanciaAtaque;
            prop.corriendo = true;

            if (sensorMovimiento.EstaARangoFisico(posEnemigo, distanciaAtaque))
            {
                prop.atacar = true;
                prop.objetivoAtaque = posEnemigo;
            }
        }
        // Sin amenaza -> patrullar.
        else
        {
            if (!rutaCalculada) { PlanificarNuevaRuta(); rutaCalculada = true; }
            if (sensorMovimiento.HaLlegadoAlDestino()) CambiarDestinoDePatrulla();

            prop.destinoMovimiento = destinoActual;
            prop.velocidad = velocidadCaminar;
            prop.aceleracion = aceleracionCaminar;
            prop.distanciaParada = 0f;
            prop.corriendo = false;
        }

        return prop;
    }

    // Analiza el entorno con raycasts de NavMesh para encontrar el punto más lejano sin obstáculos.
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

    // Alterna el destino entre puntoA y puntoB.
    void CambiarDestinoDePatrulla() => destinoActual = (Vector3.SqrMagnitude(destinoActual - puntoA) < 0.1f) ? puntoB : puntoA;
}
