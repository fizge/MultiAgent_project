using System;
using System.Collections.Generic;
using UnityEngine;

// Capa Social de la cámara de vigilancia.
// Hereda de AgenteFIPAACL e implementa el vocabulario del lado Iniciador del protocolo Contract Net.
// La cámara nunca actúa como Participante: rechaza cualquier REQUEST entrante automáticamente.
// Usa los structs de SkeletonSocialLayer directamente para que el pattern matching en
// SkeletonSocialLayer.ProcesarMensaje funcione correctamente al recibir mensajes de la cámara.
public class CameraSocialLayer : AgenteFIPAACL
{
    // Performatives — mismos valores de cadena que SkeletonSocialLayer para compatibilidad de protocolo.
    // Si los valores difirieran, los guardias no reconocerían los mensajes de la cámara.
    private const string REQUEST          = "request";
    private const string CFP              = "cfp";
    private const string ACCEPT_PROPOSAL  = "accept-proposal";
    private const string REJECT_PROPOSAL  = "reject-proposal";
    private const string REFUSE           = "refuse";
    private const string NOT_UNDERSTOOD   = "not-understood";

    // Eventos hacia CameraDeliberativeLayer: traducen los ACLMessage entrantes a datos sin lenguaje FIPA.
    // La deliberativa nunca ve un ACLMessage directamente.
    public event Action<string, bool, string> OnRequestResponseReceived;  // from, acepto, convId — AGREE o REFUSE al REQUEST
    public event Action<string, float, string> OnProposalReceived;         // from, coste, convId — PROPOSE del candidato
    public event Action<string, bool, object, string> OnResultReceived;   // from, exito, datos, convId — INFORM_RESULT del ganador

    // Fase 0 — envía REQUEST a cada SkeletonSocialLayer de la lista preguntando si están disponibles.
    // Filtra agentes que no sean SkeletonSocialLayer (por si hubiera otros tipos en Todos).
    // Devuelve el convId generado para que CameraDeliberativeLayer lo conserve y reutilice en fases posteriores.
    public string EnviarRequest(string tarea, List<AgenteFIPAACL> destinatarios)
    {
        string convId = System.Guid.NewGuid().ToString();
        foreach (AgenteFIPAACL dest in destinatarios)
        {
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(REQUEST, convId);
            msg.content = new SkeletonSocialLayer.ContentRequest { tarea = tarea };
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
        return convId;
    }

    // Fase 1 — envía CFP a los voluntarios que respondieron AGREE.
    // Incluye la posición del avistamiento como referencia para que cada candidato calcule su coste (distancia).
    public void EnviarCFP(string convId, List<AgenteFIPAACL> voluntarios, Vector3 posicion, string tarea)
    {
        foreach (AgenteFIPAACL dest in voluntarios)
        {
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(CFP, convId);
            msg.content = new SkeletonSocialLayer.ContentCFP { tarea = tarea, posicionReferencia = posicion };
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // Notifica al candidato ganador (menor coste) que ha sido seleccionado para ejecutar la tarea.
    public void AceptarPropuesta(string convId, AgenteFIPAACL ganador)
    {
        ACLMessage msg = CrearMensaje(ACCEPT_PROPOSAL, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(ganador.IdAgente);
        EnviarMensaje(msg, ganador);
    }

    // Notifica a los candidatos no seleccionados que han sido rechazados para que vuelvan a patrullar.
    public void RechazarPropuesta(string convId, AgenteFIPAACL perdedor)
    {
        ACLMessage msg = CrearMensaje(REJECT_PROPOSAL, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(perdedor.IdAgente);
        EnviarMensaje(msg, perdedor);
    }

    // Procesa cada mensaje entrante y dispara el evento correspondiente hacia CameraDeliberativeLayer.
    // La cámara solo espera AGREE, REFUSE, PROPOSE e INFORM_RESULT — cualquier otro se responde con NOT_UNDERSTOOD.
    protected override void ProcesarMensaje(ACLMessage msg)
    {
        switch (msg.performative)
        {
            case SkeletonSocialLayer.AGREE:
                // Un guardia acepta participar: la deliberativa lo añade a la lista de voluntarios
                OnRequestResponseReceived?.Invoke(msg.sender, true, msg.conversationId);
                break;

            case SkeletonSocialLayer.REFUSE:
                // Un guardia rechaza participar (está ocupado o es guardián estático)
                OnRequestResponseReceived?.Invoke(msg.sender, false, msg.conversationId);
                break;

            case SkeletonSocialLayer.PROPOSE:
                // Un voluntario propone su coste (distancia al punto de avistamiento)
                if (msg.content is SkeletonSocialLayer.ContentPropose pp)
                    OnProposalReceived?.Invoke(msg.sender, pp.coste, msg.conversationId);
                else
                    EnviarNotUnderstood(msg);
                break;

            case SkeletonSocialLayer.INFORM_RESULT:
                // El patrullero ganador notifica que completó la investigación
                if (msg.content is SkeletonSocialLayer.ContentInform inf)
                    OnResultReceived?.Invoke(msg.sender, inf.exito, inf.datos, msg.conversationId);
                else
                    EnviarNotUnderstood(msg);
                break;

            case SkeletonSocialLayer.REQUEST:
                // La cámara nunca es Participante: rechaza cualquier REQUEST entrante de otros iniciadores
                AgenteFIPAACL solicitante = BuscarAgentePorId(msg.sender);
                if (solicitante != null)
                {
                    ACLMessage refuse = CrearMensaje(REFUSE, msg.conversationId);
                    refuse.inReplyTo = msg.conversationId;
                    refuse.receivers.Add(solicitante.IdAgente);
                    EnviarMensaje(refuse, solicitante);
                }
                break;

            case SkeletonSocialLayer.NOT_UNDERSTOOD:
                // Ignorado para evitar bucles de NOT_UNDERSTOOD
                break;

            default:
                // Performative desconocido: notifica al emisor
                EnviarNotUnderstood(msg);
                break;
        }
    }

    // Responde al emisor con NOT_UNDERSTOOD cuando llega un mensaje con performative desconocido
    // o con un content de tipo incorrecto.
    private void EnviarNotUnderstood(ACLMessage msgOriginal)
    {
        AgenteFIPAACL emisor = BuscarAgentePorId(msgOriginal.sender);
        if (emisor == null) return;
        ACLMessage respuesta = CrearMensaje(NOT_UNDERSTOOD, msgOriginal.conversationId);
        respuesta.inReplyTo = msgOriginal.conversationId;
        respuesta.receivers.Add(emisor.IdAgente);
        EnviarMensaje(respuesta, emisor);
    }

    // Busca un agente en el registro global por su identificador de nombre.
    private static AgenteFIPAACL BuscarAgentePorId(string id)
    {
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.IdAgente == id) return a;
        return null;
    }
}
