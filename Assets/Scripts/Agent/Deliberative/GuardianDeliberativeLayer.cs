// Capa Deliberativa del guardia estático.
// Solo actúa como Iniciador del protocolo Contract Net — nunca como Participante,
// ya que el guardián no puede moverse y no tiene sentido que ejecute tareas de desplazamiento.
// Gestiona dos tareas:
//   - "comprobarCofre": lanzada por timer aleatorio para verificar que el cofre sigue intacto.
//   - "ladronAvistado": lanzada cuando el sensor detecta al ladrón, para que un patrullero investigue la entrada.
using System.Collections.Generic;
using UnityEngine;

public class GuardianDeliberativeLayer : DeliberativeLayer
{
    [SerializeField] private Transform cofre;        // Referencia al cofre; necesaria para el CFP de comprobarCofre
    [SerializeField] private Transform puntoEntrada; // Punto dentro del NavMesh cerca de la puerta; usado como destino en ladronAvistado
    public float timeoutFase = 3f;                   // Segundos máximos de espera por fase del protocolo
    public string tareaCofre = "comprobarCofre";     // Identificador de la tarea de comprobación periódica
    private const string tareaLadron = "ladronAvistado"; // Identificador de la tarea de alerta de intrusión

    private SkeletonSocialLayer social;  // Capa social: traduce entre la FSM y los mensajes ACL
    private VisionSensor visionSensor;   // Sensor de visión: dispara OnLadronAvistado cuando ve al ladrón
    private string tareaActual;          // Tarea en curso; determina qué posición usar en el CFP

    // Estados de la FSM del Iniciador:
    // IDLE → INIT_WAIT_RESPONSES → INIT_WAIT_PROPOSALS → INIT_WAIT_RESULT → IDLE
    private enum Estado
    {
        IDLE,
        INIT_WAIT_RESPONSES,  // REQUEST enviado; esperando AGREE/REFUSE de los patrulleros
        INIT_WAIT_PROPOSALS,  // CFP enviado a voluntarios; esperando PROPOSE con costes
        INIT_WAIT_RESULT      // ACCEPT_PROPOSAL enviado al ganador; esperando INFORM_RESULT
    }

    private Estado estado = Estado.IDLE;
    private string convIdActual;  // GUID compartido por todos los mensajes de la misma conversación
    private float deadlineFase;   // Tiempo absoluto (Time.time) en que expira la fase actual

    // Flag que evita relanzar el Contract Net de comprobarCofre una vez confirmado el robo.
    private bool cofreRobadoConfirmado;

    // Listas para acumular respuestas a lo largo del protocolo
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

        // Suscripción al sensor de visión para detectar al ladrón
        if (visionSensor != null) visionSensor.OnLadronAvistado += OnLadronAvistado;

