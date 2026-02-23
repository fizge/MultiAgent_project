using UnityEngine;
using UnityEngine.AI;

public class AgentMove : MonoBehaviour
{
    public Transform ladron; // El protagonista (ladrón)
    public float rangoVision = 10f; // Rango de visión
    public float rangoAtaque = 1.5f; // Rango de ataque
    public float velocidadRotacion = 30f; // Velocidad de rotación

    private NavMeshAgent agente;
    private Animator animator;
    private Vector3 posicionInicial; // Posición inicial para volver cuando el ladrón se aleje
    private bool yaAtacando = false; // Para evitar que el agente ataque varias veces

    private enum Estado
    {
        Quieto,         // Estado cuando el agente está parado
        Persiguiendo,   // Estado cuando el agente persigue
        Atacando        // Estado cuando el agente está atacando
    }

    private Estado estadoActual;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        posicionInicial = transform.position;
        estadoActual = Estado.Quieto; // Empieza quieto
    }

    void Update()
    {
        // Cambiar el estado basado en el rango de visión y la distancia al protagonista
        switch (estadoActual)
        {
            case Estado.Quieto:
                Vigilando();
                break;

            case Estado.Persiguiendo:
                Persiguiendo();
                break;

            case Estado.Atacando:
                Atacando();
                break;
        }
    }

    // -----------------------------
    // Vigilando, el agente está quieto
    // -----------------------------
    void Vigilando()
    {
        agente.isStopped = true; // El agente no se mueve mientras está quieto

        // Rotar el agente para simular vigilancia (puedes personalizarlo)
        transform.Rotate(0f, velocidadRotacion * Time.deltaTime, 0f);

        if (PuedeVerLadron()) // Si ve al ladrón, comienza a perseguir
        {
            estadoActual = Estado.Persiguiendo;
            agente.isStopped = false; // Permite que el agente se mueva
        }
    }

    // -----------------------------
    // Persiguiendo, el agente sigue al ladrón
    // -----------------------------
    void Persiguiendo()
    {
        if (!PuedeVerLadron()) // Si el ladrón se va fuera de la visión, vuelve al estado Quieto
        {
            estadoActual = Estado.Quieto;
            return;
        }

        // Si el agente está lo suficientemente cerca, comienza a atacar
        if (Vector3.Distance(transform.position, ladron.position) <= rangoAtaque)
        {
            estadoActual = Estado.Atacando;
            return;
        }

        // Mover hacia la posición del ladrón
        agente.SetDestination(ladron.position);
        animator.SetBool("isRunning", true); // Activamos la animación de correr
    }

    // -----------------------------
    // Atacando, el agente se detiene para atacar
    // -----------------------------
    void Atacando()
    {
        if (yaAtacando) return;

        yaAtacando = true;
        agente.isStopped = true; // El agente se detiene al atacar

        // Mirar al ladrón
        Vector3 direccion = (ladron.position - transform.position).normalized;
        direccion.y = 0; // Mantener el personaje en el plano 2D
        transform.rotation = Quaternion.LookRotation(direccion);

        // Lanzar la animación de ataque
        animator.SetTrigger("Attack");
        animator.SetBool("isAttacking", true); // Iniciar la animación de ataque
    }

    // -----------------------------
    // Verificación de si el agente puede ver al ladrón
    // -----------------------------
    bool PuedeVerLadron()
    {
        float distancia = Vector3.Distance(transform.position, ladron.position);

        // El agente puede ver al ladrón solo si está dentro del rango de visión
        if (distancia > rangoVision)
            return false;

        return true;
    }

    // -----------------------------
    // Reseteo después de un ataque
    // -----------------------------
    public void FinAtaque()
    {
        // El ataque ha terminado, desactivar isAttacking
        animator.SetBool("isAttacking", false);
        yaAtacando = false;
        estadoActual = Estado.Quieto; // Vuelve a estar quieto después del ataque
    }
}