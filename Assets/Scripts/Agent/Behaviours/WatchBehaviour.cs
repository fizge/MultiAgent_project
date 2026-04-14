// Comportamiento de vigilancia estática: oscila la vista del guardia en un arco configurable.
using UnityEngine;

public class WatchBehaviour : IBehaviour
{
    public float rotationSpeed = 30f;
    public float watchAngle = 90f;

    private Transform agentTransform;
    private float anguloActual;
    private float direccionGiro = 1f;

    public void Initialize(Transform transform)
    {
        agentTransform = transform;
    }

    public ActionProposal Evaluate()
    {
        float rotacionFrame = rotationSpeed * Time.deltaTime * direccionGiro;
        anguloActual += rotacionFrame;
        
        // Si se alcanza el límite del arco, invertir el sentido de giro.
        if (anguloActual >= watchAngle) direccionGiro = -1f;
        else if (anguloActual <= -watchAngle) direccionGiro =  1f;

        return new ActionProposal
        {
            solicitaControl = true,
            rotarEnLugar = true,
            gradosRotacion = rotacionFrame,
            destinoMovimiento = agentTransform.position
        };
    }
}
