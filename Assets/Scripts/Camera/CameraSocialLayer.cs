using System;
using System.Collections.Generic;
using UnityEngine;

// Capa Social de la cámara de vigilancia.
// Hereda de AgenteFIPAACL e implementa el vocabulario del lado Iniciador del protocolo Contract Net.
// La cámara nunca actúa como Participante: rechaza cualquier REQUEST entrante.
// Usa los structs de SkeletonSocialLayer para que los guardias interpreten correctamente los mensajes.
public class CameraSocialLayer : AgenteFIPAACL
{
    // Performatives — mismos valores de cadena que SkeletonSocialLayer para compatibilidad de protocolo.
    private const string REQUEST          = "request";
    private const string CFP              = "cfp";
    private const string ACCEPT_PROPOSAL  = "accept-proposal";
    private const string REJECT_PROPOSAL  = "reject-proposal";
    private const string REFUSE           = "refuse";
    private const string NOT_UNDERSTOOD   = "not-understood";

    // Eventos hacia CameraDeliberativeLayer (sin lenguaje ACL)
    public event Action<string, bool, string> OnRequestResponseReceived;  // from, acepto, convId
    public event Action<string, float, string> OnProposalReceived;         // from, coste, convId
    public event Action<string, bool, object, string> OnResultReceived;   // from, exito, datos, convId

    // Envía REQUEST a los destinatarios SkeletonSocialLayer pidiendo participantes para la tarea.
    // Devuelve el convId generado para que CameraDeliberativeLayer lo conserve.
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

    // Envía CFP a los voluntarios con la posición del avistamiento como referencia de coste.
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

    // Notifica al ganador que ha sido seleccionado para ejecutar la tarea.
    public void AceptarPropuesta(string convId, AgenteFIPAACL ganador)
    {
        ACLMessage msg = CrearMensaje(ACCEPT_PROPOSAL, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(ganador.IdAgente);
        EnviarMensaje(msg, ganador);
    }

    // Notifica a un candidato que no fue seleccionado.
    public void RechazarPropuesta(string convId, AgenteFIPAACL perdedor)
    {
        ACLMessage msg = CrearMensaje(REJECT_PROPOSAL, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(perdedor.IdAgente);
        EnviarMensaje(msg, perdedor);
    }

    protected override void ProcesarMensaje(ACLMessage msg)
    {
        switch (msg.performative)
        {
            case SkeletonSocialLayer.AGREE:
                OnRequestResponseReceived?.Invoke(msg.sender, true, msg.conversationId);
                break;

            case SkeletonSocialLayer.REFUSE:
                OnRequestResponseReceived?.Invoke(msg.sender, false, msg.conversationId);
                break;

            case SkeletonSocialLayer.PROPOSE:
                if (msg.content is SkeletonSocialLayer.ContentPropose pp)
                    OnProposalReceived?.Invoke(msg.sender, pp.coste, msg.conversationId);
                else
                    EnviarNotUnderstood(msg);
                break;

            case SkeletonSocialLayer.INFORM_RESULT:
                if (msg.content is SkeletonSocialLayer.ContentInform inf)
                    OnResultReceived?.Invoke(msg.sender, inf.exito, inf.datos, msg.conversationId);
                else
                    EnviarNotUnderstood(msg);
                break;

            case SkeletonSocialLayer.REQUEST:
                // La cámara nunca es Participante: rechaza cualquier REQUEST entrante.
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
                // Ignorado para evitar bucles.
                break;

            default:
                EnviarNotUnderstood(msg);
                break;
        }
    }

    private void EnviarNotUnderstood(ACLMessage msgOriginal)
    {
        AgenteFIPAACL emisor = BuscarAgentePorId(msgOriginal.sender);
        if (emisor == null) return;
        ACLMessage respuesta = CrearMensaje(NOT_UNDERSTOOD, msgOriginal.conversationId);
        respuesta.inReplyTo = msgOriginal.conversationId;
        respuesta.receivers.Add(emisor.IdAgente);
        EnviarMensaje(respuesta, emisor);
    }

    private static AgenteFIPAACL BuscarAgentePorId(string id)
    {
        foreach (AgenteFIPAACL a in AgenteFIPAACL.Todos)
            if (a.IdAgente == id) return a;
        return null;
    }
}
