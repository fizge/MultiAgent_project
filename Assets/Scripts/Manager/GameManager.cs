using UnityEngine;
using UnityEngine.SceneManagement; 

// Singleton que lleva la cuenta del estado de la misión.
// Sabe si el ladrón ya tiene el botín y decide cuándo se ha ganado.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public bool hasLoot = false;
    public string nombreEscenaVictoria = "Victoria";

    private void Awake()
    {
        // Solo puede existir uno; si ya hay otro, este se destruye.
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // El cofre llama a esto cuando el ladrón lo recoge.
    public void PlayerLooted()
    {
        hasLoot = true;
        Debug.Log("El ladrón ha robado el cofre!");
    }

    // La zona de salida llama a esto; solo deja pasar si ya tiene el botín.
    public void IntentarFinalizarMision()
    {
        if (hasLoot)
        {
            Debug.Log("¡Misión cumplida! El ladrón escapó con el botín.");
            TerminarJuego();
        }
        else
        {
            Debug.Log("¡Aún no tienes el cofre! No puedes irte con las manos vacías.");
        }
    }

    private void TerminarJuego()
    {
        SceneManager.LoadScene(nombreEscenaVictoria);
    }
}