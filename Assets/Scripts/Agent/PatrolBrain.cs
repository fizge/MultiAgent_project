using UnityEngine;

[RequireComponent(typeof(VisionSensor), typeof(AgentActuator))]
public class PatrolBrain : MonoBehaviour, IDamageable
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

    private VisionSensor sensor;
    private AgentActuator actuador;

    private Vector3 puntoA;
    private Vector3 puntoB;
    private Vector3 destinoActual;

    private enum Estado { Patrullando, Persiguiendo, Atacando }
    private Estado estadoActual;

    void Start()
    {
        sensor = GetComponent<VisionSensor>();
        actuador = GetComponent<AgentActuator>();

        if (actuador.swordHitbox != null)
            actuador.swordHitbox.SetOwner(this);

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
        if (sensor.PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            return;
        }

        if (actuador.HaLlegadoAlDestino())
        {
            CambiarDestinoDePatrulla();
        }

        actuador.MoverA(destinoActual, velocidadCaminar, aceleracionCaminar, 0f, false);
    }

    void DeliberarPersecucion()
    {
        if (!sensor.PuedeVerLadron())
        {
            estadoActual = Estado.Patrullando;
            PlanificarNuevaRuta();
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
                estadoActual = Estado.Patrullando;
                PlanificarNuevaRuta();
            }
        }
    }

    void PlanificarNuevaRuta()
    {
        puntoA = transform.position;
        float maxDistanciaEncontrada = 0f;
        Vector3 mejorPuntoB = puntoA;

        for (int i = 0; i < direccionesEscaneo; i++)
        {
            float angulo = i * (360f / direccionesEscaneo);
            Vector3 direccion = Quaternion.Euler(0, angulo, 0) * Vector3.forward;

            // 1. Sentir
            float distanciaLibre = sensor.MedirDistanciaLibre(direccion, distanciaMaxEscaneo);

            // 2. Pensar
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

        // 3. Decidir
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

    public void OnSwordHitLadron()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}