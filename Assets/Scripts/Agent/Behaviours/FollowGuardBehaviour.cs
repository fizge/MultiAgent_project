// Comportamiento de seguimiento: el esqueleto oye a otro esqueleto corriendo y va hacia él.
// Solo activo si no hay información más directa (visión o ruido del ladrón).
using UnityEngine;

public class FollowGuardBehaviour : IBehaviour
{
    public float speed = 2f;
    public float acceleration = 2f;

    private bool esqueletoEscuchado;
    private bool ladronVisible;
    private bool ladronEscuchado;
    private Vector3 posicionGuardia;

    public void Initialize(HearingSensor hearing, VisionSensor vision)
    {
        hearing.OnEsqueletoEscuchado += (pos) => { esqueletoEscuchado = true; posicionGuardia = pos; };
        hearing.OnEsqueletoNoEscuchado += () => esqueletoEscuchado = false;

        // Cede si hay información más directa sobre el ladrón.
        vision.OnLadronAvistado += () => ladronVisible   = true;
        vision.OnLadronPerdido += () => ladronVisible   = false;
        hearing.OnLadronEscuchado += (_) => ladronEscuchado = true;
        hearing.OnLadronNoEscuchado += () => ladronEscuchado = false;
    }

    public ActionProposal Evaluate()
    {
        // Cede si no escuchó al esqueleto o si tiene información directa del ladrón.
        if (!esqueletoEscuchado || ladronVisible || ladronEscuchado) return null; 

        return new ActionProposal
        {
            solicitaControl = true,
            esPrioridadAlta = true,
            destinoMovimiento = posicionGuardia,
            velocidad = speed,
            aceleracion = acceleration,
            distanciaParada = 0f,
            corriendo = false
        };
    }
}
