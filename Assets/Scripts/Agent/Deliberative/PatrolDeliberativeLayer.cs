// FSM del protocolo FIPA Contract Net para guardias patrulleros.
// Puede actuar como Iniciador (cuando expira su timer) y como Participante (cuando recibe REQUEST/CFP).
// No conoce el lenguaje ACL: solo llama métodos de SkeletonSocialLayer y reacciona a sus eventos.
using System.Collections.Generic;
using UnityEngine;

public class PatrolDeliberativeLayer : DeliberativeLayer
{
    [SerializeField] private Transform cofre;
    public float velocidadComprobacion = 2f;
    public float aceleracionComprobacion = 2f;
    public float toleranciaCofre = 2f;
    public float timeoutFase = 3f; // segundos máximos de espera por fase del protocolo
    public string tareaCofre = "comprobarCofre";

    private SkeletonSocialLayer social;
    private CheckChestBehaviour checkChest;

    private enum Estado
    {
        IDLE,
        INIT_WAIT_RESPONSES,   // iniciador esperando AGREE/REFUSE al REQUEST
        INIT_WAIT_PROPOSALS,   // iniciador esperando PROPOSE al CFP
        INIT_WAIT_RESULT,      // iniciador esperando INFORM_RESULT del ganador
        PART_VOLUNTEERED,      // participante: respondió AGREE, esperando CFP
        PART_PROPOSED,         // participante: envió PROPOSE, esperando ACCEPT/REJECT
        PART_EXECUTING         // participante: ganó, ejecutando CheckChestBehaviour
    }

    private Estado estado = Estado.IDLE;
    private string convIdActual;
    private float deadlineFase;

    // Estado del Iniciador
    private List<AgenteFIPAACL> destinatariosRequest = new List<AgenteFIPAACL>();
    private List<AgenteFIPAACL> voluntarios = new List<AgenteFIPAACL>();
    private Dictionary<string, AgenteFIPAACL> proposalAgents = new Dictionary<string, AgenteFIPAACL>();
    private Dictionary<string, float> proposalCostes = new Dictionary<string, float>();
    private int respuestasEsperadas;
    private int respuestasRecibidas;
    private int propuestasEsperadas;
    private int propuestasRecibidas;

    // Estado del Participante
    private AgenteFIPAACL iniciadorActual;

    // Flag que evita relanzar el Contract Net una vez confirmado que el cofre fue robado.
    private bool cofreRobadoConfirmado;

    void Awake()
    {
        social = GetComponent<SkeletonSocialLayer>();
        MovementSensor mov = GetComponent<MovementSensor>();
        VisionSensor vision = GetComponent<VisionSensor>();

        checkChest = new CheckChestBehaviour
        {
            speed = velocidadComprobacion,
            acceleration = aceleracionComprobacion,
            toleranciaCofre = toleranciaCofre
        };
        checkChest.Initialize(transform, cofre, mov, vision);
        checkChest.OnComprobacionCompletada += OnCheckChestCompletado;

        social.OnTimerExpired += OnTimerExpired;
        social.OnRequestReceived += OnRequestReceived;
        social.OnRequestResponseReceived += OnRequestResponseReceived;
        social.OnCFPReceived += OnCFPReceived;
        social.OnProposalReceived += OnProposalReceived;
        social.OnProposalAccepted += OnProposalAccepted;
        social.OnProposalRejected += OnProposalRejected;
        social.OnResultReceived += OnResultReceived;
        social.OnInformReceived += OnInformReceived;
        social.OnFailureReceived += OnFailureReceived;
        social.OnCancellationReceived += OnCancellationReceived;
    }

