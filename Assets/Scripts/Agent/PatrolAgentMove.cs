using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class PatrolAgentMove : MonoBehaviour, IDamageable
{
    public Transform ladron; // Transform del protagonista

    [Header("Visión")]
    public float viewDistance = 12f;
    public float viewAngle = 180f;
    public Transform eyePoint;
    public LayerMask obstacleMask;
    public LayerMask targetMask;

    [Header("Ataque")]
    public float distanciaAtaque = 1.5f;
    public float idleAntesAtaque = 0.2f;
    public float delayActivarHitbox = 0.3f;
    public float ventanaHitbox = 0.15f;
    public float cooldownAtaque = 1.5f;

    [Header("Movimiento")]
    public float velocidadCaminar = 2f;
    public float aceleracionCaminar = 2f;
    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;

    [Header("Cálculo de Patrulla")]
    public int direccionesEscaneo = 16;      // Direcciones en las que buscará la ruta más larga
    public float distanciaMaxEscaneo = 50f;  // Límite máximo de visión para buscar el punto B
    public float margenPared = 1.5f;         // Margen para no chocarse con la pared al llegar al punto B

    public SwordHitbox swordHitbox;

    // Componentes internos
    private NavMeshAgent agente;
    private Animator animator;

    // Variables de la ruta de patrulla
    private Vector3 puntoA;
    private Vector3 puntoB;
    private Vector3 destinoActual;

    // Control de ataque
    private bool atacando;
    private Coroutine rutinaDeAtaque;

    // Estados posibles del patrullero
    private enum Estado { Patrullando, Persiguiendo, Atacando }
    private Estado estadoActual;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (swordHitbox != null)
            swordHitbox.SetOwner(this);

        estadoActual = Estado.Patrullando;
        CalcularNuevaRuta();
    }

    void Update()
    {
        // Máquina de estados principal
        switch (estadoActual)
        {
            case Estado.Patrullando:
                Patrullando();
                break;

            case Estado.Persiguiendo:
                Persiguiendo();
                break;

            case Estado.Atacando:
                // El ataque se controla mediante coroutine
                break;
        }
    }

    // ==========================================
    // CÁLCULO DE LA LÍNEA RECTA MÁS LARGA
    // ==========================================
    void CalcularNuevaRuta()
    {
        // El punto de inicio es donde está el agente ahora mismo
        puntoA = transform.position;
        float maxDistanciaEncontrada = 0f;
        Vector3 mejorPuntoB = puntoA; // Por defecto, se queda donde está

        // Escanear en 360 grados
        for (int i = 0; i < direccionesEscaneo; i++)
        {
            float angulo = i * (360f / direccionesEscaneo);
            Vector3 direccion = Quaternion.Euler(0, angulo, 0) * Vector3.forward;
            Vector3 destinoLejano = puntoA + direccion * distanciaMaxEscaneo;

            // NavMesh.Raycast devuelve TRUE si choca con un borde/obstáculo del NavMesh
            if (NavMesh.Raycast(puntoA, destinoLejano, out NavMeshHit hit, NavMesh.AllAreas))
            {
                float distanciaLibre = Vector3.Distance(puntoA, hit.position);
                
                // Si la distancia es suficiente, restamos el margen de la pared
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
            else
            {
                // Si no choca con nada, es un camino despejado hasta el infinito (o hasta distanciaMaxEscaneo)
                if (distanciaMaxEscaneo > maxDistanciaEncontrada)
                {
                    maxDistanciaEncontrada = distanciaMaxEscaneo;
                    mejorPuntoB = destinoLejano;
                }
            }
        }

        puntoB = mejorPuntoB;
        destinoActual = puntoB;
        
        // Empezamos yendo hacia el punto B
        agente.SetDestination(destinoActual);
    }

    // ==========================================
    // ESTADO: PATRULLANDO
    // ==========================================
    void Patrullando()
    {
        agente.speed = velocidadCaminar;
        agente.acceleration = aceleracionCaminar;
        agente.stoppingDistance = 0f; 
        animator.SetBool("isRunning", false);

        // Si ve al jugador empieza persecución
        if (PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            return;
        }

        // Si llega a su destino (Punto A o Punto B), cambia de dirección
        if (!agente.pathPending && agente.remainingDistance <= 0.3f)
        {
            // Intercambiamos el destino
            if (Vector3.SqrMagnitude(destinoActual - puntoA) < 0.1f)
                destinoActual = puntoB;
            else
                destinoActual = puntoA;

            agente.SetDestination(destinoActual);
        }
    }

    // ==========================================
    // ESTADO: PERSIGUIENDO
    // ==========================================
    void Persiguiendo()
    {
        agente.speed = velocidadCorrer;
        agente.acceleration = aceleracionCorrer;
        agente.stoppingDistance = distanciaAtaque;
        animator.SetBool("isRunning", true);

        // Si deja de verlo, busca una nueva ruta de patrulla desde este punto
        if (!PuedeVerLadron())
        {
            estadoActual = Estado.Patrullando;
            CalcularNuevaRuta();
            return;
        }

        // Sigue al jugador
        agente.SetDestination(ladron.position);

        // Si está lo suficientemente cerca y no está atacando
        if (!agente.pathPending &&
            agente.remainingDistance <= agente.stoppingDistance &&
            !atacando)
        {
            estadoActual = Estado.Atacando;
            rutinaDeAtaque = StartCoroutine(RutinaAtaque());
        }
    }

    // ==========================================
    // RUTINA DE ATAQUE
    // ==========================================
    IEnumerator RutinaAtaque()
    {
        atacando = true;

        agente.isStopped = true;
        animator.SetBool("isRunning", false);

        MirarAlLadron();

        yield return new WaitForSeconds(idleAntesAtaque);

        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(delayActivarHitbox);

        if (swordHitbox != null)
        {
            swordHitbox.EnableHitbox(true);
            yield return new WaitForSeconds(ventanaHitbox);
            swordHitbox.EnableHitbox(false);
        }

        yield return new WaitForSeconds(cooldownAtaque);

        atacando = false;
        rutinaDeAtaque = null;
        agente.isStopped = false;

        // Decide qué hacer después del ataque
        if (PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
        }
        else
        {
            estadoActual = Estado.Patrullando;
            CalcularNuevaRuta();
        }
    }

    // ==========================================
    // MIRAR AL JUGADOR
    // ==========================================
    void MirarAlLadron()
    {
        if (!ladron) return;

        Vector3 dir = ladron.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // ==========================================
    // SISTEMA DE VISIÓN 
    // ==========================================
    bool PuedeVerLadron()
    {
        if (!ladron) return false;

        Vector3 origin = eyePoint.position;
        Vector3 toTarget = ladron.position - origin;
        toTarget.y += 0.2f; // Ajuste para apuntar al torso

        if (toTarget.magnitude > viewDistance)
            return false;

        float ang = Vector3.Angle(transform.forward, toTarget);
        if (ang > viewAngle * 0.5f)
            return false;

        int mask = obstacleMask | targetMask;

        if (Physics.Raycast(origin, toTarget.normalized,
            out RaycastHit hit,
            viewDistance,
            mask,
            QueryTriggerInteraction.Ignore))
        {
            return hit.transform == ladron;
        }

        return false;
    }

    // ==========================================
    // CUANDO LA ESPADA TOCA AL JUGADOR
    // ==========================================
    public void OnSwordHitLadron()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}