// Reactive Layer para guardias estáticos.
// Prioridad: Attack > Chase > Investigate > FollowGuard > Watch.
public class GuardianReactiveLayer : ReactiveLayer
{
    public float velocidadCorrer       = 5f;
    public float aceleracionCorrer     = 8f;
    public float distanciaAtaque       = 1.5f;
    public float velocidadCaminar      = 2f;
    public float aceleracionCaminar    = 2f;
    public float velocidadRotacionIdle = 30f;
    public float anguloVigilancia      = 90f;

    private AttackBehaviour      attack;
    private ChaseBehaviour       chase;
    private InvestigateBehaviour investigate;
    private FollowGuardBehaviour followGuard;
    private WatchBehaviour       watch;

    void Awake()
    {
        VisionSensor  sensorVision  = GetComponent<VisionSensor>();
        HearingSensor sensorEscucha = GetComponent<HearingSensor>();

        attack = new AttackBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        attack.Initialize(sensorVision);

        chase = new ChaseBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        chase.Initialize(sensorVision);

        investigate = new InvestigateBehaviour { speed = velocidadCaminar, acceleration = aceleracionCaminar };
        investigate.Initialize(sensorEscucha, sensorVision);

        followGuard = new FollowGuardBehaviour { speed = velocidadCaminar, acceleration = aceleracionCaminar };
        followGuard.Initialize(sensorEscucha, sensorVision);

        watch = new WatchBehaviour { rotationSpeed = velocidadRotacionIdle, watchAngle = anguloVigilancia };
        watch.Initialize(transform);
    }

    public override ActionProposal GenerarPropuesta() =>
        attack.Evaluate() ?? chase.Evaluate() ?? investigate.Evaluate() ?? followGuard.Evaluate() ?? watch.Evaluate();
}
