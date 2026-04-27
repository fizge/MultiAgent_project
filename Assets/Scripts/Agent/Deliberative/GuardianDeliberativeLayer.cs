// FSM del protocolo FIPA Contract Net para guardias estáticos.
// Solo actúa como Iniciador: nunca puede ir al cofre porque no se mueve, así que rechaza
// cualquier REQUEST/CFP de otros Iniciadores. Su única función es lanzar conversaciones cuando
// expira su timer y propagar el resultado final a todos los participantes.
using System.Collections.Generic;
using UnityEngine;

public class GuardianDeliberativeLayer : DeliberativeLayer
{
    [SerializeField] private Transform cofre;
    public float timeoutFase = 3f;
    public string tareaCofre = "comprobarCofre";

    private SkeletonSocialLayer social;

    private enum Estado
    {
        IDLE,
        INIT_WAIT_RESPONSES,
        INIT_WAIT_PROPOSALS,
        INIT_WAIT_RESULT
    }

    private Estado estado = Estado.IDLE;
    private string convIdActual;
    private float deadlineFase;

    private List<AgenteFIPAACL> destinatariosRequest = new List<AgenteFIPAACL>();
    private List<AgenteFIPAACL> voluntarios = new List<AgenteFIPAACL>();
    private Dictionary<string, AgenteFIPAACL> proposalAgents = new Dictionary<string, AgenteFIPAACL>();
    private Dictionary<string, float> proposalCostes = new Dictionary<string, float>();
    private int respuestasEsperadas;
    private int respuestasRecibidas;
    private int propuestasEsperadas;
    private int propuestasRecibidas;

    void Awake()
    {
        social = GetComponent<SkeletonSocialLayer>();

        social.OnTimerExpired += OnTimerExpired;
        social.OnRequestReceived += OnRequestReceived;
        social.OnRequestResponseReceived += OnRequestResponseReceived;
        social.OnCFPReceived += OnCFPReceived;
        social.OnProposalReceived += OnProposalReceived;
        social.OnResultReceived += OnResultReceived;
        social.OnFailureReceived += OnFailureReceived;
        social.OnCancellationReceived += OnCancellationReceived;
    }

    void Update()
    {
        if (estado == Estado.IDLE) return;
        if (Time.time < deadlineFase) return;

        switch (estado)
        {
            case Estado.INIT_WAIT_RESPONSES:
                FinDeRecogidaDeRespuestas();
                break;
            case Estado.INIT_WAIT_PROPOSALS:
                FinDeRecogidaDePropuestas();
                break;
            case Estado.INIT_WAIT_RESULT:
                BroadcastResultadoFinalYReset(false, "timeout");
                break;
        }
    }

    // El guardián estático nunca propone movimiento desde la Deliberativa.
    public override ActionProposal GenerarPropuesta() => null;

    // -------------------- Iniciador --------------------

    private void OnTimerExpired()
    {
        if (estado != Estado.IDLE) return;
        if (cofre == null) return;

        destinatariosRequest.Clear();
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
        {
            if (a == social) continue;
            if (a is SkeletonSocialLayer) destinatariosRequest.Add(a);
        }

        if (destinatariosRequest.Count == 0)
        {
            social.ReiniciarTimer();
            return;
        }

        convIdActual = social.EnviarRequest(tareaCofre, destinatariosRequest);
        voluntarios.Clear();
        respuestasEsperadas = destinatariosRequest.Count;
        respuestasRecibidas = 0;
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.INIT_WAIT_RESPONSES;
    }

    private void OnRequestResponseReceived(string from, bool acepto, string convId)
    {
        if (estado != Estado.INIT_WAIT_RESPONSES || convId != convIdActual) return;
        respuestasRecibidas++;

        if (acepto)
        {
            AgenteFIPAACL voluntario = BuscarAgentePorId(from);
            if (voluntario != null) voluntarios.Add(voluntario);
        }

        if (respuestasRecibidas >= respuestasEsperadas) FinDeRecogidaDeRespuestas();
    }

    private void FinDeRecogidaDeRespuestas()
    {
        if (voluntarios.Count == 0)
        {
            FinalizarConversacionIniciador();
            social.ReiniciarTimer();
            return;
        }

        social.EnviarCFP(convIdActual, voluntarios, cofre.position, tareaCofre);
        proposalAgents.Clear();
        proposalCostes.Clear();
        propuestasEsperadas = voluntarios.Count;
        propuestasRecibidas = 0;
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.INIT_WAIT_PROPOSALS;
    }

