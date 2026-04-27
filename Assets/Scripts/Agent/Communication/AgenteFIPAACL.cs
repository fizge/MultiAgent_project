using System;
using System.Collections.Generic;
using UnityEngine;

// MonoBehaviour abstracto que representa un agente con capacidad de comunicación FIPA-ACL.
// Todas las entidades comunicantes (guardias via GuardSocialLayer, cámaras via CameraSocialLayer)
// deben heredar de esta clase.
public abstract class AgenteFIPAACL : MonoBehaviour
{
    // Registro estático de todas las instancias activas en escena.
    // Permite localizar agentes sin referencias directas en el Inspector.
    public static List<AgenteFIPAACL> Todos { get; private set; } = new List<AgenteFIPAACL>();

    // Número máximo de mensajes procesados por frame para evitar congeladas.
    public int mensajesPorFrame = 2;

    // Cola interna de mensajes recibidos pendientes de procesar.
    private Queue<ACLMessage> inbox = new Queue<ACLMessage>();

    // Identificador único del agente, usado como sender/receiver en los mensajes.
    public string IdAgente => name;

    void OnEnable()
    {
        Todos.Add(this);
    }

    void OnDisable()
    {
        Todos.Remove(this);
    }

    void Update()
    {
        ProcesarInbox();
        ActualizarAgente();
    }

    // Procesa la cola de inbox procesando como máximo mensajesPorFrame mensajes.
    // Los sobrantes esperan al frame siguiente.
    private void ProcesarInbox()
    {
        int procesados = 0;
        while (inbox.Count > 0 && procesados < mensajesPorFrame)
        {
            ACLMessage msg = inbox.Dequeue();
            ProcesarMensaje(msg);
            procesados++;
        }
    }

    // Hook ( método que existe solo para que las subclases lo sobreescriban y añadan su comportamiento), para que las subclases puedan añadir lógica en Update sin sobreescribir.
    // Se pone hook en vez de abstract porque socialcamera no necesita lógica en Update, pero guardias sí.
    protected virtual void ActualizarAgente() { }

    // Deposita un mensaje en el inbox de este agente.
    // Llamado por el emisor; no lo llama nunca el propio agente sobre sí mismo.
    public void RecibirMensaje(ACLMessage msg)
    {
        inbox.Enqueue(msg);
    }

    // Envía un mensaje unicast al destinatario indicado depositándolo en su inbox.
    // Para enviar a varios agentes, llamar en bucle desde la capa Social.
    public void EnviarMensaje(ACLMessage msg, AgenteFIPAACL destinatario)
    {
        if (destinatario == null) return;
        destinatario.RecibirMensaje(msg);
    }

    // Construye un ACLMessage poniendo ya el id del sender.
    // Si no se proporciona conversationId, genera uno nuevo (un nuevo GUID de 32 caracteres hexadecimales separados en grupos por guiones).
    public ACLMessage CrearMensaje(string performative, string conversationId = null)
    {
        ACLMessage msg = new ACLMessage();
        msg.performative = performative;
        msg.sender = IdAgente;
        msg.conversationId = string.IsNullOrEmpty(conversationId) ? Guid.NewGuid().ToString() : conversationId;
        return msg;
    }

    // Cada subclase define cómo reacciona al recibir un mensaje de su inbox.
    // Aquí si que es abstract porque cada tipo de agente (guardia, cámara) procesa los mensajes de forma completamente diferente, no hay nada de lógica común.
    protected abstract void ProcesarMensaje(ACLMessage msg);
}
