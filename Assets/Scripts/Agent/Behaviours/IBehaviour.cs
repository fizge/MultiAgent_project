// Interfaz común para todos los comportamientos encapsulados del agente.
public interface IBehaviour
{
    // Devuelve una propuesta de acción, o null si el comportamiento no está activo.
    ActionProposal Evaluate();
}
