using UnityEngine;

// Comportamiento de investigación: el guardia oye al ladrón pero no lo ve,
// y se desplaza hacia la zona del ruido a paso normal.
public class InvestigateBehaviour : IBehaviour
{
    public float speed       = 2f;
    public float acceleration = 2f;

    private HearingSensor hearingSensor;
    private bool ladronEscuchado;
    private bool ladronVisible;
    private Vector3 posicionRuido;

    public void Initialize(HearingSensor hearing, VisionSensor vision)
    {
        hearingSensor = hearing;

        hearing.OnLadronEscuchado  += (pos) => { ladronEscuchado = true; posicionRuido = pos; };
        hearing.OnLadronNoEscuchado += () => ladronEscuchado = false;

        // Cede el control en cuanto el guardia ve al ladrón directamente.
        vision.OnLadronAvistado += () => ladronVisible = true;
        vision.OnLadronPerdido  += () => ladronVisible = false;
    }

    public ActionProposal Evaluate()
    {
        if (!ladronEscuchado || ladronVisible) return null;

        Debug.Log("[InvestigateBehaviour] Investigando zona de ruido");
        return new ActionProposal
        {
            solicitaControl   = true,
            destinoMovimiento = posicionRuido,
            velocidad         = speed,
            aceleracion       = acceleration,
            distanciaParada   = 0f,
            corriendo         = false
        };
    }
}
