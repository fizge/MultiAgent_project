using UnityEngine;

public class ZonaSalida : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Verificamos que sea el jugador el que toca el cubo
        if (other.CompareTag("Ladron"))
        {
            // Le decimos al GameManager que el jugador intenta salir
            GameManager.Instance.IntentarFinalizarMision();
        }
    }
}