        // Suscripción a los eventos de la capa social
        social.OnTimerExpired += OnTimerExpired;
        social.OnRequestReceived += OnRequestReceived;
        social.OnRequestResponseReceived += OnRequestResponseReceived;
        social.OnCFPReceived += OnCFPReceived;
        social.OnProposalReceived += OnProposalReceived;
        social.OnResultReceived += OnResultReceived;
        social.OnFailureReceived += OnFailureReceived;
        social.OnCancellationReceived += OnCancellationReceived;
    }

    // Comprueba en cada frame si ha expirado el deadline de la fase actual.
    void Update()
    {
        if (estado == Estado.IDLE) return;
        if (Time.time < deadlineFase) return;

        switch (estado)
        {
            case Estado.INIT_WAIT_RESPONSES:
                // Deadline de REQUEST: procesamos las respuestas recibidas hasta ahora
                FinDeRecogidaDeRespuestas();
                break;
            case Estado.INIT_WAIT_PROPOSALS:
                // Deadline de CFP: procesamos las propuestas recibidas hasta ahora
                FinDeRecogidaDePropuestas();
                break;
            case Estado.INIT_WAIT_RESULT:
                // El ganador no respondió a tiempo: cerramos la conversación y reiniciamos timer
                FinalizarConversacionIniciador();
                social.ReiniciarTimer();
                break;
        }
    }

    // El guardián estático nunca propone movimiento — GenerarPropuesta siempre devuelve null.
    public override ActionProposal GenerarPropuesta() => null;

    // -------------------- Iniciador --------------------

    // Disparado por VisionSensor cuando el ladrón entra en el campo de visión.
    // Lanza el protocolo ladronAvistado para que el patrullero más cercano al puntoEntrada investigue.
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

    // Centraliza el lanzamiento de cualquier protocolo Contract Net como Iniciador.
    // Recoge todos los esqueletos del registro global, envía REQUEST y espera respuestas.
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

    // Disparado por SkeletonSocialLayer cuando expira el timer aleatorio.
    // Lanza el protocolo comprobarCofre si el cofre no ha sido robado previamente.
    private void OnTimerExpired()
    {
        if (estado != Estado.IDLE) return;
        if (cofre == null) return;
        if (cofreRobadoConfirmado) return; // ya se sabe que el cofre fue robado, no tiene sentido comprobarlo

        LanzarProtocolo(tareaCofre);
    }

    // Recibe cada AGREE o REFUSE al REQUEST. Acumula voluntarios y avanza cuando llegan todas las respuestas.
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

    // Fase 1: envía CFP a los voluntarios.
    // La posición de referencia depende de la tarea: puntoEntrada para ladronAvistado, cofre para comprobarCofre.
    // Si nadie aceptó, cancela el protocolo y reinicia el timer.
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

    // Recibe cada PROPOSE con el coste (distancia al punto de referencia) del candidato.
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

    // Selecciona al candidato con menor coste, le envía ACCEPT_PROPOSAL y rechaza al resto.
    // Si no llegó ninguna propuesta, cancela el protocolo.
    private void FinDeRecogidaDePropuestas()
    {
        if (proposalCostes.Count == 0)
        {
            FinalizarConversacionIniciador();
            social.ReiniciarTimer();
            return;
        }

        // Selección del ganador: menor distancia al punto de referencia
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

        deadlineFase = Time.time + timeoutFase * 4f; // timeout generoso: el ganador debe desplazarse físicamente
        estado = Estado.INIT_WAIT_RESULT;
    }

    // El ganador no pudo completar la tarea: cerramos la conversación y reiniciamos el timer.
    private void OnFailureReceived(string convId)
    {
        if (estado != Estado.INIT_WAIT_RESULT || convId != convIdActual) return;
        FinalizarConversacionIniciador();
        social.ReiniciarTimer();
    }

    // -------------------- Como receptor de REQUEST/CFP de otro Iniciador --------------------

    // El guardián nunca puede moverse, así que siempre responde REFUSE a cualquier REQUEST entrante.
    private void OnRequestReceived(string from, string tarea, string convId)
    {
        AgenteFIPAACL iniciador = BuscarAgentePorId(from);
        if (iniciador != null) social.ResponderRequest(convId, false, iniciador);
    }

    // No debería ocurrir (no respondemos AGREE, así que no nos debería llegar un CFP), pero rechazamos por defensa.
    private void OnCFPReceived(string from, string tarea, Vector3 refPos, string convId)
    {
        AgenteFIPAACL iniciador = BuscarAgentePorId(from);
        if (iniciador != null) social.Rechazar(convId, iniciador);
    }

    // -------------------- Resultado de la conversación --------------------

    // Recibe INFORM_RESULT del patrullero ganador cuando completa la tarea.
    // Solo cierra la FSM: el ganador ya ha hecho el broadcast INFORM a todos los patrulleros.
    private void OnResultReceived(string from, bool exito, object datos, string convId)
    {
        if (estado == Estado.INIT_WAIT_RESULT && convId == convIdActual)
        {
            if (!exito) cofreRobadoConfirmado = true; // el cofre fue robado: no tiene sentido seguir comprobando
            FinalizarConversacionIniciador();
        }
    }

    // El guardián no tiene estado de Participante, así que no hay nada que cancelar localmente.
    private void OnCancellationReceived(string convId) { }

    // Resetea la FSM al estado inicial y limpia todas las listas de la conversación.
    private void FinalizarConversacionIniciador()
    {
        estado = Estado.IDLE;
        convIdActual = null;
        destinatariosRequest.Clear();
        voluntarios.Clear();
        proposalAgents.Clear();
        proposalCostes.Clear();
    }

    // Busca un agente en el registro global por su identificador de nombre.
    private static AgenteFIPAACL BuscarAgentePorId(string id)
    {
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.IdAgente == id) return a;
        return null;
    }
}
