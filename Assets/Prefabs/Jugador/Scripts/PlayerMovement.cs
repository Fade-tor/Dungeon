using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float baseSpeed = 12f;
    public float sprintSpeed = 10f;
    public float jumpHeight = 3f;
    public float gravity = -9.81f;
    public float crouchHeight = 0.5f; //altura al agacharse
    private float _originalHeight; //Altura normal

    [Header("Dash   ")]
    public bool enableDash = true; // Habilitar o deshabilitar el dash
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private Vector3 _dashDirection;
    private bool _isDashing = false; // se inicia en false
    private float _dashTimeLeft = 0f;
    private float _dashCooldownLeft = 0f;

    [Header("Components")]
    private CharacterController _controller;
    private PlayerInput _playerInput;// base
    private InputAction _moveAction;// mover
    private InputAction _jumpAction;// saltar
    private InputAction _sprintAction;// correr
    private InputAction _dashAction;// deslizarse
    private InputAction _crouchAction; // agacharse
    private const float GroundedGravity = -0.5f; //Gravedad al estar en el suelo
    private const float TerminalVelocity = -50f; // Velocidad máxima de caída

    private Vector3 _velocity;
    private float _speedBoost = 1f;
    private bool _isCrouching = false; // se inicia en false

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _playerInput = GetComponent<PlayerInput>();
        _originalHeight = _controller.height; // Guardamos la altura original del personaje

        _moveAction = _playerInput.actions["Move"];
        _jumpAction = _playerInput.actions["Jump"];
        _sprintAction = _playerInput.actions["Sprint"];
        _dashAction = _playerInput.actions["Dash"];
        _crouchAction = _playerInput.actions["Crouch"];
    }

    private void Update()
    {
        HandleMovement();
        HandleJump();
        ApplyGravity();
        HandleDash();
        HandleCrouch();

        _controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleMovement()
    {
        // 1. Movimiento básico (sin suavizado)
        Vector2 input = _moveAction.ReadValue<Vector2>();
        _speedBoost = _sprintAction.IsPressed() && !_isCrouching ? sprintSpeed : 1f; //No correo mientras está agachado

        // Solo movemos si hay input (para que se detenga)
        if (input.magnitude > 0.1f && !_isDashing)
        {
            Vector3 move = transform.right * input.x + transform.forward * input.y;
            move.Normalize(); // Normalizamos el vector de movimiento

            move *= (baseSpeed + _speedBoost); // Aplicamos la velocidad base y el boost
            _velocity.x = move.x;
            _velocity.z = move.z;
        }
        else if (!_isDashing)
        {
            _velocity.x = 0f;
            _velocity.z = 0f;
        }
    }

    private void HandleJump()
    {
        // 2. Gravedad y salto (versión simple)
        if (_controller.isGrounded)
        {
            /*if (_velocity.y < 0f)
            {
                _velocity.y = -2f;
            }*/

            if (_jumpAction.triggered && !_isCrouching)
            {
                Debug.Log("Jump triggered!");
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        //_velocity.y += gravity * Time.deltaTime;
        //_controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleDash()
    {
        // 4. Deslizarse
        if (!enableDash) return; // Si el dash está deshabilitado, no hacemos nada

        if (_dashAction.triggered && _dashCooldownLeft <= 0f && !_isDashing && !_isCrouching)
        {
            _isDashing = true;
            _dashTimeLeft = dashDuration;
            _dashCooldownLeft = dashCooldown;

            Vector2 input = _moveAction.ReadValue<Vector2>();
            _dashDirection = input.magnitude > 0.1f
                ? (transform.right * input.x + transform.forward * input.y).normalized
                : transform.forward;
        }
        if (_isDashing)
        {
            _dashTimeLeft -= Time.deltaTime;

            Vector3 dashMove = _dashDirection * (dashDistance / dashDuration);
            _velocity.x = dashMove.x;
            _velocity.z = dashMove.z;

            if (_dashTimeLeft <= 0f)
            {
                _isDashing = false;
                //_velocity = Vector3.zero; // Reiniciar la velocidad al terminar el dash
            }
        }
        else
        {
            _dashCooldownLeft -= Time.deltaTime;
        }
    }

    private void HandleCrouch()
    {
        bool isCrouchPressed = _crouchAction.IsPressed();
        if (isCrouchPressed && !_isDashing)
        {
            // Alternar entre agachado y de pie
            _isCrouching = true;
            _controller.height = crouchHeight;

        }
        else
        {
            if (CanStandUp())
            {
                _isCrouching = false;
                _controller.height = _originalHeight;
            }
        }
    }

    // Método para verificar si hay espacio para levantarse
    private bool CanStandUp()
    {
        // Uso un Raycast para verificar si hay obstáculos por encima del personaje
        float headClearance = _originalHeight - crouchHeight;
        Vector3 start = transform.position + Vector3.up * crouchHeight;
        return !Physics.Raycast(start, Vector3.up, headClearance);
    }
    private void ApplyGravity()
    {
        // Aplicar gravedad
        if (_controller.isGrounded)
        {
            if (_velocity.y < 0f)
            {
                _velocity.y = GroundedGravity;
            }
        }
        else
        {
            _velocity.y = Mathf.Max(_velocity.y + gravity * Time.deltaTime, TerminalVelocity);
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Debug: Radio de detección de suelo
        Gizmos.color = Color.cyan;
        float radius = _controller != null ? _controller.radius * 0.9f : 0.5f;
        Gizmos.DrawWireSphere(transform.position + Vector3.down * 0.1f, radius);

        // Debug: Velocidad actual
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                                $"Velocity Y: {_velocity.y:F2}\nGrounded: {_controller.isGrounded}");
#endif
    }
}