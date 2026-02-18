using UnityEngine;

public class ThiefMove : MonoBehaviour
{
    public float rotSpeed = 540f;
    public float moveSpeed = 5f;
    public float grav = -20f;

    private bool girando = false;

    private CharacterController controller;
    private Animator animator;

    private float velY;
    private Quaternion rotObjetivo;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        bool corriendo = (!girando && z > 0f); // solo si vas hacia delante

        // -------------------------
        // GIRO 180 SI VAS HACIA ATRÁS
        // -------------------------
        if (!girando && z < 0f)
        {
            girando = true;
            rotObjetivo = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);
        }

        if (girando)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                rotObjetivo,
                rotSpeed * Time.deltaTime
            );

            float falta = Quaternion.Angle(transform.rotation, rotObjetivo);
            if (falta < 0.5f)
            {
                girando = false;
            }

            animator.SetBool("isRunning", false);
            SoloGravedad();
            return;
        }

        // Animación (Idle/Run)
        animator.SetBool("isRunning", corriendo);

        // -------------------------
        // MOVIMIENTO NORMAL
        // -------------------------
        float adelante = corriendo ? 1f : 0f;

        // girar con A/D aunque no avances
        transform.Rotate(0f, x * rotSpeed * 0.35f * Time.deltaTime, 0f);

        // gravedad
        if (controller.isGrounded && velY < 0f)
        {
            velY = -5f;
        }

        velY += grav * Time.deltaTime;

        Vector3 dir = transform.forward * (adelante * moveSpeed);
        dir.y = velY;

        controller.Move(dir * Time.deltaTime);
    }

    void SoloGravedad()
    {
        if (controller.isGrounded && velY < 0f)
        {
            velY = -5f;
        }

        velY += grav * Time.deltaTime;

        Vector3 caida = new Vector3(0f, velY, 0f);
        controller.Move(caida * Time.deltaTime);
    }
}
