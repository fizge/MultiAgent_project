using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para cambiar de escena

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public bool hasLoot = false;

    [Header("Configuración de Escenas")]
    public string nombreEscenaVictoria = "Victoria"; // Asegúrate de que se llame así en Build Settings

    private void Awake()
    {
        // Singleton sencillo
        if (Instance == null)
        {
            Instance = this;
            // Opcional: Don'tDestroyOnLoad(gameObject); si quieres que persista entre niveles
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayerLooted()
    {
        hasLoot = true;
        Debug.Log("El ladrón ha robado el cofre!");
        
        // Aquí puedes añadir eventos visuales, música de persecución, etc.
    }

    // Esta función la llamará el Cubo/Trigger de salida
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
        // Carga la escena de victoria
        SceneManager.LoadScene(nombreEscenaVictoria);
        
        // Si quieres que el juego se cierre directamente en una build:
        // Application.Quit();
    }
}