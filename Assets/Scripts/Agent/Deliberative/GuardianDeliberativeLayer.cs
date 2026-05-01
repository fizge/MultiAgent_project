// FSM del protocolo FIPA Contract Net para guardias estáticos.
// Solo actúa como Iniciador: nunca puede ir al cofre porque no se mueve, así que rechaza
// cualquier REQUEST/CFP de otros Iniciadores. Su única función es lanzar conversaciones cuando
// expira su timer y propagar el resultado final a todos los participantes.
using System.Collections.Generic;
using UnityEngine;

public class GuardianDeliberativeLayer : DeliberativeLayer
{
    [SerializeField] private Transform cofre;
    [SerializeField] private Transform puntoEntrada;
    public float timeoutFase = 3f;
    public string tareaCofre = "comprobarCofre";
    private const string tareaLadron = "ladronAvistado";

    private SkeletonSocialLayer social;
    private VisionSensor visionSensor;
    private string tareaActual;

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

    // Flag que evita relanzar el Contract Net una vez confirmado que el cofre fue robado.
    private bool cofreRobadoConfirmado;

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
        visionSensor = GetComponent<VisionSensor>();
        if (visionSensor != null) visionSensor.OnLadronAvistado += OnLadronAvistado;

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
                // El ganador no respondió a tiempo: cerramos la conversación y reiniciamos timer.
                FinalizarConversacionIniciador();
                social.ReiniciarTimer();
                break;
        }
    }

    // El guardián estático nunca propone movimiento desde la Deliberativa.
    public override ActionProposal GenerarPropuesta() => null;

    // -------------------- Iniciador --------------------

    private void OnLadronAvistado()
    {
        if (estado != Estado.IDLE) return;
        if (puntoEntrada == null)
        {
            Debug.LogWarning($"[{name}] Ladrón avistado pero puntoEntrada no asignado — protocolo cancelado.");
            return;
        }
        Debug.Log($"[{name}] Ladrón avistado — iniciando ContractNet '{tareaLadron}'.");
        LanzarProtocolo(tareaLadron);
    }

private void LanzarProtocolo(string tarea)
    {
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

        tareaActual = tarea;
        convIdActual = social.EnviarRequest(tarea, destinatariosRequest);
        voluntarios.Clear();
        respuestasEsperadas = destinatariosRequest.Count;
        respuestasRecibidas = 0;
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.INIT_WAIT_RESPONSES;
    }

    private void OnTimerExpired()
    {
        if (estado != Estado.IDLE) return;
        if (cofre == null) return;
        if (cofreRobadoConfirmado) return;

        LanzarProtocolo(tareaCofre);
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

        Vector3 posicionCFP = (tareaActual == tareaLadron) ? puntoEntrada.position : cofre.position;
        social.EnviarCFP(convIdActual, voluntarios, posicionCFP, tareaActual);
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
        FinalizarConversacionIniciador();
        social.ReiniciarTimer();
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
        // Soy el Iniciador: recibo INFORM_RESULT del ganador. Solo cierro la FSM.
        // El ganador ya ha hecho el broadcast INFORM a todos los patrulleros.
        if (estado == Estado.INIT_WAIT_RESULT && convId == convIdActual)
        {
            if (!exito) cofreRobadoConfirmado = true; // evita relanzar el Contract Net en el futuro
            FinalizarConversacionIniciador();
        }
    }

    private void OnCancellationReceived(string convId)
    {
        // Sin estado de Participante, no hay nada que cancelar localmente.
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
