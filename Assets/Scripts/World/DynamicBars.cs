using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gestiona una rejilla que emerge del suelo periódicamente.
// El NavMeshObstacle adjunto talla el NavMesh automáticamente conforme sube.
// Plantemos esta clase sin eventos: el sensor de visión puede consultar su estado directamente a través de la propiedad EstaArriba.
public class DynamicBars : MonoBehaviour
{
    public float tiempoAbajo         = 4.5f; // segundos quieta bajo el suelo antes de subir
    public float tiempoArriba        = 1.5f; // segundos quieta arriba antes de bajar
    public float velocidadMovimiento = 2f;
    public Vector3 desplazamientoArriba = new Vector3(0f, 4f, 0f); // altura que sube desde la posición de reposo

    // Estado observable por los sensores sin necesidad de eventos.
    public bool EstaArriba { get; private set; } = false;

    // Registro estático para que VisionSensor pueda consultarlo sin referencias directas.
    public static List<DynamicBars> todas = new List<DynamicBars>();

    private Vector3 posicionAbajo;
    private Vector3 posicionArriba;

    void Awake()
    {
        todas.Add(this); // Registrar esta instancia en la lista estática al crearse
    }

    void OnDestroy()
    {
        todas.Remove(this); // Eliminar esta instancia de la lista estática al destruirse para evitar referencias colgantes
    }

    void Start()
    {
        posicionAbajo  = transform.position;
        posicionArriba = posicionAbajo + desplazamientoArriba;
        StartCoroutine(CicloRejas());
    }

    // Ciclo infinito: quieta abajo → sube → quieta arriba → baja → repite.
    IEnumerator CicloRejas()
    {
        while (true)
        {
            yield return new WaitForSeconds(tiempoAbajo);

            EstaArriba = true;
            yield return StartCoroutine(Mover(posicionArriba));

            yield return new WaitForSeconds(tiempoArriba);

            EstaArriba = false;
            yield return StartCoroutine(Mover(posicionAbajo));
        }
    }

    // Mueve la rejilla hacia el destino frame a frame.
    IEnumerator Mover(Vector3 destino)
    {
        while (Vector3.Distance(transform.position, destino) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, destino, velocidadMovimiento * Time.deltaTime); // Time.deltaTime para que el avance sea independiente de los FPS
            yield return null;
        }
        transform.position = destino;
    }
}
