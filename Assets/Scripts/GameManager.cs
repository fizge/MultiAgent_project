using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public bool hasLoot = false;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayerLooted()
    {
        hasLoot = true;
        Debug.Log("El ladrón ha robado el cofre!");

        // Aquí luego activaremos modo peligro
    }
}
