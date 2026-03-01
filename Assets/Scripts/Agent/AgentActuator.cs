using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AgentActuator : MonoBehaviour
{
    private NavMeshAgent agente;
    private Animator animator;
    public SwordHitbox swordHitbox;

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

    public void MoverA(Vector3 destino, float velocidad, float aceleracion, float stoppingDist, bool corriendo)
    {
        agente.isStopped = false;
        agente.speed = velocidad;
        agente.acceleration = aceleracion;
        agente.stoppingDistance = stoppingDist;
        agente.SetDestination(destino);
        
        animator.SetBool("isRunning", corriendo);
    }

    public void Detener()
    {
        agente.isStopped = true;
        animator.SetBool("isRunning", false);
    }

    public void Rotar(float grados)
    {
        transform.Rotate(0f, grados, 0f);
    }

    public void MirarHacia(Vector3 objetivo)
    {
        Vector3 dir = objetivo - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // Ahora recibe un Vector3 en lugar de un Transform físico
    public void IniciarAtaque(Vector3 posicionObjetivo)
    {
        if (!EstaAtacando)
            StartCoroutine(RutinaAtaque(posicionObjetivo));
    }

    private IEnumerator RutinaAtaque(Vector3 posicionObjetivo)
    {
        EstaAtacando = true;
        Detener();
        MirarHacia(posicionObjetivo); // Mira hacia las coordenadas

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