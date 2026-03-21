// Reactive Layer para guardias patrulleros.
// Prioridad: AttackBehaviour > ChaseBehaviour > PatrolBehaviour.
public class PatrolReactiveLayer : ReactiveLayer
{
    public float velocidadCorrer     = 5f;
    public float aceleracionCorrer   = 8f;
    public float distanciaAtaque     = 1.5f;
    public float velocidadCaminar    = 2f;
    public float aceleracionCaminar  = 2f;
    public float margenPared         = 1.5f;

    private AttackBehaviour attack;
    private ChaseBehaviour  chase;
    private PatrolBehaviour patrol;

    void Awake()
    {
        VisionSensor sensorVision      = GetComponent<VisionSensor>();
        MovementSensor sensorMovimiento = GetComponent<MovementSensor>();

        attack = new AttackBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        attack.Initialize(sensorVision);

        chase = new ChaseBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        chase.Initialize(sensorVision);

        patrol = new PatrolBehaviour { speed = velocidadCaminar, acceleration = aceleracionCaminar, wallMargin = margenPared };
        patrol.Initialize(sensorVision, sensorMovimiento, transform);
    }

    public override ActionProposal GenerarPropuesta() =>
        attack.Evaluate() ?? chase.Evaluate() ?? patrol.Evaluate();
}
