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
    public float margenPared = 1.5f;

    private VisionSensor sensorVision;
    private MovementSensor sensorMovimiento;

    private Vector3 puntoA, puntoB, destinoActual;

    // Estado interno actualizado por eventos de los sensores.
    private bool ladronVisible = false;
    private bool dentroDeRango = false;
    private bool destinoAlcanzado = false;

    void Awake()
    {
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();

        sensorVision.OnLadronAvistado    += () => ladronVisible = true;
        sensorVision.OnLadronPerdido     += () => ladronVisible = false;
        sensorVision.OnLadronMuyCerca    += () => dentroDeRango = true;
        sensorVision.OnLadronLejos       += () => dentroDeRango = false;
        sensorVision.OnEscaneoCompletado += PlanificarNuevaRuta;
        sensorMovimiento.OnDestinoAlcanzado += () => destinoAlcanzado = true;
    }

    public override ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();
        prop.solicitaControl = true;

        // Regla 1: ladrón visible -> perseguir y atacar.
        if (ladronVisible)
        {
            Vector3 posEnemigo = sensorVision.PosicionLadron;
            prop.destinoMovimiento = posEnemigo;
            prop.velocidad = velocidadCorrer;
            prop.aceleracion = aceleracionCorrer;
            prop.distanciaParada = distanciaAtaque;
            prop.corriendo = true;

            if (dentroDeRango)
            {
                prop.atacar = true;
                prop.objetivoAtaque = posEnemigo;
            }
        }
        // Regla 2: sin amenaza -> patrullar.
        else
        {
            if (destinoAlcanzado) { CambiarDestinoDePatrulla(); destinoAlcanzado = false; }

            prop.destinoMovimiento = destinoActual;
            prop.velocidad = velocidadCaminar;
            prop.aceleracion = aceleracionCaminar;
            prop.distanciaParada = 0f;
            prop.corriendo = false;
        }

        return prop;
    }

    // Recibe los datos del escaneo del sensor y calcula los puntos de patrulla.
    void PlanificarNuevaRuta(Vector3[] direcciones, float[] distancias)
    {
        destinoAlcanzado = false;
        puntoA = transform.position;
        float maxDistanciaEncontrada = 0f;
        Vector3 mejorPuntoB = puntoA;

        for (int i = 0; i < direcciones.Length; i++)
        {
            float distanciaLibre = distancias[i];
            if (distanciaLibre > margenPared)
            {
                distanciaLibre -= margenPared;
                if (distanciaLibre > maxDistanciaEncontrada)
                {
                    maxDistanciaEncontrada = distanciaLibre;
                    mejorPuntoB = puntoA + direcciones[i] * distanciaLibre;
                }
            }
        }
        puntoB = mejorPuntoB;
        destinoActual = puntoB;
    }

    // Alterna el destino entre puntoA y puntoB.
    void CambiarDestinoDePatrulla() => destinoActual = (Vector3.SqrMagnitude(destinoActual - puntoA) < 0.1f) ? puntoB : puntoA;
}
