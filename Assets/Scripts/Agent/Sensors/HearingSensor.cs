using System;
using UnityEngine;

// Proporciona el oído al agente: detecta NoiseEmitter cercanos cuyo radio solape con el de escucha.
// Distingue si el ruido viene del ladrón o de otro esqueleto y dispara eventos separados.
public class HearingSensor : MonoBehaviour
{
    public float radioEscucha    = 2f;
    public float imprecisionRuido = 1.5f; // radio del offset aleatorio aplicado a la posición oída

    // Eventos de detección del ladrón por sonido.
    public event Action<Vector3> OnLadronEscuchado;
    public event Action          OnLadronNoEscuchado;

    // Eventos de detección de otro esqueleto corriendo.
    public event Action<Vector3> OnEsqueletoEscuchado;
    public event Action          OnEsqueletoNoEscuchado;

    // Última posición conocida por sonido de cada tipo.
    public Vector3 PosicionRuidoLadron  { get; private set; }
    public Vector3 PosicionRuidoEsqueleto { get; private set; }

    private bool ladronEscuchadoAntes  = false;
    private bool esqueletoEscuchadoAntes = false;

    void Update()
    {
        bool ladronDetectado  = false;
        bool esqueletoDetectado = false;

        foreach (NoiseEmitter emisor in NoiseEmitter.todos)
        {
            // No detectarse a uno mismo.
            if (emisor.gameObject == gameObject) continue;

            // Sin ruido no hay detección.
            if (emisor.radioActual <= 0f) continue;

            float distancia = Vector3.Distance(transform.position, emisor.transform.position);

            // Detección cuando los dos radios se solapan.
            if (distancia > radioEscucha + emisor.radioActual) continue;

            if (emisor.CompareTag("Ladron"))
            {
                ladronDetectado     = true;
                PosicionRuidoLadron = emisor.transform.position + AplicarImprecision();
            }
            else if (emisor.CompareTag("Esqueleto") && emisor.radioActual >= emisor.radioCorriendo)
            {
                esqueletoDetectado     = true;
                PosicionRuidoEsqueleto = emisor.transform.position + AplicarImprecision();
            }
        }

        // Disparar eventos en cambio de estado (igual que VisionSensor).
        if (ladronDetectado  && !ladronEscuchadoAntes)  { Debug.Log($"[HearingSensor] {name} escucha al ladrón");         OnLadronEscuchado?.Invoke(PosicionRuidoLadron); }
        if (!ladronDetectado &&  ladronEscuchadoAntes)  { Debug.Log($"[HearingSensor] {name} deja de escuchar al ladrón"); OnLadronNoEscuchado?.Invoke(); }

        if (esqueletoDetectado  && !esqueletoEscuchadoAntes) { Debug.Log($"[HearingSensor] {name} escucha a un esqueleto corriendo en {PosicionRuidoEsqueleto}");         OnEsqueletoEscuchado?.Invoke(PosicionRuidoEsqueleto); }
        if (!esqueletoDetectado &&  esqueletoEscuchadoAntes) { Debug.Log($"[HearingSensor] {name} deja de escuchar al esqueleto"); OnEsqueletoNoEscuchado?.Invoke(); }

        ladronEscuchadoAntes  = ladronDetectado;
        esqueletoEscuchadoAntes = esqueletoDetectado;
    }

    // Devuelve un offset aleatorio horizontal dentro del radio de imprecisión.
    Vector3 AplicarImprecision()
    {
        Vector2 offsetAleatorio = UnityEngine.Random.insideUnitCircle * imprecisionRuido;
        return new Vector3(offsetAleatorio.x, 0f, offsetAleatorio.y);
    }

    // Dibuja el radio de escucha en la Scene View cuando el objeto está seleccionado.
    void OnDrawGizmosSelected()
    {
        // Radio de escucha — cian
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radioEscucha);
    }
}
