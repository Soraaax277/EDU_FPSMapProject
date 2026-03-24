using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCharacter : MonoBehaviour
{
    public string team = "Green";
    public GameObject bulletPrefab;
    public Transform shootPoint;
    
    [Header("FPS Settings")]
    public float movementSpeed = 6f;
    public float sprintMultiplier = 2.0f;
    public float mouseSensitivity = 0.2f;
    public float fireRate = 0.4f; 
    
    private CharacterController controller;
    private Camera playerCamera;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private bool isGrounded;
    private float nextFireTime;

    void Start()
    {
        gameObject.tag = "Player";
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            if (Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0) velocity.y = -2f;
        float currentSpeed = movementSpeed;
        if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed) currentSpeed *= sprintMultiplier;
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);
        velocity.y += -9.81f * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleRotation()
    {
        transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);
        verticalRotation -= lookInput.y * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -85f, 85f);
        if (playerCamera != null) playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    public void OnMove(InputValue value) { moveInput = value.Get<Vector2>(); }
    public void OnLook(InputValue value) { lookInput = value.Get<Vector2>(); }

    private void Shoot()
    {
        if (bulletPrefab != null && shootPoint != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, shootPoint.rotation);
            bullet.SetActive(true);
            Bullet b = bullet.GetComponent<Bullet>();
            if (b != null) b.team = team;
            Debug.Log("Player shot projectile at " + Time.time);
        }
    }
}
