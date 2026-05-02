// Capa Deliberativa de la cámara de vigilancia.
// Implementa la FSM del protocolo FIPA Contract Net como Iniciador exclusivo.
// A diferencia de los esqueletos, la cámara no tiene capa reactiva ni ControlSubsystem:
// el trigger del protocolo viene directamente del sensor (CameraSensor) y la cámara
// nunca propone movimiento propio — solo coordina a los patrulleros.
using System.Collections.Generic;
using UnityEngine;

public class CameraDeliberativeLayer : MonoBehaviour
{
    public float timeoutFase = 3f;       // Segundos máximos para esperar AGREE/REFUSE o PROPOSE en cada fase
    public float timeoutEjecucion = 15f; // Segundos máximos para esperar INFORM_RESULT del patrullero ganador
    public string tareaAvistamiento = "investigarAvistamiento"; // Identificador de la tarea que se negocia

    private CameraSocialLayer social; // Capa social: traduce entre la FSM y los mensajes ACL
    private CameraSensor sensor;      // Sensor de visión simplificado exclusivo para cámaras
    private bool protocoloActivo;     // Evita lanzar un nuevo protocolo si ya hay uno en curso

    // Estados de la FSM del Iniciador:
    // IDLE → WAIT_RESPONSES → WAIT_PROPOSALS → WAIT_RESULT → IDLE
    private enum Estado
    {
        IDLE,
        WAIT_RESPONSES,  // REQUEST enviado; esperando AGREE/REFUSE de los guardias
        WAIT_PROPOSALS,  // CFP enviado a voluntarios; esperando PROPOSE con el coste de cada uno
        WAIT_RESULT      // ACCEPT_PROPOSAL enviado al ganador; esperando INFORM_RESULT
    }

    private Estado estado = Estado.IDLE;
    private string convIdActual;          // GUID compartido por todos los mensajes de la misma conversación
    private float deadlineFase;           // Tiempo absoluto (Time.time) en que expira la fase actual
    private Vector3 posicionAvistamiento; // Posición del ladrón en el momento del avistamiento; se pasa en el CFP

    // Listas del Iniciador para acumular respuestas a lo largo del protocolo
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
        social = GetComponent<CameraSocialLayer>();
        sensor = GetComponent<CameraSensor>();

        // Suscripción directa al sensor: no hay capa reactiva intermedia
        sensor.OnLadronAvistado += OnLadronAvistado;
        sensor.OnLadronPerdido += OnLadronPerdido;

