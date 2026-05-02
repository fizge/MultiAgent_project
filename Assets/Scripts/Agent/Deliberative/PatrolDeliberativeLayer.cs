// Capa Deliberativa del guardia patrullero.
// Puede actuar como Iniciador (cuando expira su timer o detecta un evento) y como Participante
// (cuando recibe un REQUEST de otro Iniciador). No conoce el lenguaje ACL: solo llama métodos
// de SkeletonSocialLayer y reacciona a sus eventos.
//
// Tareas gestionadas:
//   - "comprobarCofre"       : iniciada por timer; el más cercano al cofre comprueba su estado.
//   - "bloquearSalida"       : iniciada al detectar el robo del cofre; el más cercano a la salida la custodia.
//   - "investigarAvistamiento": participante; responde a la cámara cuando avista al ladrón.
//   - "ladronAvistado"       : participante; responde al guardián estático cuando avista al ladrón.
//   - "busquedaCoordinada"   : coordinación emergente via INFORM (sin Contract Net); cada patrullero
//                              va a un sector distinto alrededor del último punto conocido del ladrón.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PatrolDeliberativeLayer : DeliberativeLayer
{
    [SerializeField] private Transform cofre;      // Referencia al cofre; destino del CFP en comprobarCofre
    [SerializeField] private Transform zonaSalida; // Referencia a la zona de salida; destino del CFP en bloquearSalida
    public float velocidadComprobacion = 4f;       // Velocidad usada por CheckChestBehaviour y GoToPositionBehaviour
    public float aceleracionComprobacion = 2f;
    public float toleranciaCofre = 2f;             // Radio de llegada válida al cofre (filtra llegadas espurias)
    public float timeoutFase = 3f;                 // Segundos máximos de espera por fase del protocolo
    public float radioBusqueda = 8f;               // Radio desde el último punto conocido para calcular destinos de búsqueda

    public string tareaCofre = "comprobarCofre";
    private const string tareaAvistamiento = "investigarAvistamiento";
    private const string tareaLadron = "ladronAvistado";
    private const string tareaBloqueo = "bloquearSalida";
    private const string tareaBusqueda = "busquedaCoordinada";

    private SkeletonSocialLayer social;      // Capa social: traduce entre FSM y mensajes ACL
    private VisionSensor visionSensor;       // Sensor de visión: dispara eventos de cofre y ladrón
    private CheckChestBehaviour checkChest;  // Behaviour activo cuando el agente gana el contrato comprobarCofre
    private GoToPositionBehaviour goToPos;   // Behaviour activo para tareas de desplazamiento a posición arbitraria
    private PatrolBehaviour patrolBehaviour; // Referencia al behaviour de patrulla para activar modo alerta

    // Tarea que está ejecutando actualmente el agente (como Participante o en búsqueda coordinada).
    // Determina qué behaviour activar y qué posición usar.
    private string tareaActual;
    private Vector3 posicionInvestigacion; // Posición recibida en el CFP; destino del GoToPositionBehaviour

    // Estados de la FSM:
    // Como Iniciador:    IDLE → INIT_WAIT_RESPONSES → INIT_WAIT_PROPOSALS → INIT_WAIT_RESULT → IDLE
    // Como Participante: IDLE → PART_VOLUNTEERED → PART_PROPOSED → PART_EXECUTING → IDLE
    private enum Estado
    {
        IDLE,
        INIT_WAIT_RESPONSES,   // Iniciador: REQUEST enviado, esperando AGREE/REFUSE
        INIT_WAIT_PROPOSALS,   // Iniciador: CFP enviado, esperando PROPOSE
        INIT_WAIT_RESULT,      // Iniciador: ACCEPT_PROPOSAL enviado, esperando INFORM_RESULT del ganador
        PART_VOLUNTEERED,      // Participante: respondió AGREE, esperando CFP
        PART_PROPOSED,         // Participante: envió PROPOSE, esperando ACCEPT/REJECT
        PART_EXECUTING         // Participante: ganó, ejecutando CheckChestBehaviour o GoToPositionBehaviour según la tarea
    }

    private Estado estado = Estado.IDLE;
    private string convIdActual;  // GUID compartido por todos los mensajes de la misma conversación
    private float deadlineFase;   // Tiempo absoluto (Time.time) en que expira la fase actual

    // Listas del Iniciador para acumular respuestas a lo largo del protocolo
    private List<AgenteFIPAACL> destinatariosRequest = new List<AgenteFIPAACL>();
    private List<AgenteFIPAACL> voluntarios = new List<AgenteFIPAACL>();
    private Dictionary<string, AgenteFIPAACL> proposalAgents = new Dictionary<string, AgenteFIPAACL>();
    private Dictionary<string, float> proposalCostes = new Dictionary<string, float>();
    private int respuestasEsperadas;
    private int respuestasRecibidas;
    private int propuestasEsperadas;
    private int propuestasRecibidas;

    // Referencia al iniciador de la conversación actual (solo cuando este agente es Participante)
    private AgenteFIPAACL iniciadorActual;

    // Flag que evita relanzar el Contract Net de comprobarCofre o bloquearSalida una vez confirmado el robo.
    private bool cofreRobadoConfirmado;


    // Se ejecuta después de todos los Awake: garantiza que PatrolReactiveLayer ya inicializó patrol.
    void Start()
    {
        patrolBehaviour = GetComponent<PatrolReactiveLayer>().Patrol;
    }

    void Awake()
    {
        social = GetComponent<SkeletonSocialLayer>();
        visionSensor = GetComponent<VisionSensor>();

        // Suscripción a eventos del sensor de visión
        visionSensor.OnCofresDesaparecido += OnCofresDesaparecido;
        visionSensor.OnLadronPerdido += OnLadronPerdido;

        MovementSensor mov = GetComponent<MovementSensor>();

        // CheckChestBehaviour: va al cofre y comprueba si fue robado
        checkChest = new CheckChestBehaviour
        {
            speed = velocidadComprobacion,
            acceleration = aceleracionComprobacion,
            toleranciaCofre = toleranciaCofre
        };
        checkChest.Initialize(transform, cofre, mov, visionSensor);
        checkChest.OnComprobacionCompletada += OnCheckChestCompletado;

        // GoToPositionBehaviour: se dirige a una posición arbitraria (avistamiento, entrada, salida, sector)
        goToPos = new GoToPositionBehaviour
        {
            speed = velocidadComprobacion,
            acceleration = aceleracionComprobacion
        };
        goToPos.Initialize(transform, mov);
        goToPos.OnLlegadaCompletada += OnInvestigacionCompletada;

        // Suscripción a los eventos de la capa social
        social.OnTimerExpired += OnTimerExpired;
        social.OnRequestReceived += OnRequestReceived;
        social.OnRequestResponseReceived += OnRequestResponseReceived;
        social.OnCFPReceived += OnCFPReceived;
        social.OnProposalReceived += OnProposalReceived;
        social.OnProposalAccepted += OnProposalAccepted;
        social.OnBusquedaCoordinadaReceived += OnBusquedaCoordinadaReceived;
        social.OnProposalRejected += OnProposalRejected;
        social.OnResultReceived += OnResultReceived;
        social.OnInformReceived += OnInformReceived;
        social.OnFailureReceived += OnFailureReceived;
        social.OnCancellationReceived += OnCancellationReceived;
    }

    // Comprueba en cada frame si ha expirado el deadline de la fase actual.
    // IDLE y PART_EXECUTING no tienen deadline: no caducan.
    void Update()
    {
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
                // El ganador no respondió a tiempo: cerramos la conversación y reiniciamos timer
                FinalizarConversacionIniciador();
                social.ReiniciarTimer();
                break;
            case Estado.PART_VOLUNTEERED:
            case Estado.PART_PROPOSED:
                // El iniciador no contestó: abortamos localmente y reiniciamos timer
                ResetParticipante();
                social.ReiniciarTimer();
                break;
        }
    }

    // Devuelve ActionProposal solo cuando el agente está ejecutando una tarea como Participante.
    // El behaviour concreto depende de la tarea activa.
    public override ActionProposal GenerarPropuesta()
    {
        if (estado != Estado.PART_EXECUTING) return null;
        if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron || tareaActual == tareaBloqueo || tareaActual == tareaBusqueda)
            return goToPos.Evaluate();
        return checkChest.Evaluate();
    }

    // -------------------- Iniciador --------------------

    // Disparado por SkeletonSocialLayer cuando expira el timer aleatorio.
    // Lanza el protocolo comprobarCofre si el cofre existe y no ha sido robado previamente.
    private void OnTimerExpired()
    {
        if (estado != Estado.IDLE) return;
        if (cofre == null) return;
        if (cofreRobadoConfirmado) return;
        LanzarProtocoloIniciador(tareaCofre);
    }

    // Disparado por VisionSensor cuando el cofre desaparece del campo de visión del agente.
    // Lanza el protocolo bloquearSalida para que el patrullero más cercano a la salida la custodie.
    private void OnCofresDesaparecido()
    {
        if (estado != Estado.IDLE) return;
        if (cofreRobadoConfirmado) return;
        if (zonaSalida == null)
        {
            Debug.LogWarning($"[{name}] Cofre robado pero zonaSalida no asignada — protocolo cancelado.");
            return;
        }
        Debug.Log($"[{name}] Cofre robado — iniciando ContractNet '{tareaBloqueo}'.");
        LanzarProtocoloIniciador(tareaBloqueo);
    }

    // Disparado por VisionSensor cuando el ladrón se pierde de vista.
    // Emite INFORM de búsqueda coordinada a todos los patrulleros y va a su propio sector.
    private void OnLadronPerdido()
    {
        if (estado != Estado.IDLE) return;
        Debug.Log($"[{name}] Ladrón perdido — iniciando búsqueda coordinada.");
        social.InformarBusqueda(visionSensor.PosicionLadron);
        IrASector(visionSensor.PosicionLadron);
    }

    // Disparado por SkeletonSocialLayer al recibir un INFORM de búsqueda coordinada de otro patrullero.
    // El agente va a su sector correspondiente alrededor del último punto conocido.
    private void OnBusquedaCoordinadaReceived(Vector3 ultimaPosicion)
    {
        if (estado != Estado.IDLE) return;
        IrASector(ultimaPosicion);
    }

    // Calcula el sector de búsqueda propio usando el índice del agente en la lista de patrulleros.
    // Divide 360° entre el número total de patrulleros y asigna un ángulo a cada uno.
    // El destino se corrige con NavMesh.SamplePosition para garantizar que está dentro del NavMesh.
    private void IrASector(Vector3 posicionReferencia)
    {
        List<AgenteFIPAACL> patrulleros = new List<AgenteFIPAACL>();
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.GetComponent<PatrolDeliberativeLayer>() != null) patrulleros.Add(a);

        int miIndice = patrulleros.IndexOf(social);
        if (miIndice < 0) miIndice = 0;
        float angulo = miIndice * (360f / Mathf.Max(patrulleros.Count, 1));
        Vector3 direccion = Quaternion.Euler(0, angulo, 0) * Vector3.forward;
        Vector3 candidato = posicionReferencia + direccion * radioBusqueda;

        // Corrige el destino al punto válido más cercano del NavMesh
        NavMeshHit hit;
        Vector3 destino = NavMesh.SamplePosition(candidato, out hit, radioBusqueda, NavMesh.AllAreas)
            ? hit.position
            : transform.position; // si no hay punto válido, el agente se queda donde está

        tareaActual = tareaBusqueda;
        estado = Estado.PART_EXECUTING;
        goToPos.Activar(destino);
        Debug.Log($"[{name}] Sector {angulo:F0}° → {destino}");
    }

    // Centraliza el lanzamiento de cualquier protocolo Contract Net como Iniciador.
    // Envía REQUEST a todos los esqueletos registrados y pasa a INIT_WAIT_RESPONSES.
    private void LanzarProtocoloIniciador(string tarea)
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

        convIdActual = social.EnviarRequest(tarea, destinatariosRequest);
        voluntarios.Clear();
        respuestasEsperadas = destinatariosRequest.Count;
        respuestasRecibidas = 0;
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.INIT_WAIT_RESPONSES;
        tareaActual = tarea;
    }

    // Recibe cada AGREE o REFUSE. Acumula voluntarios y avanza cuando llegan todas las respuestas.
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
    // La posición de referencia depende de la tarea: zonaSalida para bloquearSalida, cofre para el resto.
    // Si nadie aceptó, cancela el protocolo y reinicia el timer.
    private void FinDeRecogidaDeRespuestas()
    {
        if (voluntarios.Count == 0)
        {
            FinalizarConversacionIniciador();
            social.ReiniciarTimer();
            return;
        }

        Vector3 posicionCFP = tareaActual == tareaBloqueo ? zonaSalida.position : cofre.position;
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

        // Selección del ganador: menor distancia al punto de referencia del CFP
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

    // -------------------- Participante --------------------

    // Recibe REQUEST de otro Iniciador. Responde AGREE si está IDLE y la tarea es conocida, REFUSE si no.
    private void OnRequestReceived(string from, string tarea, string convId)
    {
        AgenteFIPAACL iniciador = BuscarAgentePorId(from);
        if (iniciador == null) return;

        bool puedoAceptar = (estado == Estado.IDLE) &&
                            (tarea == tareaCofre || tarea == tareaAvistamiento ||
                             tarea == tareaLadron || tarea == tareaBloqueo);
        social.ResponderRequest(convId, puedoAceptar, iniciador);

        if (puedoAceptar)
        {
            iniciadorActual = iniciador;
            convIdActual = convId;
            tareaActual = tarea;
            deadlineFase = Time.time + timeoutFase;
            estado = Estado.PART_VOLUNTEERED;
        }
    }

    // Recibe CFP del Iniciador con la posición de referencia.
    // Calcula el coste (distancia propia al punto) y envía PROPOSE.
    // Si el CFP no corresponde a la conversación activa, rechaza.
    private void OnCFPReceived(string from, string tarea, Vector3 refPos, string convId)
    {
        if (estado != Estado.PART_VOLUNTEERED || convId != convIdActual)
        {
            AgenteFIPAACL emisor = BuscarAgentePorId(from);
            if (emisor != null) social.Rechazar(convId, emisor);
            return;
        }

        // Guarda la posición para activar GoToPositionBehaviour si ganamos
        if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron || tareaActual == tareaBloqueo)
            posicionInvestigacion = refPos;

        float coste = Vector3.Distance(transform.position, refPos);
        social.EnviarPropuesta(convId, coste, iniciadorActual);
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.PART_PROPOSED;
    }

    // Ganamos el contrato: activamos el behaviour correspondiente a la tarea.
    private void OnProposalAccepted(string convId)
    {
        if (estado != Estado.PART_PROPOSED || convId != convIdActual) return;
        estado = Estado.PART_EXECUTING;
        if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron || tareaActual == tareaBloqueo)
            goToPos.Activar(posicionInvestigacion);
        else
            checkChest.Activar();
    }

    // Perdimos el contrato: volvemos a IDLE sin ejecutar nada.
    private void OnProposalRejected(string convId)
    {
        if (convId != convIdActual) return;
        if (estado == Estado.PART_PROPOSED || estado == Estado.PART_VOLUNTEERED)
            ResetParticipante();
    }

    // El Iniciador cancela la conversación: abortamos el behaviour activo y volvemos a IDLE.
    private void OnCancellationReceived(string convId)
    {
        if (convId != convIdActual) return;
        if (estado == Estado.PART_EXECUTING)
        {
            if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron ||
                tareaActual == tareaBloqueo || tareaActual == tareaBusqueda)
                goToPos.Desactivar();
            else
                checkChest.Desactivar();
        }
        ResetParticipante();
    }

    // Llamado por CheckChestBehaviour cuando el patrullero llega al cofre y comprueba su estado.
    // Notifica al Iniciador con INFORM_RESULT y hace broadcast INFORM a todos los patrulleros.
    private void OnCheckChestCompletado(bool cofreIntacto)
    {
        if (estado != Estado.PART_EXECUTING) return;

        // INFORM_RESULT al Iniciador (dentro del Contract Net)
        if (iniciadorActual != null)
            social.InformarResultado(convIdActual, cofreIntacto,
                cofreIntacto ? "cofre intacto" : "cofre robado",
                tareaCofre, new List<AgenteFIPAACL> { iniciadorActual });

        // INFORM a todos los patrulleros (fuera del Contract Net, para que reinicien sus timers)
        social.BroadcastInform(convIdActual, cofreIntacto,
            cofreIntacto ? "cofre intacto" : "cofre robado");

        ResetParticipante();
        social.ReiniciarTimer();
    }

    // Llamado por GoToPositionBehaviour cuando el patrullero llega a su destino.
    // Para búsqueda coordinada simplemente vuelve a patrullar (no hay Iniciador que notificar).
    // Para el resto, notifica al Iniciador y hace broadcast a los demás patrulleros.
    private void OnInvestigacionCompletada()
    {
        if (estado != Estado.PART_EXECUTING) return;

        if (tareaActual == tareaBusqueda)
        {
            // Búsqueda coordinada: coordinación emergente sin Iniciador — solo volvemos a patrullar
            ResetParticipante();
            social.ReiniciarTimer();
            return;
        }

        string resultado = tareaActual == tareaBloqueo ? "salida bloqueada" : "avistamiento investigado";

        if (iniciadorActual != null)
            social.InformarResultado(convIdActual, true, resultado,
                tareaActual, new List<AgenteFIPAACL> { iniciadorActual });

        social.BroadcastInform(convIdActual, true, resultado, tareaActual);

        ResetParticipante();
        social.ReiniciarTimer();
    }

    // -------------------- Resultados y reacción a la alerta --------------------

    // Soy el Iniciador: recibo INFORM_RESULT del ganador. Solo cierro la FSM.
    // El ganador ya ha hecho el broadcast INFORM a todos los patrulleros.
    private void OnResultReceived(string from, bool exito, object datos, string convId)
    {
        if (estado == Estado.INIT_WAIT_RESULT && convId == convIdActual)
            FinalizarConversacionIniciador();
    }

    // Notificación externa al Contract Net: el ganador de comprobarCofre informa a todos los patrulleros.
    // Si el cofre fue robado, activa el modo alerta (velocidad aumentada) de forma permanente.
    private void OnInformReceived(string from, bool exito, object datos, string convId)
    {
        if (!exito)
        {
            cofreRobadoConfirmado = true;
            patrolBehaviour.ActivarModoAlerta();
            Debug.Log($"[{name}] ALERTA: cofre robado — modo alerta activado (informado por {from}, conv={convId}).");
        }
    }

    // -------------------- Helpers de transición --------------------

    // Resetea la FSM del Iniciador al estado inicial y limpia todas las listas de la conversación.
    private void FinalizarConversacionIniciador()
    {
        estado = Estado.IDLE;
        convIdActual = null;
        destinatariosRequest.Clear();
        voluntarios.Clear();
        proposalAgents.Clear();
        proposalCostes.Clear();
    }

    // Resetea la FSM del Participante al estado inicial.
    private void ResetParticipante()
    {
        estado = Estado.IDLE;
        convIdActual = null;
        iniciadorActual = null;
        tareaActual = null;
    }

    // Busca un agente en el registro global por su identificador de nombre.
    private static AgenteFIPAACL BuscarAgentePorId(string id)
    {
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.IdAgente == id) return a;
        return null;
    }
}
