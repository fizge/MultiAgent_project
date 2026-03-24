// Script para manejar el juego, sabe si el ladrón ya tiene el botín y decide cuándo se ha ganado.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // Captura del último frame del juego; MenuVictoria la usa como fondo difuminado.
    public static Texture2D capturedScreenshot;

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
            StartCoroutine(CapturarYCargar());
        }
        else
        {
            Debug.Log("¡Aún no tienes el cofre! No puedes irte con las manos vacías.");
        }
    }

    // Espera al final del frame para que la imagen esté renderizada, captura y carga.
    private IEnumerator CapturarYCargar()
    {
        yield return new WaitForEndOfFrame();
        capturedScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
        SceneManager.LoadScene(nombreEscenaVictoria);
    }
}