using UnityEngine;

[RequireComponent(typeof(VisionSensor), typeof(MovementSensor), typeof(AgentActuator))]
public class GuardianBrain : MonoBehaviour
{
    [Header("Vigilancia")]
    public float velocidadRotacionIdle = 30f;
    public float anguloVigilancia = 90f;

    [Header("Persecución")]
    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;
    public float distanciaAtaque = 1.5f;

    private VisionSensor sensorVision;
    private MovementSensor sensorMovimiento;
    private AgentActuator actuador;

    private float anguloGiroActual = 0f;
    private float direccionGiro = 1f;

    private enum Estado { Quieto, Persiguiendo, Atacando }
    private Estado estadoActual;

    void Start()
    {
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();
        actuador = GetComponent<AgentActuator>();

        estadoActual = Estado.Quieto;
    }

    void Update()
    {
        switch (estadoActual)
        {
            case Estado.Quieto:
                DeliberarVigilancia();
                break;
            case Estado.Persiguiendo:
                DeliberarPersecucion();
                break;
            case Estado.Atacando:
                DeliberarAtaque();
                break;
        }
    }

    void DeliberarVigilancia()
    {
        actuador.Detener();

        float rotacionFrame = velocidadRotacionIdle * Time.deltaTime * direccionGiro;
        actuador.Rotar(rotacionFrame);
        anguloGiroActual += rotacionFrame;

        if (anguloGiroActual >= anguloVigilancia)
            direccionGiro = -1f;
        else if (anguloGiroActual <= -anguloVigilancia)
            direccionGiro = 1f;

        if (sensorVision.PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
        }
    }

    void DeliberarPersecucion()
    {
        if (!sensorVision.PuedeVerLadron())
        {
            estadoActual = Estado.Quieto;
            anguloGiroActual = 0f;
            direccionGiro = 1f;
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
                estadoActual = Estado.Quieto;
                anguloGiroActual = 0f;
                direccionGiro = 1f;
            }
        }
    }
}