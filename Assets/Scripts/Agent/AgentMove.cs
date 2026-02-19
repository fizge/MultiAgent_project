using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class GuardiaExteriorIA : MonoBehaviour
{
    public Transform ladron;
    public float rangoVision = 10f;
    public float rangoAtaque = 1.5f;
    public float velocidadRotacion = 30f;
    public float limitePuertaZ;

    private NavMeshAgent agente;
    private Animator animator;
    private Vector3 posicionInicial;

    private bool yaAtacando = false;

    private enum Estado
    {
        Vigilando,
        Persiguiendo,
        Volviendo,
        Atacando
    }

    private Estado estadoActual;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        posicionInicial = transform.position;
        estadoActual = Estado.Vigilando;
    }

    void Update()
    {
        switch (estadoActual)
        {
            case Estado.Vigilando:
                Vigilando();
                break;

            case Estado.Persiguiendo:
                Persiguiendo();
                break;

            case Estado.Volviendo:
                Volviendo();
                break;

            case Estado.Atacando:
                Atacando();
                break;
        }
    }

    void Vigilando()
    {
        agente.isStopped = true;

        transform.Rotate(0f, velocidadRotacion * Time.deltaTime, 0f);

        if (PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            agente.isStopped = false;
        }
    }

    void Persiguiendo()
    {
        if (!PuedeVerLadron())
        {
            estadoActual = Estado.Volviendo;
            return;
        }

        if (Vector3.Distance(transform.position, ladron.position) <= rangoAtaque)
        {
            estadoActual = Estado.Atacando;
            return;
        }

        agente.SetDestination(ladron.position);
    }

    void Volviendo()
    {
        agente.isStopped = false;
        agente.SetDestination(posicionInicial);

        if (Vector3.Distance(transform.position, posicionInicial) < 0.5f)
        {
            estadoActual = Estado.Vigilando;
        }
    }

    void Atacando()
    {
        if (yaAtacando) return;

        yaAtacando = true;
        agente.isStopped = true;

        // Mirar al ladrón
        Vector3 direccion = (ladron.position - transform.position).normalized;
        direccion.y = 0;
        transform.rotation = Quaternion.LookRotation(direccion);

        // Lanzar animación
        animator.SetTrigger("Attack");
    }

    bool PuedeVerLadron()
    {
        float distancia = Vector3.Distance(transform.position, ladron.position);

        if (distancia > rangoVision)
            return false;

        if (ladron.position.z > limitePuertaZ)
            return false;

        return true;
    }

    public void FinAtaque()
    {
        GameManager.instance.Reiniciar();
    }
}
