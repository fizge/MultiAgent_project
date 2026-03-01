using UnityEngine;

public class ActionProposal
{
    public bool solicitaControl; 
    
    // Movimiento
    public Vector3 destinoMovimiento;
    public float velocidad;
    public float aceleracion;
    public float distanciaParada;
    public bool corriendo;
    
    // Rotación
    public bool rotarEnLugar;
    public float gradosRotacion;
    
    // Combate
    public bool atacar;
    public Vector3 objetivoAtaque;
}