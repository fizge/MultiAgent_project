using UnityEngine;

// Capa reactiva de la cámara de vigilancia.
// Suscribe a VisionSensor y notifica a CameraDeliberativeLayer cuando detecta al ladrón.
// Solo lanza el protocolo una vez por avistamiento: espera a perder al ladrón antes de relanzar.
public class CameraReactiveLayer : MonoBehaviour
{
    private VisionSensor sensor;
    private CameraDeliberativeLayer deliberativa;
    private bool protocoloActivo;

    void Awake()
    {
        sensor = GetComponent<VisionSensor>();
        deliberativa = GetComponent<CameraDeliberativeLayer>();

        sensor.OnLadronAvistado += OnLadronAvistado; // Suscribe al evento de avistamiento para iniciar el protocolo
        sensor.OnLadronPerdido += OnLadronPerdido; // Suscribe al evento de pérdida para permitir relanzar el protocolo en el próximo avistamiento
    }

    private void OnLadronAvistado()
    {
        if (protocoloActivo) return; 
        protocoloActivo = true;
        deliberativa.IniciarProtocolo(sensor.PosicionLadron);
    }

    private void OnLadronPerdido()
    {
        // Permite relanzar el protocolo en el próximo avistamiento.
        // El protocolo en curso sigue su curso hasta que la deliberativa lo cierre.
        protocoloActivo = false;
    }
}
