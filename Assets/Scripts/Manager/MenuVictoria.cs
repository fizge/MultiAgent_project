using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuVictoria : MonoBehaviour
{
    public void VolverAJugar()
    {
        SceneManager.LoadScene("Castle"); 
    }

    public void SalirDelJuego()
    {
        Debug.Log("Saliendo...");
        Application.Quit();
    }
}