    void Update()
    {
        // Solo los estados de espera tienen deadline. PART_EXECUTING e IDLE no caducan.
        if (estado == Estado.IDLE || estado == Estado.PART_EXECUTING) return;
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
            case Estado.PART_VOLUNTEERED:
            case Estado.PART_PROPOSED:
                // El iniciador no contestó: abortamos localmente y reiniciamos timer.
                ResetParticipante();
                social.ReiniciarTimer();
                break;
        }
    }

    public override ActionProposal GenerarPropuesta()
    {
        if (estado == Estado.PART_EXECUTING) return checkChest.Evaluate();
        return null;
    }

    // -------------------- Iniciador --------------------

    private void OnTimerExpired()
    {
        if (estado != Estado.IDLE) return;
        if (cofre == null) return;
        if (cofreRobadoConfirmado) return; // ya se sabe que el cofre fue robado, no tiene sentido comprobarlo de nuevo

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
            // Nadie disponible: abortamos y reiniciamos timer.
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
            // Ningún PROPOSE: abortamos y reiniciamos timer.
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

    // -------------------- Participante --------------------

    private void OnRequestReceived(string from, string tarea, string convId)
    {
        AgenteFIPAACL iniciador = BuscarAgentePorId(from);
        if (iniciador == null) return;

        // Solo aceptamos si estamos libres y la tarea es la del cofre.
        bool puedoAceptar = (estado == Estado.IDLE) && tarea == tareaCofre;
        social.ResponderRequest(convId, puedoAceptar, iniciador);

        if (puedoAceptar)
        {
            iniciadorActual = iniciador;
            convIdActual = convId;
            deadlineFase = Time.time + timeoutFase;
            estado = Estado.PART_VOLUNTEERED;
        }
    }

    private void OnCFPReceived(string from, string tarea, Vector3 refPos, string convId)
    {
        if (estado != Estado.PART_VOLUNTEERED || convId != convIdActual)
        {
            // CFP fuera de contexto: rechazamos.
            AgenteFIPAACL emisor = BuscarAgentePorId(from);
            if (emisor != null) social.Rechazar(convId, emisor);
            return;
        }

        float coste = Vector3.Distance(transform.position, refPos);
        social.EnviarPropuesta(convId, coste, iniciadorActual);
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.PART_PROPOSED;
    }

    private void OnProposalAccepted(string convId)
    {
        if (estado != Estado.PART_PROPOSED || convId != convIdActual) return;
        estado = Estado.PART_EXECUTING;
        checkChest.Activar();
    }

    private void OnProposalRejected(string convId)
    {
        if (convId != convIdActual) return;
        if (estado == Estado.PART_PROPOSED || estado == Estado.PART_VOLUNTEERED)
            ResetParticipante();
    }

    private void OnCancellationReceived(string convId)
    {
        if (convId != convIdActual) return;
        if (estado == Estado.PART_EXECUTING) checkChest.Desactivar();
        ResetParticipante();
    }

    // Llamado por CheckChestBehaviour cuando el patrullero ganador llega al cofre.
    private void OnCheckChestCompletado(bool cofreIntacto)
    {
        if (estado != Estado.PART_EXECUTING) return;

        // Dentro del Contract Net: INFORM_RESULT solo al Iniciador.
        if (iniciadorActual != null)
            social.InformarResultado(convIdActual, cofreIntacto, cofreIntacto ? "cofre intacto" : "cofre robado",
                                      new List<AgenteFIPAACL> { iniciadorActual });

        // Fuera del Contract Net: INFORM a todos los patrulleros (filtra cámaras y guardianes en BroadcastInform).
        social.BroadcastInform(convIdActual, cofreIntacto, cofreIntacto ? "cofre intacto" : "cofre robado");

        ResetParticipante();
        social.ReiniciarTimer(); // el ganador reinicia su timer al terminar la tarea
    }

    // -------------------- Resultados y reacción a la alerta --------------------

    private void OnResultReceived(string from, bool exito, object datos, string convId)
    {
        // Soy el Iniciador: recibo INFORM_RESULT del ganador. Solo cierro la FSM.
        // El ganador ya ha hecho el broadcast INFORM a todos los demás.
        if (estado == Estado.INIT_WAIT_RESULT && convId == convIdActual)
            FinalizarConversacionIniciador();
    }

    // Notificación externa al Contract Net: llega INFORM del ganador a todos los patrulleros.
    private void OnInformReceived(string from, bool exito, object datos, string convId)
    {
        if (!exito)
        {
            cofreRobadoConfirmado = true; // evita relanzar el Contract Net en el futuro
            Debug.Log($"[{name}] ALERTA: cofre robado (informado por {from}, conv={convId}).");
        }
    }

    // -------------------- Helpers de transición --------------------

    private void FinalizarConversacionIniciador()
    {
        estado = Estado.IDLE;
        convIdActual = null;
        destinatariosRequest.Clear();
        voluntarios.Clear();
        proposalAgents.Clear();
        proposalCostes.Clear();
    }

    private void ResetParticipante()
    {
        estado = Estado.IDLE;
        convIdActual = null;
        iniciadorActual = null;
    }

    private static AgenteFIPAACL BuscarAgentePorId(string id)
    {
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.IdAgente == id) return a;
        return null;
    }
}
