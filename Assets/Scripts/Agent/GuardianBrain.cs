using UnityEngine;

[RequireComponent(typeof(VisionSensor), typeof(AgentActuator))]
public class GuardianBrain : MonoBehaviour, IDamageable
{
    [Header("Vigilancia")]
    public float velocidadRotacionIdle = 30f;
    public float anguloVigilancia = 90f;

    [Header("Persecución")]
    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;
    public float distanciaAtaque = 1.5f;

    private VisionSensor sensor;
    private AgentActuator actuador;

    private float anguloGiroActual = 0f;
    private float direccionGiro = 1f;

    private enum Estado { Quieto, Persiguiendo, Atacando }
    private Estado estadoActual;

    void Start()
    {
        sensor = GetComponent<VisionSensor>();
        actuador = GetComponent<AgentActuator>();

        if (actuador.swordHitbox != null)
            actuador.swordHitbox.SetOwner(this);

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
        actuador.Detener(); // Músculos quietos

        // Pensamos cuánto hay que girar y ordenamos al músculo rotar
        float rotacionFrame = velocidadRotacionIdle * Time.deltaTime * direccionGiro;
        actuador.Rotar(rotacionFrame);
        anguloGiroActual += rotacionFrame;

        // Si llegamos al límite visual, cambiamos de sentido
        if (anguloGiroActual >= anguloVigilancia)
            direccionGiro = -1f;
        else if (anguloGiroActual <= -anguloVigilancia)
            direccionGiro = 1f;

        // Si ve al ladrón, cambia de plan
        if (sensor.PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
        }
    }

    void DeliberarPersecucion()
    {
        if (!sensor.PuedeVerLadron())
        {
            estadoActual = Estado.Quieto;
            anguloGiroActual = 0f; // Reseteamos el cuello
            direccionGiro = 1f;
            return;
        }

        actuador.MoverA(sensor.ladron.position, velocidadCorrer, aceleracionCorrer, distanciaAtaque, true);

        if (actuador.EstaCercaDelObjetivo(distanciaAtaque) && !actuador.EstaAtacando)
        {
            estadoActual = Estado.Atacando;
            actuador.IniciarAtaque(sensor.ladron);
        }
    }

    void DeliberarAtaque()
    {
        if (!actuador.EstaAtacando)
        {
            if (sensor.PuedeVerLadron())
                estadoActual = Estado.Persiguiendo;
            else
            {
                estadoActual = Estado.Quieto;
                anguloGiroActual = 0f;
                direccionGiro = 1f;
            }
        }
    }

    public void OnSwordHitLadron()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}