using UnityEngine;
using UnityEngine.AI;

public class AgentMove : MonoBehaviour
{
    public GameObject objetivo;
    NavMeshAgent agente;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (objetivo == null) return;
        agente.SetDestination(objetivo.transform.position);
    }
}
