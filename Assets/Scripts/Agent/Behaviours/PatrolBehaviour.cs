// Comportamiento de patrulla: mueve al guardia entre dos puntos calculados por escaneo de direcciones.
// No es de alta prioridad (esPrioridadAlta = false), por lo que el ControlSubsystem cede a la
// Deliberativa cuando este behaviour es el único activo en la Reactiva.
using UnityEngine;

public class PatrolBehaviour : IBehaviour
{
    public float speed = 2f;
    public float acceleration = 2f;
    public float wallMargin = 1.5f;
    public float velocidadAlerta = 3f;

    public void ActivarModoAlerta()
    {
        speed = velocidadAlerta;
    }

    private Transform agentTransform;
    private Vector3 puntoA, puntoB, destinoActual;
    private bool tieneDestino; // flag explícito para evitar depender de Vector3.zero

    public void Initialize(VisionSensor visionSensor, MovementSensor movementSensor, Transform transform)
    {
        agentTransform = transform;
        visionSensor.OnEscaneoCompletado += PlanificarNuevaRuta;
        movementSensor.OnDestinoAlcanzado += AlternarDestino;
    }

    public ActionProposal Evaluate()
    {
        if (!tieneDestino) return null; // aún no se ha calculado ningún destino

        return new ActionProposal
        {
            solicitaControl = true,
            esPrioridadAlta = false, // no urgente: el ControlSubsystem puede ceder a la Deliberativa
            destinoMovimiento = destinoActual,
            velocidad = speed,
            aceleracion = acceleration,
            distanciaParada = 0f,
            corriendo = false
        };
    }

    // Recibe los datos del escaneo del sensor y calcula los puntos de patrulla.
    private void PlanificarNuevaRuta(Vector3[] direcciones, float[] distancias)
    {
        puntoA = agentTransform.position;
        float maxDistancia = 0f;
        Vector3 mejorPuntoB = puntoA;

        for (int i = 0; i < direcciones.Length; i++)
        {
            float distanciaLibre = distancias[i];
            if (distanciaLibre > wallMargin)
            {
                distanciaLibre -= wallMargin;
                if (distanciaLibre > maxDistancia)
                {
                    maxDistancia = distanciaLibre;
                    mejorPuntoB = puntoA + direcciones[i] * distanciaLibre;
                }
            }
        }

        puntoB = mejorPuntoB;
        destinoActual = puntoB;
        tieneDestino = true;
    }

    // Alterna el destino entre puntoA y puntoB al llegar al actual.
    private void AlternarDestino()
    {
        // Si el agente está cerquísima del puntoA, el destino pasa a ser el puntoB, sino, puntoA.
        destinoActual = (Vector3.SqrMagnitude(destinoActual - puntoA) < 0.1f) ? puntoB : puntoA;
    }
}
