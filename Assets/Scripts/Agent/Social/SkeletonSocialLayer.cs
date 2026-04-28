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
    public const string INFORM = "inform"; // performative externo al Contract Net: el ganador notifica a todos los patrulleros
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

    // Contents: contenido concreto que va dentro del campo "content" de cada ACLMessage
    // Algunos mensajes no necesitan content (AGREE, REFUSE, ACCEPT_PROPOSAL, REJECT_PROPOSAL, CANCEL), pero otros sí para transmitir datos relevantes de la conversación
    public struct ContentRequest
    {
        public string tarea;
    }

    public struct ContentCFP
    {
        public string tarea;
        public Vector3 posicionReferencia;
    }

    public struct ContentPropose
    {
        public float coste;
    }

    public struct ContentInform
    {
        public string tarea;
        public bool exito;
        public object datos;
        public override string ToString() => $"tarea={tarea}, exito={exito}, datos={datos}";
    }

    // Eventos hacia la Deliberativa: traducen los ACLMessage entrantes a datos sin lenguaje FIPA
    public event Action<string, string, string> OnRequestReceived;          // from, tarea, convId
    public event Action<string, bool, string> OnRequestResponseReceived;   // from, acepto, convId
    public event Action<string, string, Vector3, string> OnCFPReceived;     // from, tarea, refPos, convId
    public event Action<string, float, string> OnProposalReceived;         // from, coste, convId
    public event Action<string> OnProposalAccepted;                       // convId
    public event Action<string> OnProposalRejected;                          // convId
    public event Action<string, bool, object, string> OnResultReceived;  // from, exito, datos, convId
    public event Action<string, bool, object, string> OnInformReceived; // from, exito, datos, convId — externo al Contract Net
    public event Action<string> OnFailureReceived;                            // convId
    public event Action<string> OnCancellationReceived;                      // convId
    public event Action OnTimerExpired;                                      // es el momento de intentar ser iniciador de una nueva conversación porque el timer ha expirado

    // Sortea el timer inicial al arrancar la escena
    void Start()
    {
        SortearTimer();
    }

    // Decrementa el timer cada frame y avisa a la Deliberativa cuando llega a cero
    protected override void ActualizarAgente()
    {
        if (timerActual <= 0f) return;
        timerActual -= Time.deltaTime; // Resta el tiempo transcurrido desde el último frame
        if (timerActual <= 0f)
        {
            timerActual = 0f;
            OnTimerExpired?.Invoke();
        }
    }

    // API hacia arriba: métodos que llama la Deliberativa

    // Fase 0 — pregunta a todos los esqueletos si están disponibles para participar.
    // Devuelve el conversationId común para que la Deliberativa lo guarde y lo reutilice en fases posteriores.
    public string EnviarRequest(string tarea, List<AgenteFIPAACL> destinatarios)
    {
        timerActual = TIMER_MAX; // pausa el timer propio: ya hay una conversación en marcha
        string convId = Guid.NewGuid().ToString(); // un único convId compartido por todos los destinatarios
        foreach (AgenteFIPAACL dest in destinatarios)
        {
            if (dest == this) continue; // no se envía el REQUEST a sí mismo
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer; // filtra agentes que no son guardias (ej. cámaras)
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(REQUEST, convId); // todos los REQUEST comparten convId: misma conversación
            msg.content = new ContentRequest { tarea = tarea };
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest); // deposita el mensaje en el inbox del destinatario
        }
        return convId;
    }

    // Reinicia el timer manualmente; usado por la Deliberativa cuando aborta una iniciativa propia
    // (p.ej. nadie respondió AGREE) sin necesidad de generar un INFORM_RESULT artificial.
    public void ReiniciarTimer()
    {
        SortearTimer();
    }

    // Responde AGREE o REFUSE al iniciador según si este agente está libre
    public void ResponderRequest(string convId, bool acepto, AgenteFIPAACL iniciador)
    {
        string performative = acepto ? AGREE : REFUSE; // elige el performative según disponibilidad
        ACLMessage msg = CrearMensaje(performative, convId); // reutiliza el convId del REQUEST recibido
        msg.inReplyTo = convId;
        msg.receivers.Add(iniciador.IdAgente);
        EnviarMensaje(msg, iniciador);
    }

    // Fase 1 — invita a los voluntarios a participar enviando la posición del cofre
    public void EnviarCFP(string convId, List<AgenteFIPAACL> participantes, Vector3 refPos, string tarea)
    {
        foreach (AgenteFIPAACL dest in participantes) // solo llega a los que respondieron AGREE en Fase 0
        {
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(CFP, convId); // mismo convId que el REQUEST: misma conversación
            msg.content = new ContentCFP { tarea = tarea, posicionReferencia = refPos }; // incluye la posición del cofre para que cada uno calcule su coste
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // Envía la oferta de coste (distancia al cofre) al iniciador
    public void EnviarPropuesta(string convId, float coste, AgenteFIPAACL iniciador)
    {
        ACLMessage msg = CrearMensaje(PROPOSE, convId);
        msg.content = new ContentPropose { coste = coste }; // la Deliberativa calcula el coste antes de llamar a esta función
        msg.inReplyTo = convId;
        msg.receivers.Add(iniciador.IdAgente);
        EnviarMensaje(msg, iniciador);
    }

    // Notifica al ganador que ha sido seleccionado para ejecutar la tarea
    public void AceptarPropuesta(string convId, AgenteFIPAACL ganador)
    {
        ACLMessage msg = CrearMensaje(ACCEPT_PROPOSAL, convId); // sin content: el convId identifica de qué contrato se habla
        msg.inReplyTo = convId;
        msg.receivers.Add(ganador.IdAgente);
        EnviarMensaje(msg, ganador);
    }

    // Notifica a un candidato que no fue seleccionado
    public void RechazarPropuesta(string convId, AgenteFIPAACL perdedor)
    {
        ACLMessage msg = CrearMensaje(REJECT_PROPOSAL, convId); // sin content: el convId es suficiente
        msg.inReplyTo = convId;
        msg.receivers.Add(perdedor.IdAgente);
        EnviarMensaje(msg, perdedor);
    }

    // El ganador informa del resultado de la tarea a todos los que recibieron el REQUEST
    public void InformarResultado(string convId, bool exito, object datos, List<AgenteFIPAACL> destinatarios)
    {
        foreach (AgenteFIPAACL dest in destinatarios) // notifica a todos los que participaron en la Fase 0
        {
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue;

            ACLMessage msg = CrearMensaje(INFORM_RESULT, convId);
            msg.content = new ContentInform { tarea = "comprobarCofre", exito = exito, datos = datos }; // exito=false significa que el cofre fue robado
            msg.inReplyTo = convId;
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // REFUSE genérico: rechaza participar en un CFP o REQUEST cuando el agente está ocupado
    public void Rechazar(string convId, AgenteFIPAACL destinatario)
    {
        ACLMessage msg = CrearMensaje(REFUSE, convId); // sin content: el convId identifica qué se rechaza
        msg.inReplyTo = convId;
        msg.receivers.Add(destinatario.IdAgente);
        EnviarMensaje(msg, destinatario);
    }

    // Notificación externa al Contract Net: el ganador avisa a todos los patrulleros del resultado.
    // Usa INFORM (no INFORM_RESULT) para no mezclar con el protocolo Contract Net.
    public void BroadcastInform(string convId, bool exito, object datos)
    {
        foreach (AgenteFIPAACL dest in Todos) // envía a todos los agentes registrados
        {
            if (dest == this) continue; // el ganador no se manda el mensaje a sí mismo
            SkeletonSocialLayer skelDest = dest as SkeletonSocialLayer;
            if (skelDest == null) continue; // solo patrulleros (filtra cámaras y otros)

            ACLMessage msg = CrearMensaje(INFORM, convId);
            msg.content = new ContentInform { tarea = "comprobarCofre", exito = exito, datos = datos };
            msg.receivers.Add(skelDest.IdAgente);
            EnviarMensaje(msg, skelDest);
        }
    }

    // El ganador no pudo completar la tarea; el iniciador lo trata como fallo
    public void Fallar(string convId, AgenteFIPAACL iniciador)
    {
        ACLMessage msg = CrearMensaje(FAILURE, convId); // el iniciador lo tratará igual que un INFORM_RESULT con exito=false
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

            ACLMessage msg = CrearMensaje(CANCEL, convId); // avisa a todos los comprometidos para que aborten
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
                timerActual = TIMER_MAX; // hay una conversación en marcha, pausar el timer propio
                if (msg.content is ContentRequest cr)
                    OnRequestReceived?.Invoke(msg.sender, cr.tarea, msg.conversationId); // la Deliberativa decide si responde AGREE o REFUSE
                else
                    EnviarNotUnderstood(msg); // el content no es del tipo esperado
                break;

            case AGREE:
                OnRequestResponseReceived?.Invoke(msg.sender, true, msg.conversationId); // este agente está disponible
                break;

            case REFUSE:
                OnRequestResponseReceived?.Invoke(msg.sender, false, msg.conversationId); // este agente está ocupado
                break;

            case CFP:
                if (msg.content is ContentCFP cfp)
                    OnCFPReceived?.Invoke(msg.sender, cfp.tarea, cfp.posicionReferencia, msg.conversationId); // la Deliberativa calculará su coste y enviará PROPOSE
                else
                    EnviarNotUnderstood(msg);
                break;

            case PROPOSE:
                if (msg.content is ContentPropose pp)
                    OnProposalReceived?.Invoke(msg.sender, pp.coste, msg.conversationId); // solo lo recibe el Iniciador, que acumula propuestas
                else
                    EnviarNotUnderstood(msg);
                break;

            case ACCEPT_PROPOSAL:
                OnProposalAccepted?.Invoke(msg.conversationId); // este agente ganó: debe ejecutar CheckChestBehaviour
                break;

            case REJECT_PROPOSAL:
                OnProposalRejected?.Invoke(msg.conversationId); // este agente perdió: vuelve a patrullar
                break;

            case INFORM_RESULT:
                if (msg.content is ContentInform inf)
                {
                    if (inf.tarea == "comprobarCofre")
                        SortearTimer(); // la conversación Contract Net terminó, reiniciar timer
                    OnResultReceived?.Invoke(msg.sender, inf.exito, inf.datos, msg.conversationId);
                }
                else
                    EnviarNotUnderstood(msg);
                break;

            case INFORM:
                if (msg.content is ContentInform informMsg)
                {
                    if (informMsg.tarea == "comprobarCofre")
                        SortearTimer(); // notificación externa: la conversación terminó, reiniciar timer
                    OnInformReceived?.Invoke(msg.sender, informMsg.exito, informMsg.datos, msg.conversationId); // la Deliberativa reacciona al resultado
                }
                else
                    EnviarNotUnderstood(msg);
                break;

            case FAILURE:
                OnFailureReceived?.Invoke(msg.conversationId); // el ganador no pudo completar la tarea
                break;

            case CANCEL:
                SortearTimer(); // la conversación fue cancelada, reiniciar el timer
                OnCancellationReceived?.Invoke(msg.conversationId); // la Deliberativa aborta CheckChestBehaviour si estaba activo
                break;

            case NOT_UNDERSTOOD:
                // Ignorado para evitar bucles de NOT_UNDERSTOOD
                break;

            default:
                EnviarNotUnderstood(msg); // performative desconocido
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
        AgenteFIPAACL emisor = Todos.Find(a => a.IdAgente == msgOriginal.sender);// busca el agente emisor en la lista de agentes para enviarle la notificación de NOT_UNDERSTOOD
        if (emisor == null) return;
        ACLMessage respuesta = CrearMensaje(NOT_UNDERSTOOD, msgOriginal.conversationId);// mismo convId para que el emisor sepa a qué mensaje se refiere
        respuesta.inReplyTo = msgOriginal.conversationId;// el emisor puede usar el convId para identificar qué mensaje no fue entendido
        respuesta.receivers.Add(emisor.IdAgente); // se lo envía al emisor para que sepa que su mensaje no fue entendido
        EnviarMensaje(respuesta, emisor); // deposita el mensaje de NOT_UNDERSTOOD en el inbox del emisor
    }
}
