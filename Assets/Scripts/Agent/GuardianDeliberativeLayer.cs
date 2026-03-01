using UnityEngine;

public class GuardianDeliberativeLayer : DeliberativeLayer
{
    [Header("Vigilancia")]
    public float velocidadRotacionIdle = 30f;
    public float anguloVigilancia = 90f;

    private float anguloGiroActual = 0f;
    private float direccionGiro = 1f;

    public override ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();
        prop.solicitaControl = true;

        float rotacionFrame = velocidadRotacionIdle * Time.deltaTime * direccionGiro;
        anguloGiroActual += rotacionFrame;

        if (anguloGiroActual >= anguloVigilancia) direccionGiro = -1f;
        else if (anguloGiroActual <= -anguloVigilancia) direccionGiro = 1f;

        prop.rotarEnLugar = true;
        prop.gradosRotacion = rotacionFrame;
        prop.destinoMovimiento = transform.position;
        return prop;
    }
}