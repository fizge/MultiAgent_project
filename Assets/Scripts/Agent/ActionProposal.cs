using UnityEngine;

// Esta clase actúa como un "mensaje" o "paquete de datos" que permite a las capas (Reactiva o Deliberativa) 
// enviar sus propuestas de acción al Árbitro (ControlSubsystem) para que este decida cuál ejecutar.
public class ActionProposal
{
    // Indica si la capa quiere tomar el mando del agente 
    public bool solicitaControl; 
    
    // Parámetros propuestos para el desplazamiento del NavMeshAgent
    public Vector3 destinoMovimiento;
    public float velocidad;
    public float aceleracion;
    public float distanciaParada;
    public bool corriendo;
    
    // Parámetros propuestos para la vigilancia estática (usado por el Guardián)
    public bool rotarEnLugar;
    public float gradosRotacion;
    
    // Parámetros propuestos para la ejecución del ataque
    public bool atacar;
    public Vector3 objetivoAtaque;
}