using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    [Header("Setup")]
    public Collider hitboxCollider;   // el collider del arma
    public string ladronTag = "Ladron";

    private AgentMove owner;
    private bool enabledHit;
    private bool alreadyHit;          // evita múltiples impactos en el mismo swing

    void Awake()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        EnableHitbox(false);
    }

    public void SetOwner(AgentMove agent)
    {
        owner = agent;
    }

    public void EnableHitbox(bool enable)
    {
        enabledHit = enable;
        alreadyHit = false;

        if (hitboxCollider != null)
            hitboxCollider.enabled = enable;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!enabledHit || alreadyHit) return;

        if (other.CompareTag(ladronTag))
        {
            alreadyHit = true;
            owner?.OnSwordHitLadron();
        }
    }
}