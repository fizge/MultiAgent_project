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
    private MovementSensor sensorMovimiento;

    private float anguloGiroActual = 0f;
    private float direccionGiro = 1f;

    void Awake()
    {
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();
    }

    public override ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();
        prop.solicitaControl = true;

        // Si detecta al ladrón -> perseguir y atacar.
        if (sensorVision.PuedeVerLadron())
        {
            Vector3 posEnemigo = sensorVision.ObtenerPosicionObjetivo();
            prop.destinoMovimiento = posEnemigo;
            prop.velocidad = velocidadCorrer;
            prop.aceleracion = aceleracionCorrer;
            prop.distanciaParada = distanciaAtaque;
            prop.corriendo = true;

            if (sensorMovimiento.EstaARangoFisico(posEnemigo, distanciaAtaque))
            {
                prop.atacar = true;
                prop.objetivoAtaque = posEnemigo;
            }
        }
        // Sin amenaza -> oscilar la vista.
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
