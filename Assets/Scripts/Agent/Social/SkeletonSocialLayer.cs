using System;
using System.Collections.Generic;
using UnityEngine;

// Capa Social del esqueleto (patrullero o guardia).
// Hereda de AgenteFIPAACL y contiene todo el lenguaje ACL del protocolo Contract Net.
// La Deliberativa nunca ve un ACLMessage: solo llama métodos de esta clase y escucha sus eventos.
public class SkeletonSocialLayer : AgenteFIPAACL
{
    // Performatives
    public const string REQUEST = "request";
    public const string AGREE = "agree";
    public const string REFUSE = "refuse";
    public const string CFP = "cfp";
    public const string PROPOSE = "propose";
    public const string ACCEPT_PROPOSAL = "accept-proposal";
    public const string REJECT_PROPOSAL = "reject-proposal";
    public const string INFORM_DONE = "inform-done";
    public const string INFORM_RESULT = "inform-result";
    public const string FAILURE = "failure";
    public const string NOT_UNDERSTOOD = "not-understood";
    public const string CANCEL = "cancel";

    // Valor que pausa el timer indefinidamente hasta que se sortee uno nuevo.
    private const float TIMER_MAX = 9999f;

    // Inspector
    public float timerMin = 20f;
    public float timerMax = 40f;

    // Estado interno
    private float timerActual;

    // Payloads: contenido concreto que va dentro del campo "content" de cada ACLMessage
    public struct ContenidoRequest
    {
        public string tarea;
    }

    public struct ContenidoCFP
    {
        public string tarea;
        public Vector3 posicionReferencia;
    }

    public struct ContenidoPropose
    {
        public float coste;
    }

    public struct ContenidoInforme
    {
        public string tarea;
        public bool exito;
        public object datos;
    }

    // Eventos hacia la Deliberativa: traducen los ACLMessage entrantes a datos sin lenguaje FIPA
    public event Action<string, string, string> OnRequestRecibido;          // from, tarea, convId
    public event Action<string, bool, string> OnRespuestaRequestRecibida;   // from, acepto, convId
    public event Action<string, string, Vector3, string> OnCFPRecibida;     // from, tarea, refPos, convId
    public event Action<string, float, string> OnPropuestaRecibida;         // from, coste, convId
    public event Action<string> OnAceptacionRecibida;                       // convId
    public event Action<string> OnRechazoRecibido;                          // convId
    public event Action<string, bool, object, string> OnResultadoRecibido;  // from, exito, datos, convId
    public event Action<string> OnFalloRecibido;                            // convId
    public event Action<string> OnCancelacionRecibida;                      // convId
    public event Action OnTimerInicio;                                      // sin payload — es el turno de intentar iniciar

    // Sortea el timer inicial al arrancar la escena
    void Start()
    {
        SortearTimer();
    }

    // Decrementa el timer cada frame y avisa a la Deliberativa cuando llega a cero
    protected override void ActualizarAgente()
    {
        if (timerActual <= 0f) return;
        timerActual -= Time.deltaTime;
        if (timerActual <= 0f)
        {
            timerActual = 0f;
            OnTimerInicio?.Invoke();
        }
    }

    // API hacia arriba: métodos que llama la Deliberativa

