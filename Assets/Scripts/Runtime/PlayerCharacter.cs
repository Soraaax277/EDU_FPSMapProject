using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10)]   
public class PlayerCharacter : MonoBehaviour
{
    public string team = "Green";
    public GameObject bulletPrefab;
    public Transform  shootPoint;
    [HideInInspector] public Transform gunTransform;   

    [Header("FPS Settings")]
    public float movementSpeed    = 6f;
    public float sprintMultiplier = 2.0f;
    public float mouseSensitivity = 0.15f;
    public float fireRate         = 0.4f;

    [Header("Look Clamp")]
    public float minPitch = -85f;
    public float maxPitch =  85f;

    private CharacterController _controller;
    private Camera              _cam;
    private Vector2             _moveInput;
    private Vector2             _lookInput;
    private float               _pitch = 0f;
    private Vector3             _velocity;
    private float               _nextFireTime;

    private static readonly Vector3 GunOffset = new Vector3(0.35f, -0.32f, 0.95f);

    void Start()
    {
        gameObject.tag = "Player";
        _controller    = GetComponent<CharacterController>();
        _cam           = Camera.main;

        if (gunTransform != null)
            gunTransform.SetParent(null);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        HandleMovement();
        HandleYaw();       

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            if (Time.time >= _nextFireTime)
            { Shoot(); _nextFireTime = Time.time + fireRate; }
    }

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        _pitch -= _lookInput.y * mouseSensitivity;
        _pitch  = Mathf.Clamp(_pitch, minPitch, maxPitch);
        _cam.transform.rotation = Quaternion.Euler(_pitch, transform.eulerAngles.y, 0f);

        if (gunTransform != null)
        {
            gunTransform.position = _cam.transform.TransformPoint(GunOffset);
            gunTransform.rotation = _cam.transform.rotation;
        }
    }

    private void HandleMovement()
    {
        bool grounded = _controller.isGrounded;
        if (grounded && _velocity.y < 0f) _velocity.y = -2f;

        float speed = movementSpeed;
        if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
            speed *= sprintMultiplier;

        Vector3 move = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        _controller.Move(move * speed * Time.deltaTime);

        _velocity.y += Physics.gravity.y * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleYaw()
    {
        transform.Rotate(Vector3.up * _lookInput.x * mouseSensitivity);
    }

    public void OnMove(InputValue v) { _moveInput = v.Get<Vector2>(); }
    public void OnLook(InputValue v) { _lookInput = v.Get<Vector2>(); }

    private void Shoot()
    {
        if (bulletPrefab == null || shootPoint == null) return;
        GameObject bullet = Instantiate(bulletPrefab, shootPoint.position, shootPoint.rotation);
        bullet.SetActive(true);
        Bullet b = bullet.GetComponent<Bullet>();
        if (b != null) b.team = team;
    }

    public void ResetState()
    {
        _pitch    = 0f;
        _velocity = Vector3.zero;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }
}