    private void OnProposalReceived(string from, float coste, string convId)
    {
        if (estado != Estado.INIT_WAIT_PROPOSALS || convId != convIdActual) return;
        propuestasRecibidas++;

        AgenteFIPAACL agente = BuscarAgentePorId(from);
        if (agente != null)
        {
            proposalAgents[from] = agente;
            proposalCostes[from] = coste;
        }

        if (propuestasRecibidas >= propuestasEsperadas) FinDeRecogidaDePropuestas();
    }

    private void FinDeRecogidaDePropuestas()
    {
        if (proposalCostes.Count == 0)
        {
            FinalizarConversacionIniciador();
            social.ReiniciarTimer();
            return;
        }

        // Selecciona la propuesta de menor coste (distancia al cofre).
        string ganadorId = null;
        float mejorCoste = float.MaxValue;
        foreach (KeyValuePair<string, float> kv in proposalCostes)
        {
            if (kv.Value < mejorCoste)
            {
                mejorCoste = kv.Value;
                ganadorId = kv.Key;
            }
        }

        AgenteFIPAACL ganador = proposalAgents[ganadorId];
        social.AceptarPropuesta(convIdActual, ganador);
        foreach (KeyValuePair<string, AgenteFIPAACL> kv in proposalAgents)
            if (kv.Key != ganadorId) social.RechazarPropuesta(convIdActual, kv.Value);

        deadlineFase = Time.time + timeoutFase * 4f; // la ejecución del cofre puede ser larga
        estado = Estado.INIT_WAIT_RESULT;
    }

    private void OnFailureReceived(string convId)
    {
        if (estado != Estado.INIT_WAIT_RESULT || convId != convIdActual) return;
        BroadcastResultadoFinalYReset(false, "failure");
    }

    // -------------------- Como receptor de REQUEST/CFP de otro Iniciador --------------------

    // El guardián estático nunca puede ir al cofre, así que siempre responde REFUSE.
    private void OnRequestReceived(string from, string tarea, string convId)
    {
        AgenteFIPAACL iniciador = BuscarAgentePorId(from);
        if (iniciador != null) social.ResponderRequest(convId, false, iniciador);
    }

    // No debería ocurrir (no respondemos AGREE, así que no nos llega un CFP), pero por defensa rechazamos.
    private void OnCFPReceived(string from, string tarea, Vector3 refPos, string convId)
    {
        AgenteFIPAACL iniciador = BuscarAgentePorId(from);
        if (iniciador != null) social.Rechazar(convId, iniciador);
    }

    // -------------------- Resultados y alerta --------------------

    private void OnResultReceived(string from, bool exito, object datos, string convId)
    {
        // Caso 1: soy el Iniciador y el ganador me reporta su resultado.
        if (estado == Estado.INIT_WAIT_RESULT && convId == convIdActual)
        {
            BroadcastResultadoFinalYReset(exito, datos);
            return;
        }

        // Caso 2: he recibido la difusión final. Si el cofre fue robado, alerta.
        if (!exito)
            Debug.Log($"[{name}] ALERTA: cofre robado (conv={convId}).");
    }

    private void OnCancellationReceived(string convId)
    {
        // Sin estado de Participante, no hay nada que cancelar localmente.
    }

    private void BroadcastResultadoFinalYReset(bool exito, object datos)
    {
        List<AgenteFIPAACL> todos = new List<AgenteFIPAACL>(destinatariosRequest);
        todos.Add(social); // el propio agente también recibe la difusión para reiniciar su timer
        social.InformarResultado(convIdActual, exito, datos, todos);
        FinalizarConversacionIniciador();
    }

    private void FinalizarConversacionIniciador()
    {
        estado = Estado.IDLE;
        convIdActual = null;
        destinatariosRequest.Clear();
        voluntarios.Clear();
        proposalAgents.Clear();
        proposalCostes.Clear();
    }

    private static AgenteFIPAACL BuscarAgentePorId(string id)
    {
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.IdAgente == id) return a;
        return null;
    }
}