    // Fase 0 — pregunta a todos los esqueletos si están disponibles para participar
    public void EnviarRequest(string tarea, List<AgenteFIPAACL> destinatarios)
    {
        timerActual = TIMER_MAX;
        foreach (AgenteFIPAACL dest in destinatarios)
        {
            if (dest == this) continue;
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(REQUEST);
            msg.content = new ContenidoRequest { tarea = tarea };
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // Responde AGREE o REFUSE al iniciador según si este agente está libre
    public void ResponderRequest(string convId, bool acepto, AgenteFIPAACL iniciador)
    {
        string performative = acepto ? AGREE : REFUSE;
        ACLMessage msg = CrearMensaje(performative, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(iniciador.IdAgente);
        EnviarMensaje(msg, iniciador);
    }

    // Fase 1 — invita a los voluntarios a participar enviando la posición del cofre
    public void EnviarCFP(string convId, List<AgenteFIPAACL> participantes, Vector3 refPos, string tarea)
    {
        foreach (AgenteFIPAACL dest in participantes)
        {
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(CFP, convId);
            msg.content = new ContenidoCFP { tarea = tarea, posicionReferencia = refPos };
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // Envía la oferta de coste (distancia al cofre) al iniciador
    public void EnviarPropuesta(string convId, float coste, AgenteFIPAACL iniciador)
    {
        ACLMessage msg = CrearMensaje(PROPOSE, convId);
        msg.content = new ContenidoPropose { coste = coste };
        msg.inReplyTo = convId;
        msg.receivers.Add(iniciador.IdAgente);
        EnviarMensaje(msg, iniciador);
    }

    // Notifica al ganador que ha sido seleccionado para ejecutar la tarea
    public void AceptarPropuesta(string convId, AgenteFIPAACL ganador)
    {
        ACLMessage msg = CrearMensaje(ACCEPT_PROPOSAL, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(ganador.IdAgente);
        EnviarMensaje(msg, ganador);
    }

    // Notifica a un candidato que no fue seleccionado
    public void RechazarPropuesta(string convId, AgenteFIPAACL perdedor)
    {
        ACLMessage msg = CrearMensaje(REJECT_PROPOSAL, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(perdedor.IdAgente);
        EnviarMensaje(msg, perdedor);
    }

    // El ganador informa del resultado de la tarea a todos los que recibieron el REQUEST
    public void InformarResultado(string convId, bool exito, object datos, List<AgenteFIPAACL> destinatarios)
    {
        foreach (AgenteFIPAACL dest in destinatarios)
        {
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(INFORM_RESULT, convId);
            msg.content = new ContenidoInforme { tarea = "comprobarCofre", exito = exito, datos = datos };
            msg.inReplyTo = convId;
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // REFUSE genérico: rechaza participar en un CFP o REQUEST cuando el agente está ocupado
    public void Rechazar(string convId, AgenteFIPAACL destinatario)
    {
        ACLMessage msg = CrearMensaje(REFUSE, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(destinatario.IdAgente);
        EnviarMensaje(msg, destinatario);
    }

    // El ganador no pudo completar la tarea; el iniciador lo trata como fallo
    public void Fallar(string convId, AgenteFIPAACL iniciador)
    {
        ACLMessage msg = CrearMensaje(FAILURE, convId);
        msg.inReplyTo = convId;
        msg.receivers.Add(iniciador.IdAgente);
        EnviarMensaje(msg, iniciador);
    }

    // El iniciador cancela la conversación; los comprometidos abortan y sortean timer nuevo
    public void Cancelar(string convId, List<AgenteFIPAACL> participantes)
    {
        foreach (AgenteFIPAACL dest in participantes)
        {
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(CANCEL, convId);
            msg.inReplyTo = convId;
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // Procesado de mensajes entrantes

    protected override void ProcesarMensaje(ACLMessage msg)
    {
        switch (msg.performative)
        {
            case REQUEST:
                timerActual = TIMER_MAX;
                if (msg.content is ContenidoRequest cr)
                    OnRequestRecibido?.Invoke(msg.sender, cr.tarea, msg.conversationId);
                else
                    EnviarNotUnderstood(msg);
                break;

            case AGREE:
                OnRespuestaRequestRecibida?.Invoke(msg.sender, true, msg.conversationId);
                break;

            case REFUSE:
                OnRespuestaRequestRecibida?.Invoke(msg.sender, false, msg.conversationId);
                break;

            case CFP:
                if (msg.content is ContenidoCFP cfp)
                    OnCFPRecibida?.Invoke(msg.sender, cfp.tarea, cfp.posicionReferencia, msg.conversationId);
                else
                    EnviarNotUnderstood(msg);
                break;

            case PROPOSE:
                if (msg.content is ContenidoPropose pp)
                    OnPropuestaRecibida?.Invoke(msg.sender, pp.coste, msg.conversationId);
                else
                    EnviarNotUnderstood(msg);
                break;

            case ACCEPT_PROPOSAL:
                OnAceptacionRecibida?.Invoke(msg.conversationId);
                break;

            case REJECT_PROPOSAL:
                OnRechazoRecibido?.Invoke(msg.conversationId);
                break;

            case INFORM_RESULT:
                if (msg.content is ContenidoInforme inf)
                {
                    if (inf.tarea == "comprobarCofre")
                        SortearTimer();
                    OnResultadoRecibido?.Invoke(msg.sender, inf.exito, inf.datos, msg.conversationId);
                }
                else
                    EnviarNotUnderstood(msg);
                break;

            case FAILURE:
                OnFalloRecibido?.Invoke(msg.conversationId);
                break;

            case CANCEL:
                SortearTimer();
                OnCancelacionRecibida?.Invoke(msg.conversationId);
                break;

            case NOT_UNDERSTOOD:
                // Ignorado para evitar bucles de NOT_UNDERSTOOD
                break;

            default:
                EnviarNotUnderstood(msg);
                break;
        }
    }

    // Helpers

    private void SortearTimer()
    {
        timerActual = UnityEngine.Random.Range(timerMin, timerMax);
    }

    // Si llega un mensaje con performative desconocido o content de tipo incorrecto, notifica al emisor
    private void EnviarNotUnderstood(ACLMessage msgOriginal)
    {
        AgenteFIPAACL emisor = Todos.Find(a => a.IdAgente == msgOriginal.sender);
        if (emisor == null) return;
        ACLMessage respuesta = CrearMensaje(NOT_UNDERSTOOD, msgOriginal.conversationId);
        respuesta.inReplyTo = msgOriginal.conversationId;
        respuesta.receivers.Add(emisor.IdAgente);
        EnviarMensaje(respuesta, emisor);
    }
}
