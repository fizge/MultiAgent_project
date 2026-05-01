// FSM del protocolo FIPA Contract Net para guardias patrulleros.
// Puede actuar como Iniciador (cuando expira su timer) y como Participante (cuando recibe REQUEST/CFP).
// No conoce el lenguaje ACL: solo llama métodos de SkeletonSocialLayer y reacciona a sus eventos.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PatrolDeliberativeLayer : DeliberativeLayer
{
    [SerializeField] private Transform cofre;
    [SerializeField] private Transform zonaSalida;
    public float velocidadComprobacion = 4f;
    public float aceleracionComprobacion = 2f;
    public float toleranciaCofre = 2f;
    public float timeoutFase = 3f; // segundos máximos de espera por fase del protocolo
    public string tareaCofre = "comprobarCofre";
    private const string tareaAvistamiento = "investigarAvistamiento";
    private const string tareaLadron = "ladronAvistado";
    private const string tareaBloqueo = "bloquearSalida";
    private const string tareaBusqueda = "busquedaCoordinada";
    public float radioBusqueda = 8f;

    private SkeletonSocialLayer social;
    private VisionSensor visionSensor;
    private CheckChestBehaviour checkChest;
    private GoToPositionBehaviour goToPos;
    private PatrolBehaviour patrolBehaviour;

    // La capa deliberativa no sabe quá tarea está ejecutando hasta que recibe el request,
    // pero lo guarda para decidir qué comportamiento activar al aceptar la propuesta
    private string tareaActual; 
    private Vector3 posicionInvestigacion;

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

    void Start()
    {
        patrolBehaviour = GetComponent<PatrolReactiveLayer>().Patrol;
    }

    void Awake()
    {
        social = GetComponent<SkeletonSocialLayer>();
        visionSensor = GetComponent<VisionSensor>();
        visionSensor.OnCofresDesaparecido += OnCofresDesaparecido;
        visionSensor.OnLadronPerdido += OnLadronPerdido;
        MovementSensor mov = GetComponent<MovementSensor>();

        checkChest = new CheckChestBehaviour
        {
            speed = velocidadComprobacion,
            acceleration = aceleracionComprobacion,
            toleranciaCofre = toleranciaCofre
        };
        checkChest.Initialize(transform, cofre, mov, visionSensor);
        checkChest.OnComprobacionCompletada += OnCheckChestCompletado;

        goToPos = new GoToPositionBehaviour
        {
            speed = velocidadComprobacion,
            acceleration = aceleracionComprobacion
        };
        goToPos.Initialize(transform, mov);
        goToPos.OnLlegadaCompletada += OnInvestigacionCompletada;

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
        if (estado != Estado.PART_EXECUTING) return null;
        if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron || tareaActual == tareaBloqueo || tareaActual == tareaBusqueda) return goToPos.Evaluate();
        return checkChest.Evaluate();
    }

    // -------------------- Iniciador --------------------

    private void OnTimerExpired()
    {
        if (estado != Estado.IDLE) return;
        if (cofre == null) return;
        if (cofreRobadoConfirmado) return;
        LanzarProtocoloIniciador(tareaCofre);
    }

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

    private void OnLadronPerdido()
    {
        if (estado != Estado.IDLE) return;
        Debug.Log($"[{name}] Ladrón perdido — iniciando búsqueda coordinada.");
        social.InformarBusqueda(visionSensor.PosicionLadron);
        IrASector(visionSensor.PosicionLadron);
    }

    private void OnBusquedaCoordinadaReceived(Vector3 ultimaPosicion)
    {
        if (estado != Estado.IDLE) return;
        IrASector(ultimaPosicion);
    }

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

        NavMeshHit hit;
        Vector3 destino = NavMesh.SamplePosition(candidato, out hit, radioBusqueda, NavMesh.AllAreas)
            ? hit.position
            : transform.position; // si no hay punto válido, el agente se queda donde está

        tareaActual = tareaBusqueda;
        estado = Estado.PART_EXECUTING;
        goToPos.Activar(destino);
        Debug.Log($"[{name}] Sector {angulo:F0}° → {destino}");
    }

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

        Vector3 posicionCFP = tareaActual == tareaBloqueo ? zonaSalida.position : cofre.position;
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

        // Selecciona la propuesta de menor coste.
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

        deadlineFase = Time.time + timeoutFase * 4f;
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

        bool puedoAceptar = (estado == Estado.IDLE) && (tarea == tareaCofre || tarea == tareaAvistamiento || tarea == tareaLadron || tarea == tareaBloqueo);
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

    private void OnCFPReceived(string from, string tarea, Vector3 refPos, string convId)
    {
        if (estado != Estado.PART_VOLUNTEERED || convId != convIdActual)
        {
            // CFP fuera de contexto: rechazamos.
            AgenteFIPAACL emisor = BuscarAgentePorId(from);
            if (emisor != null) social.Rechazar(convId, emisor);
            return;
        }

        // Guarda la posición de investigación
        if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron || tareaActual == tareaBloqueo) posicionInvestigacion = refPos;

        float coste = Vector3.Distance(transform.position, refPos);
        social.EnviarPropuesta(convId, coste, iniciadorActual);
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.PART_PROPOSED;
    }

    private void OnProposalAccepted(string convId)
    {
        if (estado != Estado.PART_PROPOSED || convId != convIdActual) return;
        estado = Estado.PART_EXECUTING;
        if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron || tareaActual == tareaBloqueo)
            goToPos.Activar(posicionInvestigacion);
        else
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
        if (estado == Estado.PART_EXECUTING)
        {
            if (tareaActual == tareaAvistamiento || tareaActual == tareaLadron || tareaActual == tareaBloqueo || tareaActual == tareaBusqueda)
                goToPos.Desactivar();
            else
                checkChest.Desactivar();
        }
        ResetParticipante();
    }

    // Llamado por CheckChestBehaviour cuando el patrullero ganador llega al cofre.
    private void OnCheckChestCompletado(bool cofreIntacto)
    {
        if (estado != Estado.PART_EXECUTING) return;

        // Dentro del Contract Net: INFORM_RESULT solo al Iniciador.
        if (iniciadorActual != null)
            social.InformarResultado(convIdActual, cofreIntacto, cofreIntacto ? "cofre intacto" : "cofre robado",
                                      tareaCofre, new List<AgenteFIPAACL> { iniciadorActual });

        // Fuera del Contract Net: INFORM a todos los patrulleros (filtra cámaras y guardianes en BroadcastInform).
        social.BroadcastInform(convIdActual, cofreIntacto, cofreIntacto ? "cofre intacto" : "cofre robado");

        ResetParticipante();
        social.ReiniciarTimer(); // el ganador reinicia su timer al terminar la tarea
    }

    // Llamado por GoToPositionBehaviour cuando el patrullero llega a su destino.
    private void OnInvestigacionCompletada()
    {
        if (estado != Estado.PART_EXECUTING) return;

        if (tareaActual == tareaBusqueda)
        {
            // Búsqueda coordinada: no hay iniciador ni broadcast, simplemente volvemos a patrullar.
            ResetParticipante();
            social.ReiniciarTimer();
            return;
        }

        string resultado = tareaActual == tareaBloqueo ? "salida bloqueada" : "avistamiento investigado";

        if (iniciadorActual != null)
            social.InformarResultado(convIdActual, true, resultado, tareaActual, new List<AgenteFIPAACL> { iniciadorActual });

        social.BroadcastInform(convIdActual, true, resultado, tareaActual);

        ResetParticipante();
        social.ReiniciarTimer();
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
            cofreRobadoConfirmado = true;
            patrolBehaviour.ActivarModoAlerta();
            Debug.Log($"[{name}] ALERTA: cofre robado — modo alerta activado (informado por {from}, conv={convId}).");
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
        tareaActual = null;
    }

    private static AgenteFIPAACL BuscarAgentePorId(string id)
    {
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.IdAgente == id) return a;
        return null;
    }
}
