using UnityEngine;

public class ThiefMove : MonoBehaviour
{
    public float rotationSpeed = 540f;
    public float moveSpeed = 5f;
    public float gravitySpeed = -20f;

    private CharacterController controller;
    private Animator animator;

    private float fallSpeed;
    private Quaternion rotationObjetivo;

    // Estados del personaje
    private enum Estado
    {
        Idle, // Sin movimiento
        Running, // Corriendo
        Turning // Girando 
    }

    // Estado actual del personaje
    private Estado estadoActual = Estado.Idle; 

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal"); // Entrada horizontal para rotación (A/D)
        float z = Input.GetAxisRaw("Vertical"); // Entrada vertical para movimiento (W/S)

        GestionarTransiciones(z); 
        ProcesarEstado(x);
    }

    // Transiciones de estado
    void GestionarTransiciones(float z)
    {
        if (estadoActual == Estado.Turning)
            return;

        if (z < 0f)
        {
            IniciarGiro();
        }

        else if (z > 0f)
        {
            estadoActual = Estado.Running;
        }

        else
        {
            estadoActual = Estado.Idle;
        }
    }

    // Procesar estado actual 
    void ProcesarEstado(float x)
    {
        switch (estadoActual)
        {
            case Estado.Idle: // Sin movimiento no corremos, solo rotamos y aplicamos gravedad
                animator.SetBool("isRunning", false);
                transform.Rotate(0f, x * rotationSpeed * 0.35f * Time.deltaTime, 0f);
                AplicarGravedad();
                Caída();
                break;

            case Estado.Running: // Corriendo, rotamos y avanzamos
                animator.SetBool("isRunning", true);
                transform.Rotate(0f, x * rotationSpeed * 0.35f * Time.deltaTime, 0f);
                AplicarGravedad();
                MoverAdelante();
                break;

            case Estado.Turning: // Girando, procesamos el giro y aplicamos gravedad
                ProcesarGiro();
                break;
        }
    }

    // Giro 180º
    void IniciarGiro()
    {
        estadoActual = Estado.Turning;
        rotationObjetivo = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);
    }
 
    // Procesar el giro hacia la rotación objetivo
    void ProcesarGiro()
    {
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            rotationObjetivo,
            rotationSpeed * Time.deltaTime
        );

        float falta = Quaternion.Angle(transform.rotation, rotationObjetivo);

        if (falta < 0.5f)
        {
            estadoActual = Estado.Idle;
        }

        animator.SetBool("isRunning", false);
        AplicarGravedad();
        Caída();
    }

    // Movimiento hacia adelante
    void MoverAdelante()
    {
        Vector3 dir = transform.forward * moveSpeed;
        dir.y = fallSpeed;
        controller.Move(dir * Time.deltaTime);
    }

    // Caída vertical por gravedad
    void Caída()
    {
        Vector3 caida = new Vector3(0f, fallSpeed, 0f);
        controller.Move(caida * Time.deltaTime);
    }

    // Gravedad
    void AplicarGravedad()
    {
        if (controller.isGrounded && fallSpeed < 0f)
        {
            fallSpeed = -5f;
        }

        fallSpeed += gravitySpeed * Time.deltaTime;
    }
}
