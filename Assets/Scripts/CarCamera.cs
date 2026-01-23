using UnityEngine;
using UnityEngine.InputSystem; // Uses the New Input System

public class CarCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // Drag your car here
    public Vector3 offset = new Vector3(0, 2f, -5f); // Height and Distance from car

    [Header("Input Settings")]
    public float mouseSensitivity = 0.5f;
    public bool lockCursor = true;

    [Header("Smoothing")]
    public float followSpeed = 10f; // How fast it moves to target position
    public float rotationSpeed = 5f; // How fast it rotates to look at target
    public float mouseReturnSpeed = 2f; // How fast it re-centers behind car

    private float _yawInput;
    private float _pitchInput;
    private Vector3 _velocity = Vector3.zero; // For SmoothDamp

    private void Start()
    {
        // Optional: Hide cursor for better experience
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Initialize camera rotation to match car
        if (target != null)
        {
            transform.rotation = target.rotation;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleInput();
        MoveAndRotate();
    }

    private void HandleInput()
    {
        // Read Mouse Input using New Input System directly
        // This avoids the "InvalidOperationException" you saw earlier
        Vector2 mouseDelta = Vector2.zero;
        
        if (Mouse.current != null)
        {
            mouseDelta = Mouse.current.delta.ReadValue();
        }

        // Add mouse movement to our yaw/pitch
        _yawInput += mouseDelta.x * mouseSensitivity;
        _pitchInput -= mouseDelta.y * mouseSensitivity;

        // Clamp the vertical look (Pitch) so you can't flip the camera upside down
        _pitchInput = Mathf.Clamp(_pitchInput, -10f, 40f);

        // Optional: Slowly reset the Yaw (horizontal) to 0 so camera re-aligns behind car
        // Remove this line if you want the camera to stay where you leave it
        _yawInput = Mathf.Lerp(_yawInput, 0, Time.deltaTime * mouseReturnSpeed);
    }

    private void MoveAndRotate()
    {
        // 1. Calculate Rotation
        // We combine the Car's rotation with our Mouse Input offset
        Quaternion carRotation = Quaternion.Euler(0, target.eulerAngles.y, 0);
        Quaternion inputRotation = Quaternion.Euler(_pitchInput, _yawInput, 0);
        Quaternion targetRotation = carRotation * inputRotation;

        // 2. Calculate Position
        // Position is: Car Position + (Rotation * Offset)
        Vector3 targetPosition = target.position + (targetRotation * offset);

        // 3. Apply Smoothing (Position)
        // SmoothDamp eliminates jitter
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, 1f / followSpeed);

        // 4. Apply Smoothing (Rotation)
        // Make the camera look at the car's center (plus a little height)
        Vector3 lookAtPoint = target.position + Vector3.up * offset.y * 0.5f;
        Quaternion lookRotation = Quaternion.LookRotation(lookAtPoint - transform.position);
        
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }
}