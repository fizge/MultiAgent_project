using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Proporciona los ojos al agente para detectar objetivos y medir el entorno.
public class VisionSensor : MonoBehaviour
{
    // Mantiene la referencia privada al objetivo pero visible en el editor.
    [SerializeField] private Transform ladron;

    // Posición del cofre en el mapa; se guarda al inicio por si el objeto se destruye.
    [SerializeField] private Transform cofre;

    public float viewDistance = 12f;
    public float viewAngle = 180f;
    public Transform eyePoint; // Punto de origen de la visión
    public LayerMask obstacleMask;
    public LayerMask targetMask;

    public float alturaTorso = 0.2f;

  
    public int direccionesEscaneo = 16;
    public float distanciaMaxEscaneo = 50f;
    public float distanciaProxima = 0.5f;

    // Eventos que notifican cambios de visibilidad 
    public event Action OnLadronAvistado;
    public event Action OnLadronPerdido;

    // Eventos de proximidad.
    public event Action OnLadronMuyCerca;
    public event Action OnLadronLejos;

    // Evento emitido con el resultado del escaneo del entorno.
    // Parámetros: direcciones escaneadas, distancia libre en cada dirección.
    public event Action<Vector3[], float[]> OnEscaneoCompletado;

    // Evento emitido una sola vez cuando el guardia mira hacia el cofre y detecta que fue robado.
    public event Action OnCofresDesaparecido;

    // Última posición conocida del ladrón mientras es visible.
    public Vector3 PosicionLadron { get; private set; }

    private bool ladronVisibleAntes = false;
    private bool ladronMuyCercaAntes = false;

    // Estado anterior de cada reja para detectar cambios.
    private Dictionary<DynamicBars, bool> estadoAntesRejas = new Dictionary<DynamicBars, bool>();

    // Posición guardada del cofre al inicio, por si el GameObject se destruye antes de la detección.
    private Vector3 posicionCofre;
    // Evita disparar OnCofresDesaparecido más de una vez.
    private bool cofresDesaparecidoNotificado = false;

    void Start()
    {
        if (cofre != null) posicionCofre = cofre.position;
        MovementSensor movSensor = GetComponent<MovementSensor>();

        // Comprueba si existe MovementSensor, para evitar error cuando agente no tenga ese componente (las cámaras)
        if (movSensor != null) movSensor.OnDestinoAlcanzado += EscanearEntorno;
        EscanearEntorno();
    }

    void Update()
    {
        bool visible = PuedeVerLadron();
        if (visible) PosicionLadron = ObtenerPosicionObjetivo();

        if (visible && !ladronVisibleAntes) OnLadronAvistado?.Invoke();
        if (!visible && ladronVisibleAntes) { OnLadronPerdido?.Invoke(); EscanearEntorno(); }
        ladronVisibleAntes = visible;

        // Detección de proximidad: solo relevante cuando el ladrón es visible.
        bool muyCerca = visible && (PosicionLadron - transform.position).sqrMagnitude <= distanciaProxima * distanciaProxima;
        if (muyCerca && !ladronMuyCercaAntes) OnLadronMuyCerca?.Invoke();
        if (!muyCerca && ladronMuyCercaAntes) OnLadronLejos?.Invoke();
        ladronMuyCercaAntes = muyCerca;

        ComprobarRejas();
        ComprobarCofre();
    }

    // Recorre todas las rejas de la escena y detecta si alguna visible ha cambiado de estado.
    void ComprobarRejas()
    {
        foreach (DynamicBars reja in DynamicBars.todas)
        {
            bool estadoActual = reja.EstaArriba;

            // Si es la primera vez que vemos esta reja, registramos su estado sin reaccionar.
            if (!estadoAntesRejas.ContainsKey(reja))
            {
                estadoAntesRejas[reja] = estadoActual;
                continue;
            }

            bool estadoAntes = estadoAntesRejas[reja];

            // Solo reaccionar si el estado cambió y la reja está dentro del campo de visión.
            if (estadoActual != estadoAntes)
            {
                estadoAntesRejas[reja] = estadoActual;

                Vector3 haciaLaReja = reja.transform.position - transform.position;
                if (haciaLaReja.magnitude > viewDistance) continue;
                if (Vector3.Angle(transform.forward, haciaLaReja) > viewAngle * 0.5f) continue;

                EscanearEntorno();
            }
        }
    }

