using UnityEngine;
using UnityEngine.SceneManagement; 

// Gestiona la detección de colisiones del arma (Action Subsystem).
public class SwordHitbox : MonoBehaviour
{
    public Collider hitboxCollider;  // El disparador (trigger) físico del arma.
    public string ladronTag = "Ladron"; // Etiqueta para identificar al objetivo.

    private bool enabledHit; // Controla si el arma puede hacer daño en el frame actual.
    private bool alreadyHit; // Evita registrar múltiples impactos en un solo movimiento.

    void Awake()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        EnableHitbox(false); // El arma comienza desactivada por seguridad.
    }

    // Activa o desactiva el daño, controlado externamente por el AgentActuator.
    public void EnableHitbox(bool enable)
    {
        enabledHit = enable;
        alreadyHit = false; // Reinicia el estado de impacto para el nuevo ataque.

        if (hitboxCollider != null)
            hitboxCollider.enabled = enable;
    }

    // Detecta la colisión física con el objetivo.
    void OnTriggerEnter(Collider other)
    {
        if (!enabledHit || alreadyHit) return;

        if (other.CompareTag(ladronTag))
        {
            Debug.Log("¡El jugador ha sido alcanzado por la espada!");
            alreadyHit = true; 
            
            // Consecuencia final: Reinicio del nivel al detectar el impacto con el Ladrón.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}