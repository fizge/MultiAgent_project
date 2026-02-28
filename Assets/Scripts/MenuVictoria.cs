using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuVictoria : MonoBehaviour
{
    public void VolverAJugar()
    {
        // Carga tu escena principal (pon el nombre real de tu nivel)
        SceneManager.LoadScene("Castle"); 
    }

    public void SalirDelJuego()
    {
        Debug.Log("Saliendo...");
        Application.Quit();
    }
}