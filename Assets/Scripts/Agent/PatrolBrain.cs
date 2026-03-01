using UnityEngine;

[RequireComponent(typeof(VisionSensor), typeof(MovementSensor), typeof(AgentActuator))]
public class PatrolBrain : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidadCaminar = 2f;
    public float aceleracionCaminar = 2f;
    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;
    public float distanciaAtaque = 1.5f;

    [Header("Cálculo de Patrulla")]
    public int direccionesEscaneo = 16;
    public float distanciaMaxEscaneo = 50f;
    public float margenPared = 1.5f;

    private VisionSensor sensorVision;
    private MovementSensor sensorMovimiento;
    private AgentActuator actuador;

    private Vector3 puntoA;
    private Vector3 puntoB;
    private Vector3 destinoActual;

    private enum Estado { Patrullando, Persiguiendo, Atacando }
    private Estado estadoActual;

    void Start()
    {
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();
        actuador = GetComponent<AgentActuator>();

        estadoActual = Estado.Patrullando;
        PlanificarNuevaRuta();
    }

    void Update()
    {
        switch (estadoActual)
        {
            case Estado.Patrullando:
                DeliberarPatrulla();
                break;
            case Estado.Persiguiendo:
                DeliberarPersecucion();
                break;
            case Estado.Atacando:
                DeliberarAtaque();
                break;
        }
    }

    void DeliberarPatrulla()
    {
        if (sensorVision.PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            return;
        }

        if (sensorMovimiento.HaLlegadoAlDestino())
        {
            CambiarDestinoDePatrulla();
        }

        actuador.MoverA(destinoActual, velocidadCaminar, aceleracionCaminar, 0f, false);
    }

    void DeliberarPersecucion()
    {
        if (!sensorVision.PuedeVerLadron())
        {
            estadoActual = Estado.Patrullando;
            PlanificarNuevaRuta();
            return;
        }

        Vector3 posicionEnemigo = sensorVision.ObtenerPosicionObjetivo();
        actuador.MoverA(posicionEnemigo, velocidadCorrer, aceleracionCorrer, distanciaAtaque, true);

        if (sensorMovimiento.EstaCercaDelObjetivo(distanciaAtaque) && !actuador.EstaAtacando)
        {
            estadoActual = Estado.Atacando;
            actuador.IniciarAtaque(posicionEnemigo);
        }
    }

    void DeliberarAtaque()
    {
        if (!actuador.EstaAtacando)
        {
            if (sensorVision.PuedeVerLadron())
                estadoActual = Estado.Persiguiendo;
            else
            {
                estadoActual = Estado.Patrullando;
                PlanificarNuevaRuta();
            }
        }
    }

    void PlanificarNuevaRuta()
    {
        puntoA = sensorMovimiento.ObtenerPosicionActual();
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

    void CambiarDestinoDePatrulla()
    {
        if (Vector3.SqrMagnitude(destinoActual - puntoA) < 0.1f)
            destinoActual = puntoB;
        else
            destinoActual = puntoA;
    }
}