        // Suscripción a los eventos de la capa social (respuestas ACL traducidas a datos)
        social.OnRequestResponseReceived += OnRequestResponseReceived;
        social.OnProposalReceived += OnProposalReceived;
        social.OnResultReceived += OnResultReceived;
    }

    // Disparado por CameraSensor cuando el ladrón entra en el campo de visión.
    // Solo lanza el protocolo si no hay uno ya activo (flag protocoloActivo).
    private void OnLadronAvistado()
    {
        if (protocoloActivo) return;
        protocoloActivo = true;
        Debug.Log($"[{name}] Ladrón avistado en {sensor.PosicionLadron}, iniciando ContractNet.");
        IniciarProtocolo(sensor.PosicionLadron);
    }

    // Disparado por CameraSensor cuando el ladrón sale del campo de visión.
    // Resetea el flag para permitir un nuevo protocolo en el próximo avistamiento.
    private void OnLadronPerdido()
    {
        Debug.Log($"[{name}] Ladrón perdido de vista.");
        protocoloActivo = false;
    }

    // Comprueba en cada frame si ha expirado el deadline de la fase actual.
    // Si expira, avanza o cancela el protocolo según el estado.
    void Update()
    {
        if (estado == Estado.IDLE) return;
        if (Time.time < deadlineFase) return;

        switch (estado)
        {
            case Estado.WAIT_RESPONSES:
                // Deadline de REQUEST: procesamos las respuestas recibidas hasta ahora
                FinDeRecogidaDeRespuestas();
                break;
            case Estado.WAIT_PROPOSALS:
                // Deadline de CFP: procesamos las propuestas recibidas hasta ahora
                FinDeRecogidaDePropuestas();
                break;
            case Estado.WAIT_RESULT:
                // El ganador no respondió a tiempo: cerramos la conversación
                Debug.Log($"[{name}] Timeout esperando INFORM_RESULT, protocolo cerrado (conv={convIdActual?.Substring(0, 8)}).");
                FinalizarConversacion();
                break;
        }
    }

    // Fase 0: envía REQUEST a todos los SkeletonSocialLayer registrados en la escena.
    // Guarda la posición del avistamiento para incluirla luego en el CFP.
    private void IniciarProtocolo(Vector3 posicion)
    {
        if (estado != Estado.IDLE) return;
        posicionAvistamiento = posicion;

        // Recopila solo los patrulleros: los guardianes siempre responden REFUSE (no pueden moverse)
        destinatariosRequest.Clear();
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
        {
            if (a.GetComponent<PatrolDeliberativeLayer>() != null) destinatariosRequest.Add(a);
        }

        if (destinatariosRequest.Count == 0)
        {
            Debug.Log($"[{name}] No hay guardias registrados, protocolo cancelado.");
            return;
        }

        convIdActual = social.EnviarRequest(tareaAvistamiento, destinatariosRequest);
        Debug.Log($"[{name}] REQUEST enviado a {destinatariosRequest.Count} guardia(s) (conv={convIdActual.Substring(0, 8)}).");
        voluntarios.Clear();
        respuestasEsperadas = destinatariosRequest.Count;
        respuestasRecibidas = 0;
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.WAIT_RESPONSES;
    }

    // Recibe cada AGREE o REFUSE. Acumula voluntarios y avanza cuando llegan todas las respuestas.
    private void OnRequestResponseReceived(string from, bool acepto, string convId)
    {
        if (estado != Estado.WAIT_RESPONSES || convId != convIdActual) return;
        respuestasRecibidas++;

        if (acepto)
        {
            AgenteFIPAACL voluntario = BuscarAgentePorId(from);
            if (voluntario != null) voluntarios.Add(voluntario);
        }

        if (respuestasRecibidas >= respuestasEsperadas) FinDeRecogidaDeRespuestas();
    }

    // Fase 1: envía CFP a los voluntarios con la posición del avistamiento como referencia de coste.
    // Si nadie aceptó, cancela el protocolo.
    private void FinDeRecogidaDeRespuestas()
    {
        if (voluntarios.Count == 0)
        {
            Debug.Log($"[{name}] Ningún voluntario, protocolo abortado (conv={convIdActual.Substring(0, 8)}).");
            FinalizarConversacion();
            return;
        }

        Debug.Log($"[{name}] {voluntarios.Count} voluntario(s), enviando CFP (conv={convIdActual.Substring(0, 8)}).");
        social.EnviarCFP(convIdActual, voluntarios, posicionAvistamiento, tareaAvistamiento);
        proposalAgents.Clear();
        proposalCostes.Clear();
        propuestasEsperadas = voluntarios.Count;
        propuestasRecibidas = 0;
        deadlineFase = Time.time + timeoutFase;
        estado = Estado.WAIT_PROPOSALS;
    }

    // Recibe cada PROPOSE con el coste (distancia al punto de avistamiento) del candidato.
    private void OnProposalReceived(string from, float coste, string convId)
    {
        if (estado != Estado.WAIT_PROPOSALS || convId != convIdActual) return;
        propuestasRecibidas++;

        AgenteFIPAACL agente = BuscarAgentePorId(from);
        if (agente != null)
        {
            proposalAgents[from] = agente;
            proposalCostes[from] = coste;
        }

        if (propuestasRecibidas >= propuestasEsperadas) FinDeRecogidaDePropuestas();
    }

    // Selecciona al candidato con menor coste (el más cercano al avistamiento),
    // le envía ACCEPT_PROPOSAL y rechaza al resto.
    private void FinDeRecogidaDePropuestas()
    {
        if (proposalCostes.Count == 0)
        {
            Debug.Log($"[{name}] Ninguna propuesta recibida, protocolo abortado (conv={convIdActual.Substring(0, 8)}).");
            FinalizarConversacion();
            return;
        }

        // Selección del ganador: menor distancia al punto de avistamiento
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

        Debug.Log($"[{name}] Ganador: {ganadorId} (coste={mejorCoste:F1}), ACCEPT_PROPOSAL enviado (conv={convIdActual.Substring(0, 8)}).");
        AgenteFIPAACL ganador = proposalAgents[ganadorId];
        social.AceptarPropuesta(convIdActual, ganador);
        foreach (KeyValuePair<string, AgenteFIPAACL> kv in proposalAgents)
            if (kv.Key != ganadorId) social.RechazarPropuesta(convIdActual, kv.Value);

        // Timeout: la ejecución implica desplazarse físicamente al punto
        deadlineFase = Time.time + timeoutEjecucion;
        estado = Estado.WAIT_RESULT;
    }

    // El patrullero ganador notifica que llegó al punto de avistamiento.
    // Cierra la conversación — el protocolo ha concluido con éxito.
    private void OnResultReceived(string from, bool exito, object datos, string convId)
    {
        if (convId != convIdActual) return;
        Debug.Log($"[{name}] Investigación completada por {from} (conv={convId.Substring(0, 8)}), protocolo cerrado.");
        FinalizarConversacion();
    }

    // Resetea toda la FSM al estado inicial, lista para el próximo avistamiento.
    private void FinalizarConversacion()
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
