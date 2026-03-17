using UnityEngine;

// Reactive Layer para guardias estáticos.
// Gestiona: persecución y ataque al avistar al ladrón, vigilancia con oscilación de vista.
public class GuardianReactiveLayer : ReactiveLayer
{
    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;
    public float distanciaAtaque = 1.5f;
    public float velocidadRotacionIdle = 30f;
    public float anguloVigilancia = 90f;

    private VisionSensor sensorVision;

    private float anguloGiroActual = 0f;
    private float direccionGiro = 1f;

    // Estado interno actualizado por eventos de los sensores.
    private bool ladronVisible = false;
    private bool dentroDeRango = false;

    void Awake()
    {
        sensorVision = GetComponent<VisionSensor>();

        sensorVision.OnLadronAvistado += () => ladronVisible = true;
        sensorVision.OnLadronPerdido  += () => ladronVisible = false;
        sensorVision.OnLadronMuyCerca += () => dentroDeRango = true;
        sensorVision.OnLadronLejos    += () => dentroDeRango = false;
    }

    public override ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();
        prop.solicitaControl = true;

        // Regla 1: ladrón visible -> perseguir y atacar.
        if (ladronVisible)
        {
            Vector3 posEnemigo = sensorVision.PosicionLadron;
            prop.destinoMovimiento = posEnemigo;
            prop.velocidad = velocidadCorrer;
            prop.aceleracion = aceleracionCorrer;
            prop.distanciaParada = distanciaAtaque;
            prop.corriendo = true;

            if (dentroDeRango)
            {
                prop.atacar = true;
                prop.objetivoAtaque = posEnemigo;
            }
        }
        // Regla 2: sin amenaza -> oscilar la vista.
        else
        {
            float rotacionFrame = velocidadRotacionIdle * Time.deltaTime * direccionGiro;
            anguloGiroActual += rotacionFrame;
            if (anguloGiroActual >= anguloVigilancia) direccionGiro = -1f;
            else if (anguloGiroActual <= -anguloVigilancia) direccionGiro = 1f;

            prop.rotarEnLugar = true;
            prop.gradosRotacion = rotacionFrame;
            prop.destinoMovimiento = transform.position;
        }

        return prop;
    }
}
