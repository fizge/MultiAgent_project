using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Representa el radio de ruido que emite un personaje según su velocidad.
// Lo llevan tanto el ladrón como los guardias. El HearingSensor lo consulta.
public class NoiseEmitter : MonoBehaviour
{
    public float radioAndando   = 2f; // radio cuando se mueve despacio
    public float radioCorriendo = 3f; // radio cuando corre
    public float umbralCorrer   = 3.5f; // velocidad a partir de la cual se considera "correr"

    // Radio activo este frame. Visible en Inspector para debug.
    public float radioActual { get; private set; }

    // Registro estático para que el HearingSensor no tenga que buscar cada frame.
    public static List<NoiseEmitter> todos = new List<NoiseEmitter>();

    private CharacterController characterController;
    private NavMeshAgent navMeshAgent;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        navMeshAgent        = GetComponent<NavMeshAgent>();
        todos.Add(this);
    }

    void OnDestroy() { todos.Remove(this); }

    void Update()
    {
        float velocidad = ObtenerVelocidadHorizontal();

        if      (velocidad >= umbralCorrer) radioActual = radioCorriendo;
        else if (velocidad > 0.1f)          radioActual = radioAndando;
        else                                radioActual = 0f;
    }

    // Funciona tanto con CharacterController (ladrón) como con NavMeshAgent (guardias).
    float ObtenerVelocidadHorizontal()
    {
        Vector3 vel = Vector3.zero;

        if (characterController != null)
        {
            vel   = characterController.velocity;
            vel.y = 0f;
        }
        else if (navMeshAgent != null)
        {
            vel   = navMeshAgent.velocity;
            vel.y = 0f;
        }

        return vel.magnitude;
    }

    // Dibuja los radios en la Scene View cuando el objeto está seleccionado.
    void OnDrawGizmosSelected()
    {
        // Radio andando — amarillo
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioAndando);

        // Radio corriendo — rojo
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radioCorriendo);
    }
}
