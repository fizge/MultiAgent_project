// Reactive Layer para guardias estáticos.
// Prioridad: AttackBehaviour > ChaseBehaviour > WatchBehaviour.
public class GuardianReactiveLayer : ReactiveLayer
{
    public float velocidadCorrer       = 5f;
    public float aceleracionCorrer     = 8f;
    public float distanciaAtaque       = 1.5f;
    public float velocidadRotacionIdle = 30f;
    public float anguloVigilancia      = 90f;

    private AttackBehaviour attack;
    private ChaseBehaviour  chase;
    private WatchBehaviour  watch;

    void Awake()
    {
        var sensorVision = GetComponent<VisionSensor>();

        attack = new AttackBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        attack.Initialize(sensorVision);

        chase = new ChaseBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        chase.Initialize(sensorVision);

        watch = new WatchBehaviour { rotationSpeed = velocidadRotacionIdle, watchAngle = anguloVigilancia };
        watch.Initialize(transform);
    }

    public override ActionProposal GenerarPropuesta() =>
        attack.Evaluate() ?? chase.Evaluate() ?? watch.Evaluate();
}
