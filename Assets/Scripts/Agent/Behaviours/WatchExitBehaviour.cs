using UnityEngine;
using UnityEngine.AI;

// Comportamiento de vigilancia de salida: el guardia detecta que el cofre fue robado
// y corre hacia la zona de salida para interceptar al ladrón.
public class WatchExitBehaviour : IBehaviour
{
    public float speed       = 4f;
    public float acceleration = 4f;

    public float radioImprecision = 3f;

    private Transform zonaSalida;
    private bool cofresDesaparecido = false;
    private Vector3 destinoConOffset;

    public void Initialize(VisionSensor vision, MovementSensor movement, Transform zonaSalida)
    {
        this.zonaSalida = zonaSalida;
        vision.OnCofresDesaparecido += OnCofresDesaparecido;
        movement.OnDestinoAlcanzado += () => cofresDesaparecido = false;
    }

    // Calcula el offset una sola vez al activarse, para que el destino sea estable mientras corre.
    private void OnCofresDesaparecido()
    {
        Vector2 offset = Random.insideUnitCircle * radioImprecision;
        // Z solo se suma (nunca se resta) para no cruzar la puerta hacia el exterior.
        Vector3 candidato = zonaSalida.position + new Vector3(offset.x, 0f, Mathf.Abs(offset.y));

        // Ajustar al punto válido más cercano del NavMesh.
        if (NavMesh.SamplePosition(candidato, out NavMeshHit hit, radioImprecision, NavMesh.AllAreas))
            destinoConOffset = hit.position;
        else
            destinoConOffset = zonaSalida.position;

        cofresDesaparecido = true;
    }

    public ActionProposal Evaluate()
    {
        if (!cofresDesaparecido) return null;

        Debug.Log("[WatchExitBehaviour] Corriendo hacia la salida");
        return new ActionProposal
        {
            solicitaControl   = true,
            destinoMovimiento = destinoConOffset,
            velocidad         = speed,
            aceleracion       = acceleration,
            distanciaParada   = 0f,
            corriendo         = true
        };
    }
}
