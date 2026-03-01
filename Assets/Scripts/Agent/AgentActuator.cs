using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Este componente actúa como los "músculos" del agente (Subsistema de Acción).
// Recibe órdenes del ControlSubsystem y las ejecuta usando el NavMeshAgent y el Animator.
public class AgentActuator : MonoBehaviour
{
    private NavMeshAgent agente;
    private Animator animator;
    public SwordHitbox swordHitbox; // Referencia al script que gestiona el daño de la espada

    public float idleAntesAtaque = 0.2f;    // Pausa breve antes de soltar el golpe
    public float delayActivarHitbox = 0.3f; // Tiempo que tarda la animación en llegar al punto de impacto
    public float ventanaHitbox = 0.15f;     // Tiempo que el daño permanece activo
    public float cooldownAtaque = 1.5f;     // Tiempo de espera entre ataques

    // Propiedad que indica a las capas de control si el cuerpo está ocupado atacando
    public bool EstaAtacando { get; private set; }

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    // Traduce una propuesta de movimiento en comandos para el NavMeshAgent
    public void MoverA(Vector3 destino, float velocidad, float aceleracion, float stoppingDist, bool corriendo)
    {
        agente.isStopped = false;
        agente.speed = velocidad;
        agente.acceleration = aceleracion;
        agente.stoppingDistance = stoppingDist;
        agente.SetDestination(destino);
        
        // Sincroniza la animación de carrera con la orden recibida
        animator.SetBool("isRunning", corriendo);
    }

    // Detiene físicamente al agente y apaga la animación de movimiento
    public void Detener()
    {
        agente.isStopped = true;
        animator.SetBool("isRunning", false);
    }

    // Rota el cuerpo del agente sobre su eje Y (usado para la vigilancia del Guardián)
    public void Rotar(float grados)
    {
        transform.Rotate(0f, grados, 0f);
    }

    // Orienta el frente del agente hacia una coordenada específica antes de atacar
    public void MirarHacia(Vector3 objetivo)
    {
        Vector3 dir = objetivo - transform.position;
        dir.y = 0f; // Mantenemos el eje Y a 0 para evitar rotaciones extrañas hacia arriba o abajo
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // Punto de entrada para ejecutar la acción de combate
    public void IniciarAtaque(Vector3 posicionObjetivo)
    {
        if (!EstaAtacando)
            StartCoroutine(RutinaAtaque(posicionObjetivo));
    }

    // Corrutina que gestiona la secuencia física del ataque
    private IEnumerator RutinaAtaque(Vector3 posicionObjetivo)
    {
        EstaAtacando = true; // Bloquea nuevas órdenes del ControlSubsystem mientras dure el ataque
        Detener();
        MirarHacia(posicionObjetivo); 

        yield return new WaitForSeconds(idleAntesAtaque);

        // Dispara la animación de ataque configurada en el Animator
        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(delayActivarHitbox);

        // Activa el trigger de la espada durante una pequeña ventana de tiempo
        if (swordHitbox != null)
        {
            swordHitbox.EnableHitbox(true);
            yield return new WaitForSeconds(ventanaHitbox);
            swordHitbox.EnableHitbox(false);
        }

        yield return new WaitForSeconds(cooldownAtaque);

        EstaAtacando = false; // Libera el cuerpo para que vuelva a recibir órdenes mentales
    }
}