    // Detecta si el guardia está mirando hacia el cofre y el botín ya fue robado.
    // Comprueba distancia, ángulo de visión y línea de visión (raycast) para que solo lo detecten
    // los guardias que realmente tengan el cofre en su campo de visión.
    // Se dispara una sola vez para que el behaviour reaccione sin repeticiones.
    void ComprobarCofre()
    {
        if (cofre == null) return; // la cámara no tiene referencia al cofre, salimos
        if (cofresDesaparecidoNotificado) return;
        if (GameManager.Instance == null || !GameManager.Instance.hasLoot) return;

        Vector3 haciaElCofre = posicionCofre - transform.position;

        // Comprobación de distancia.
        if (haciaElCofre.magnitude > viewDistance) return;

        // Comprobación de ángulo: el cofre debe estar dentro del campo de visión.
        if (Vector3.Angle(transform.forward, haciaElCofre) > viewAngle * 0.5f) return;

        // Comprobación de línea de visión: no debe haber obstáculos entre el guardia y el cofre.
        if (Physics.Raycast(eyePoint.position, haciaElCofre.normalized, haciaElCofre.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)) return;

        cofresDesaparecidoNotificado = true;
        Debug.Log($"[VisionSensor] {name} detecta que el cofre ha desaparecido");
        OnCofresDesaparecido?.Invoke();
    }

    // Comprueba el estado del cofre cuando el guardia está cerca de él.
    // Devuelve true si el cofre sigue intacto, false si fue robado.
    // Usado por CheckChestBehaviour al llegar al cofre, en vez de acceder a GameManager directamente.
    public bool ComprobarEstadoCofre()
    {
        Vector3 haciaElCofre = posicionCofre - transform.position;
        if (haciaElCofre.magnitude > viewDistance) return true; // demasiado lejos para ver — asume intacto

        if (GameManager.Instance == null) return true;
        return !GameManager.Instance.hasLoot; // true = intacto, false = robado
    }

    // Escanea el entorno y emite OnEscaneoCompletado con los datos en bruto.
    // Se llama en Start y cada vez que el ladrón se pierde de vista.
    void EscanearEntorno()
    {
        Vector3[] direcciones = new Vector3[direccionesEscaneo];
        float[] distancias = new float[direccionesEscaneo];

        for (int i = 0; i < direccionesEscaneo; i++)
        {
            float angulo = i * (360f / direccionesEscaneo);
            direcciones[i] = Quaternion.Euler(0, angulo, 0) * Vector3.forward;
            distancias[i] = MedirDistanciaLibre(direcciones[i], distanciaMaxEscaneo);
        }

        OnEscaneoCompletado?.Invoke(direcciones, distancias);
    }

    // Detecta si el objetivo está dentro del rango, ángulo y línea de visión.
    public bool PuedeVerLadron()
    {
        if (!ladron) return false;

        Vector3 origin = eyePoint.position;
        Vector3 targetPos = ladron.position;
        targetPos.y += alturaTorso;

        Vector3 toTarget = targetPos - origin;

        // Comprobación de distancia.
        if (toTarget.magnitude > viewDistance)
            return false;

        // Comprobación de ángulo de visión.
        float ang = Vector3.Angle(transform.forward, toTarget);
        if (ang > viewAngle * 0.5f)
            return false;

        // Raycast físico para detectar obstáculos entre el guardia y el objetivo.
        int mask = obstacleMask | targetMask;
        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, viewDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == ladron || hit.transform.IsChildOf(ladron);
        }

        return false;
    }

    // Calcula el espacio despejado en una dirección usando el NavMesh (usado para planificación).
    public float MedirDistanciaLibre(Vector3 direccion, float distanciaMax)
    {
        Vector3 origen = transform.position;
        Vector3 destino = origen + (direccion.normalized * distanciaMax);

        if (NavMesh.Raycast(origen, destino, out NavMeshHit hit, NavMesh.AllAreas))
        {
            return Vector3.Distance(origen, hit.position);
        }

        return distanciaMax;
    }

    // Abstracción: devuelve las coordenadas del objetivo sin exponer el objeto real.
    public Vector3 ObtenerPosicionObjetivo()
    {
        if (ladron != null) return ladron.position;
        return Vector3.zero;
    }
}