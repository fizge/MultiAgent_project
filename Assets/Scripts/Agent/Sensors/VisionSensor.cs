using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Proporciona los ojos al agente para detectar objetivos y medir el entorno.
public class VisionSensor : MonoBehaviour
{
    // Mantiene la referencia privada al objetivo pero visible en el editor.
    [SerializeField] private Transform ladron;

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

    // Última posición conocida del ladrón mientras es visible.
    public Vector3 PosicionLadron { get; private set; }

    private bool ladronVisibleAntes = false;
    private bool ladronMuyCercaAntes = false;

    // Estado anterior de cada reja para detectar cambios.
    private Dictionary<DynamicBars, bool> estadoAntesRejas = new Dictionary<DynamicBars, bool>();

    void Start()
    {
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