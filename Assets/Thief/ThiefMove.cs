using UnityEngine;




public class ThiefMove : MonoBehaviour
{
    public float rotateSpeedDeg = 540f; // grados/segundo (360-720 suele ir bien)
    bool turning180 = false;
    public float speed = 5f;
    public float turnSpeed = 12f;
    public float gravity = -20f;

    CharacterController cc;
    float yVelocity;

     Quaternion target180;

    void Start()
    {
        cc = GetComponent<CharacterController>();
    }
   void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Si pulsas atrás y NO estamos ya girando, inicia giro 180
        if (!turning180 && v < 0f)
        {
            turning180 = true;
            target180 = Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);
        }

        // Si estamos girando, solo giramos (sin mover)
        if (turning180)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                target180,
                rotateSpeedDeg * Time.deltaTime
            );

            // cuando termina el giro, salimos del modo giro
            float angleLeft = Quaternion.Angle(transform.rotation, target180);
            if (angleLeft < 0.5f)
                turning180 = false;

            ApplyGravityOnly();
            return;
        }

        // Movimiento normal (sin marcha atrás): solo avanzas si v > 0
        float forward = Mathf.Max(0f, v);

        // Opcional: si quieres que A/D rote al personaje (recomendado con cámara fija)
        transform.Rotate(0f, h * rotateSpeedDeg * 0.35f * Time.deltaTime, 0f);

        // Gravedad + mover
        if (cc.isGrounded && yVelocity < 0f) yVelocity = -2f;
        yVelocity += gravity * Time.deltaTime;

        Vector3 move = transform.forward * (forward * speed);
        move.y = yVelocity;

        cc.Move(move * Time.deltaTime);
    }

    void ApplyGravityOnly()
    {
        if (cc.isGrounded)
        {
            if (yVelocity < 0f)
                yVelocity = -5f;
        }

        yVelocity += gravity * Time.deltaTime;

        Vector3 move = new Vector3(0f, yVelocity, 0f);
        cc.Move(move * Time.deltaTime);
    }


}
