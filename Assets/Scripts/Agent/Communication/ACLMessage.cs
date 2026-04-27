using System.Collections.Generic;

// Contenedor de datos que representa un mensaje FIPA-ACL.
// Clase plana sin lógica: solo almacena los campos del protocolo.
public class ACLMessage
{
    // Acto comunicativo (ej. "cfp", "propose", "accept-proposal").
    public string performative;

    // Identificador del agente emisor.
    public string sender;

    // Lista de destinatarios (en esta implementación siempre un único elemento — unicast).
    public List<string> receivers = new List<string>();

    // Contenido del mensaje. Su tipo real lo conoce solo la capa Social.
    public object content;

    // GUID asignado por el Iniciador al abrir la conversación.
    // Todos los mensajes de una misma negociación comparten este id.
    public string conversationId;

    // conversationId del mensaje al que responde, para encadenar turnos.
    public string inReplyTo;

    // Protocolo usado. Siempre "fipa-contract-net" en este proyecto.
    public string protocol = "fipa-contract-net";

    // Tiempo absoluto de juego (Time.time) antes del cual se espera respuesta.
    public float replyBy;


