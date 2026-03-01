using UnityEngine;

[RequireComponent(typeof(VisionSensor), typeof(MovementSensor))]
public class ReactiveLayer : MonoBehaviour
{
    [Header("Instintos (No cambiar valores)")]
    public float velocidadCorrer = 5f;
    public float aceleracionCorrer = 8f;
    public float distanciaAtaque = 1.5f;

    private VisionSensor sensorVision;
    private MovementSensor sensorMovimiento;

    void Awake()
    {
        sensorVision = GetComponent<VisionSensor>();
        sensorMovimiento = GetComponent<MovementSensor>();
    }

    public ActionProposal GenerarPropuesta()
    {
        ActionProposal prop = new ActionProposal();

        if (sensorVision.PuedeVerLadron())
        {
            prop.solicitaControl = true;
            Vector3 posEnemigo = sensorVision.ObtenerPosicionObjetivo();
            
            prop.destinoMovimiento = posEnemigo;
            prop.velocidad = velocidadCorrer;
            prop.aceleracion = aceleracionCorrer;
            prop.distanciaParada = distanciaAtaque;
            prop.corriendo = true;

            // USANDO EL SENSOR: Preguntamos si estamos físicamente cerca
            if (sensorMovimiento.EstaARangoFisico(posEnemigo, distanciaAtaque))
            {
                prop.atacar = true;
                prop.objetivoAtaque = posEnemigo;
            }
        }
        return prop;
    }
}