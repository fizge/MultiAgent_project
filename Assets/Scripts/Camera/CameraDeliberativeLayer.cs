// FSM del protocolo FIPA Contract Net para la cámara de vigilancia.
// Solo actúa como Iniciador: nunca participa como candidato.
// No hereda de DeliberativeLayer porque la cámara no propone movimiento y no usa ControlSubsystem.
// El disparo viene de CameraReactiveLayer (evento de percepción), no de un timer como en los esqueletos.
using System.Collections.Generic;
using UnityEngine;

public class CameraDeliberativeLayer : MonoBehaviour
{
    public float timeoutFase = 3f; // Tiempo máximo para esperar respuestas en cada fase del protocolo
    public float timeoutEjecucion = 15f;  // Tiempo máximo para esperar el resultado de la ejecución de la tarea por parte del ganador
    public string tareaAvistamiento = "investigarAvistamiento";

    private CameraSocialLayer social;

    private enum Estado
    {
        IDLE,
        WAIT_RESPONSES,  // REQUEST enviado; esperando AGREE/REFUSE de los guardias
        WAIT_PROPOSALS,  // CFP enviado a voluntarios; esperando PROPOSE
        WAIT_RESULT      // ganador aceptado; esperando INFORM_RESULT del ejecutor
    }

    private Estado estado = Estado.IDLE;
    private string convIdActual;
    private float deadlineFase;
    private Vector3 posicionAvistamiento;

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

        social.OnRequestResponseReceived += OnRequestResponseReceived;
        social.OnProposalReceived += OnProposalReceived;
        social.OnResultReceived += OnResultReceived;
    }

    void Update()
    {
        if (estado == Estado.IDLE) return;
        if (Time.time < deadlineFase) return;

        switch (estado)
        {
            case Estado.WAIT_RESPONSES:
                FinDeRecogidaDeRespuestas();
                break;
            case Estado.WAIT_PROPOSALS:
                FinDeRecogidaDePropuestas();
                break;
            case Estado.WAIT_RESULT:
                Debug.Log($"[{name}] Timeout esperando INFORM_RESULT, protocolo cerrado (conv={convIdActual?.Substring(0, 8)}).");
                FinalizarConversacion();
                break;
        }
    }

    // Llamado por CameraReactiveLayer al detectar al ladrón.
    public void IniciarProtocolo(Vector3 posicion)
    {
        if (estado != Estado.IDLE) return;
        posicionAvistamiento = posicion;

        destinatariosRequest.Clear();
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
        {
            if (a is SkeletonSocialLayer) destinatariosRequest.Add(a);
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

    private void FinDeRecogidaDePropuestas()
    {
        if (proposalCostes.Count == 0)
        {
            Debug.Log($"[{name}] Ninguna propuesta recibida, protocolo abortado (conv={convIdActual.Substring(0, 8)}).");
            FinalizarConversacion();
            return;
        }

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

        deadlineFase = Time.time + timeoutEjecucion;
        estado = Estado.WAIT_RESULT;
    }

    private void OnResultReceived(string from, bool exito, object datos, string convId)
    {
        if (convId != convIdActual) return;
        Debug.Log($"[{name}] Investigación completada por {from} (conv={convId.Substring(0, 8)}), protocolo cerrado.");
        FinalizarConversacion();
    }

    private void FinalizarConversacion()
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
