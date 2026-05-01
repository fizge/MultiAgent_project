using UnityEngine;

// Reactive Layer para guardias patrulleros.
// Prioridad: Attack > Chase > Investigate > FollowGuard > Patrol.
public class PatrolReactiveLayer : ReactiveLayer
{
    public float velocidadCorrer     = 5f;
    public float aceleracionCorrer   = 8f;
    public float distanciaAtaque     = 1.5f;
    public float velocidadCaminar    = 2f;
    public float aceleracionCaminar  = 2f;
    public float margenPared         = 1.5f;

    private AttackBehaviour      attack;
    private ChaseBehaviour       chase;
    private InvestigateBehaviour investigate;
    private FollowGuardBehaviour followGuard;
    private PatrolBehaviour      patrol;
    public PatrolBehaviour Patrol => patrol;

    // Para cada comportamiento, se crean y se inicializan suscribiendo los eventos del sensor
    void Awake()
    {
        VisionSensor   sensorVision     = GetComponent<VisionSensor>();
        MovementSensor sensorMovimiento = GetComponent<MovementSensor>();
        HearingSensor  sensorEscucha    = GetComponent<HearingSensor>();

        attack = new AttackBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        attack.Initialize(sensorVision);

        chase = new ChaseBehaviour { speed = velocidadCorrer, acceleration = aceleracionCorrer, stopDistance = distanciaAtaque };
        chase.Initialize(sensorVision);

        investigate = new InvestigateBehaviour { speed = velocidadCaminar, acceleration = aceleracionCaminar };
        investigate.Initialize(sensorEscucha, sensorVision);

        followGuard = new FollowGuardBehaviour { speed = velocidadCaminar, acceleration = aceleracionCaminar };
        followGuard.Initialize(sensorEscucha, sensorVision);

        patrol = new PatrolBehaviour { speed = velocidadCaminar, acceleration = aceleracionCaminar, wallMargin = margenPared };
        patrol.Initialize(sensorVision, sensorMovimiento, transform);
    }

    // Se genera la propuesta según prioridad de la acción
    public override ActionProposal GenerarPropuesta() =>
        attack.Evaluate() ?? chase.Evaluate() ?? investigate.Evaluate() ?? followGuard.Evaluate() ?? patrol.Evaluate();
}
