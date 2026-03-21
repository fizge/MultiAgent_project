// Comportamiento de ataque: activo cuando el ladrón está visible Y dentro del rango de ataque.
public class AttackBehaviour : IBehaviour
{
    public float speed        = 5f;
    public float acceleration = 8f;
    public float stopDistance = 1.5f;

    private VisionSensor sensor;
    private bool ladronVisible;
    private bool dentroDeRango;

    public void Initialize(VisionSensor visionSensor)
    {
        sensor = visionSensor;
        sensor.OnLadronAvistado += () => ladronVisible = true;
        sensor.OnLadronPerdido  += () => ladronVisible = false;
        sensor.OnLadronMuyCerca += () => dentroDeRango = true;
        sensor.OnLadronLejos    += () => dentroDeRango = false;
    }

    public ActionProposal Evaluate()
    {
        if (!ladronVisible || !dentroDeRango) return null;

        return new ActionProposal
        {
            solicitaControl   = true,
            destinoMovimiento = sensor.PosicionLadron,
            velocidad         = speed,
            aceleracion       = acceleration,
            distanciaParada   = stopDistance,
            corriendo         = true,
            atacar            = true,
            objetivoAtaque    = sensor.PosicionLadron
        };
    }
}
