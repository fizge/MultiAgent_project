using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class GuardianAgentMove : MonoBehaviour, IDamageable
{

    public Transform ladron; // Transform del protagonista (a quién persigue)


    public float viewDistance = 5f;           // Distancia máxima a la que puede ver

    public float viewAngle = 180f;             // Ángulo del cono de visión
    public Transform eyePoint;                 // Punto de los ojos
    public LayerMask obstacleMask;             // Capas que bloquean la visión (paredes)
    public LayerMask targetMask;               // Capa del jugador


    public float distanciaAtaque = 1.5f;       // Distancia mínima para empezar a atacar
    public float velocidadRotacionIdle = 30f;  // Velocidad de giro cuando está vigilando

    public float idleAntesAtaque = 0.2f;      // Pequeña pausa antes de atacar
    public float delayActivarHitbox = 0.3f;   // Tiempo hasta que el golpe "sale"
    public float ventanaHitbox = 0.15f;        // Tiempo activo del collider del arma
    public float cooldownAtaque = 1.5f;        // Tiempo antes de poder volver a atacar

    public float anguloVigilancia = 90f;   // Cuánto gira hacia cada lado (90 a cada lado = 180 en total)
    private float anguloGiroActual = 0f;   // Rastrear por dónde va mirando
    private float direccionGiro = 1f;      // 1 para girar a la derecha, -1 para la izquierda
 
    public SwordHitbox swordHitbox;            // Script del arma que detecta impacto

    // Componentes internos
    private NavMeshAgent agente;
    private Animator animator;

    // Posición inicial para volver cuando pierde al jugador
    private Vector3 puestoInicialPos;
    private Quaternion puestoInicialRot;

    // Control de ataque
    private bool atacando;
    private Coroutine rutinaDeAtaque;

    // Estados posibles del guardia
    private enum Estado { Quieto, Persiguiendo, Volviendo, Atacando }
    private Estado estadoActual;

    void Start()
    {
        // Obtener componentes
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Guardamos la posición inicial
        puestoInicialPos = transform.position;
        puestoInicialRot = transform.rotation;

        // El agente se detendrá a esta distancia del objetivo
        agente.stoppingDistance = distanciaAtaque;

        estadoActual = Estado.Quieto;

        // Asignamos este script como dueño del arma
        if (swordHitbox != null)
            swordHitbox.SetOwner(this);
    }

    void Update()
    {
        // Máquina de estados principal
        switch (estadoActual)
        {
            case Estado.Quieto:
                Quieto();
                break;

            case Estado.Persiguiendo:
                Persiguiendo();
                break;

            case Estado.Volviendo:
                Volviendo();
                break;

            case Estado.Atacando:
                // El ataque se controla mediante coroutine
                break;
        }
    }

 
    // ESTADO: QUIETO

    void Quieto()
    {
        agente.isStopped = true;               // No se mueve
        animator.SetBool("isRunning", false);  // Animación idle

        // Calculamos cuánto va a girar en este frame exacto
        float rotacionFrame = velocidadRotacionIdle * Time.deltaTime * direccionGiro;
        
        // Aplicamos la rotación al modelo del guardia
        transform.Rotate(0f, rotacionFrame, 0f);
        
        // Sumamos (o restamos) el giro al total que estamos rastreando
        anguloGiroActual += rotacionFrame;

        // Si llega al tope derecho (ej: 90) o izquierdo (-90), invertimos la dirección
        if (anguloGiroActual >= anguloVigilancia)
        {
            direccionGiro = -1f; // Empieza a girar a la izquierda
        }
        else if (anguloGiroActual <= -anguloVigilancia)
        {
            direccionGiro = 1f;  // Empieza a girar a la derecha
        }

        // Si ve al jugador empieza persecución
        if (PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            agente.isStopped = false;
        }
    }

    
    // ESTADO: PERSIGUIENDO
    
    void Persiguiendo()
    {
        // Si deja de verlo, vuelve a su puesto
        if (!PuedeVerLadron())
        {
            estadoActual = Estado.Volviendo;
            agente.SetDestination(puestoInicialPos);
            animator.SetBool("isRunning", true);
            return;
        }

        // Sigue al jugador
        agente.SetDestination(ladron.position);
        animator.SetBool("isRunning", true);

        // Si está lo suficientemente cerca y no está atacando
        if (!agente.pathPending &&
            agente.remainingDistance <= agente.stoppingDistance &&
            !atacando)
        {
            estadoActual = Estado.Atacando;
            rutinaDeAtaque = StartCoroutine(RutinaAtaque());
        }
    }

  
    // ESTADO: VOLVIENDO AL PUESTO

    void Volviendo()
    {
        // Si vuelve a verlo, persigue otra vez
        if (PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            return;
        }

        agente.SetDestination(puestoInicialPos);
        animator.SetBool("isRunning", true);

        // Cuando llega, vuelve a estar quieto
        if (!agente.pathPending &&
            agente.remainingDistance <= agente.stoppingDistance)
        {
            transform.rotation = puestoInicialRot;
            anguloGiroActual = 0f;  // Reseteamos su memoria de giro
            direccionGiro = 1f;     // Hacemos que la próxima vez empiece hacia la derecha
            estadoActual = Estado.Quieto;
        }
    }

    // RUTINA DE ATAQUE
    
    IEnumerator RutinaAtaque()
    {
        atacando = true;

        agente.isStopped = true;               // Se detiene
        animator.SetBool("isRunning", false);  // Idle antes del golpe

        MirarAlLadron();

        yield return new WaitForSeconds(idleAntesAtaque);

        // Lanzar animación de ataque
        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(delayActivarHitbox);

        // Activar hitbox del arma
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
            estadoActual = Estado.Persiguiendo;
        else
            estadoActual = Estado.Volviendo;
    }

  
    // MIRAR AL JUGADOR

    void MirarAlLadron()
    {
        if (!ladron) return;

        Vector3 dir = ladron.position - transform.position;
        dir.y = 0f; // Mantener rotación horizontal

        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

   
    // SISTEMA DE VISIÓN 
    
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


    // CUANDO LA ESPADA TOCA AL JUGADOR
    
    public void OnSwordHitLadron()
    {     
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}