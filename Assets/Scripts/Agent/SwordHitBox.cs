using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para recargar la escena

public class SwordHitbox : MonoBehaviour
{
    [Header("Setup")]
    public Collider hitboxCollider;   // el collider del arma
    public string ladronTag = "Ladron";

    private bool enabledHit;
    private bool alreadyHit;          // evita múltiples impactos en el mismo swing

    void Awake()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        EnableHitbox(false);
    }

    public void EnableHitbox(bool enable)
    {
        enabledHit = enable;
        alreadyHit = false;

        if (hitboxCollider != null)
            hitboxCollider.enabled = enable;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!enabledHit || alreadyHit) return;

        if (other.CompareTag(ladronTag))
        {
            Debug.Log("¡El jugador ha sido alcanzado por la espada!");
            alreadyHit = true;
            
            // Reiniciamos la escena directamente desde aquí
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}