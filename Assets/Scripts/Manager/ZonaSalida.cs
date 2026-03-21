using UnityEngine;

// Trigger invisible que marca la salida del castillo.
// Cuando el ladrón entra, avisa al GameManager para ver si puede escapar.
public class ZonaSalida : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ladron"))
            GameManager.Instance.IntentarFinalizarMision();
    }
}