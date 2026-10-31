using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float mouseSensitivity = 100f;

    public Transform cameraPivot;

    float xRotation = 0f;
    CharacterController controller;

    float gravity = -9.81f;
    float velocityY;

    private AudioSource footsteps;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        footsteps = GetComponent<AudioSource>();
    }

    void Update()
    {

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        if (controller.isGrounded && velocityY < 0)
        {
            velocityY = -2f; 
        }

        velocityY += gravity * Time.deltaTime;

        Vector3 move = transform.right * x + transform.forward * z;
        move.y = velocityY;

        controller.Move(move * speed * Time.deltaTime);

   
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

      
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);


        transform.Rotate(Vector3.up * mouseX);

        bool isWalking = Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f;

        if (isWalking)
        {
            if (!footsteps.isPlaying)
                footsteps.Play();
        }
        else
        {
            footsteps.Stop();
        }
    }
}