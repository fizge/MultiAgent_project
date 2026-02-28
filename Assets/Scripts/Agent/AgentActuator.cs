using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AgentActuator : MonoBehaviour
{
    private NavMeshAgent agente;
    private Animator animator;
    public SwordHitbox swordHitbox;

    [Header("Configuración de Ataque")]
    public float idleAntesAtaque = 0.2f;
    public float delayActivarHitbox = 0.3f;
    public float ventanaHitbox = 0.15f;
    public float cooldownAtaque = 1.5f;

    public bool EstaAtacando { get; private set; }

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    // Mueve al agente hacia un punto
    public void MoverA(Vector3 destino, float velocidad, float aceleracion, float stoppingDist, bool corriendo)
    {
        agente.isStopped = false;
        agente.speed = velocidad;
        agente.acceleration = aceleracion;
        agente.stoppingDistance = stoppingDist;
        agente.SetDestination(destino);
        
        animator.SetBool("isRunning", corriendo);
    }

    // Detiene al agente por completo
    public void Detener()
    {
        agente.isStopped = true;
        animator.SetBool("isRunning", false);
    }

    // Rota al agente una cantidad específica de grados (Útil para el Guardián)
    public void Rotar(float grados)
    {
        transform.Rotate(0f, grados, 0f);
    }

    // Rota al agente para mirar a un punto concreto
    public void MirarHacia(Vector3 objetivo)
    {
        Vector3 dir = objetivo - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // Devuelve true si ha llegado a su destino en el NavMesh
    public bool HaLlegadoAlDestino()
    {
        return !agente.pathPending && agente.remainingDistance <= 0.3f;
    }

    // Devuelve true si está a cierta distancia de su objetivo actual
    public bool EstaCercaDelObjetivo(float distancia)
    {
        return !agente.pathPending && agente.remainingDistance <= distancia;
    }

    // Ejecuta la rutina física del ataque
    public void IniciarAtaque(Transform objetivo)
    {
        if (!EstaAtacando)
            StartCoroutine(RutinaAtaque(objetivo));
    }

    private IEnumerator RutinaAtaque(Transform objetivo)
    {
        EstaAtacando = true;
        Detener();
        MirarHacia(objetivo.position);

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

        EstaAtacando = false;
    }
}