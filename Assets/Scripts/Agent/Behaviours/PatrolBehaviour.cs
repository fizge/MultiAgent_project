// Comportamiento de patrulla: mueve al guardia entre dos puntos calculados por escaneo de direcciones.
using UnityEngine;

public class PatrolBehaviour : IBehaviour
{
    public float speed = 2f;
    public float acceleration = 2f;
    public float wallMargin = 1.5f;

    private Transform agentTransform;
    private Vector3 puntoA, puntoB, destinoActual;
    private bool destinoAlcanzado;

    public void Initialize(VisionSensor visionSensor, MovementSensor movementSensor, Transform transform)
    {
        agentTransform = transform;
        visionSensor.OnEscaneoCompletado += PlanificarNuevaRuta; 
        movementSensor.OnDestinoAlcanzado += () => destinoAlcanzado = true;
    }

    public ActionProposal Evaluate()
    {
        // Si el destino actual fue alcanzado, alterna al otro punto.
        if (destinoAlcanzado) { CambiarDestino(); destinoAlcanzado = false; }

        return new ActionProposal
        {
            solicitaControl = true,
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
        destinoAlcanzado = false;
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
    }

    // Alterna el destino entre puntoA y puntoB.
    private void CambiarDestino() =>
        // Si el agente está cerquísima del puntoA, el destino pasa a ser el puntoB, sino, puntoA
        destinoActual = (Vector3.SqrMagnitude(destinoActual - puntoA) < 0.1f) ? puntoB : puntoA;
}
