using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class PatrolAgentMove : MonoBehaviour
{
    [Header("Target")]
    public Transform ladron;

    [Header("Patrulla Aleatoria")]
    public float maxPatrolDistance = 15f;
    public float minPatrolDistance = 5f;
    public int intentosBusqueda = 10;

    [Header("Vision")]
    public float viewDistance = 12f;
    public float viewAngle = 180f;
    public Transform eyePoint;
    public LayerMask obstacleMask;
    public LayerMask targetMask;
    public float torsoOffsetY = 0.2f;

    [Header("Attack")]
    public float distanciaAtaque = 1.5f;
    public float idleAntesAtaque = 0.2f;
    public float delayActivarHitbox = 0.3f;
    public float ventanaHitbox = 0.15f;
    public float cooldownAtaque = 1.5f;

    public SwordHitbox swordHitbox;

    private NavMeshAgent agente;
    private Animator animator;

    private bool atacando;
    private Coroutine rutinaDeAtaque;

    private enum Estado { Patrullando, Persiguiendo, Atacando }
    private Estado estadoActual;

    private Vector3 destinoActual;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        agente.stoppingDistance = distanciaAtaque;

        estadoActual = Estado.Patrullando;

        //if (swordHitbox != null)
            ///swordHitbox.SetOwner(this);

        GenerarNuevoDestino();
        animator.SetBool("isRunning", false);
    }

    void Update()
    {
        switch (estadoActual)
        {
            case Estado.Patrullando:
                Patrullando();
                break;

            case Estado.Persiguiendo:
                Persiguiendo();
                break;

            case Estado.Atacando:
                break;
        }
    }

    // ==========================
    // PATRULLANDO
    // ==========================
    void Patrullando()
    {
        animator.SetBool("isRunning", false);

        if (PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            return;
        }

        if (!agente.pathPending && agente.remainingDistance <= 0.3f)
        {
            GenerarNuevoDestino();
        }
    }

    // ==========================
    // PERSIGUIENDO
    // ==========================
    void Persiguiendo()
    {
        if (!PuedeVerLadron())
        {
            estadoActual = Estado.Patrullando;
            animator.SetBool("isRunning", false);
            GenerarNuevoDestino();
            return;
        }

        animator.SetBool("isRunning", true);
        agente.SetDestination(ladron.position);

        if (!agente.pathPending &&
            agente.remainingDistance <= agente.stoppingDistance &&
            !atacando)
        {
            estadoActual = Estado.Atacando;
            rutinaDeAtaque = StartCoroutine(RutinaAtaque());
        }
    }

    // ==========================
    // ATAQUE
    // ==========================
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

        if (PuedeVerLadron())
        {
            estadoActual = Estado.Persiguiendo;
            animator.SetBool("isRunning", true);
        }
        else
        {
            estadoActual = Estado.Patrullando;
            animator.SetBool("isRunning", false);
            GenerarNuevoDestino();
        }
    }

    // ==========================
    // GENERAR DESTINO ALEATORIO
    // ==========================
    void GenerarNuevoDestino()
    {
        for (int i = 0; i < intentosBusqueda; i++)
        {
            Vector2 direccion2D = Random.insideUnitCircle.normalized;
            Vector3 direccion = new Vector3(direccion2D.x, 0, direccion2D.y);

            float distancia = Random.Range(minPatrolDistance, maxPatrolDistance);

            Vector3 puntoTentativo = transform.position + direccion * distancia;

            if (NavMesh.SamplePosition(puntoTentativo, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                destinoActual = hit.position;
                agente.SetDestination(destinoActual);
                return;
            }
        }

        destinoActual = transform.position;
    }

    // ==========================
    // MIRAR AL LADRÓN
    // ==========================
    void MirarAlLadron()
    {
        if (!ladron) return;

        Vector3 dir = ladron.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // ==========================
    // VISIÓN
    // ==========================
    bool PuedeVerLadron()
    {
        if (!ladron || eyePoint == null) return false;

        Vector3 origin = eyePoint.position;
        Vector3 targetPoint = ladron.position + Vector3.up * torsoOffsetY;
        Vector3 toTarget = targetPoint - origin;

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
            return hit.transform == ladron || hit.transform.IsChildOf(ladron);
        }

        return false;
    }

    // ==========================
    // IMPACTO
    // ==========================
    public void OnSwordHitLadron()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}