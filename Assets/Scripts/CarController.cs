using UnityEngine;
using UnityEngine.InputSystem;
using GameJam2026;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float acceleration = 500f;
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float brakeForce = 1000f;
    [SerializeField] private float steerAngle = 25f;
    
    [Header("Physics Settings")]
    [SerializeField] private float downForce = 100f; // Keeps rover grounded
    [SerializeField] private Vector3 centerOfMass = new Vector3(0, -0.5f, 0);
    
    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeft;
    [SerializeField] private WheelCollider frontRight;
    [SerializeField] private WheelCollider rearLeft;
    [SerializeField] private WheelCollider rearRight;
    
    [Header("Wheel Meshes")]
    [SerializeField] private Transform frontLeftMesh;
    [SerializeField] private Transform frontRightMesh;
    [SerializeField] private Transform rearLeftMesh;
    [SerializeField] private Transform rearRightMesh;
    
    // Private variables
    private Rigidbody rb;
    private InputAction moveAction;
    private InputAction brakeAction;
    private float currentSpeed;
    private float motorInput;
    private float steerInput;
    private RoverAttributeManager attributeManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Basic rigidbody setup
        rb.mass = 1000f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.centerOfMass = centerOfMass;
        
        // Setup input
        if (inputActions != null)
        {
            moveAction = inputActions.FindAction("Move");
            brakeAction = inputActions.FindAction("Brake");
        }
    }

    private void Start()
    {
        attributeManager = GetComponent<RoverAttributeManager>();
        SetupWheels();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        brakeAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        brakeAction?.Disable();
    }

    private void Update()
    {
        // Get input
        Vector2 input = moveAction.ReadValue<Vector2>();
        motorInput = input.y;
        steerInput = input.x;
        
        // Update attribute manager
        currentSpeed = rb.linearVelocity.magnitude;
        if (attributeManager != null)
        {
            attributeManager.SetMoving(currentSpeed > 0.5f);
        }
    }

    private void FixedUpdate()
    {
        // Apply downforce to keep grounded
        rb.AddForce(-transform.up * downForce * rb.linearVelocity.magnitude);
        
        // Handle movement
        Move();
        Steer();
        Brake();
        
        // Update visual wheels
        UpdateWheelMeshes();
    }

    private void Move()
    {
        // Get speed multiplier
        float speedMultiplier = 1f;
        if (attributeManager != null)
        {
            speedMultiplier = attributeManager.SpeedMultiplier;
        }
        
        // Calculate motor torque
        float motor = acceleration * motorInput * speedMultiplier;
        
        // Limit max speed
        if (currentSpeed < maxSpeed || motorInput < 0)
        {
            rearLeft.motorTorque = motor;
            rearRight.motorTorque = motor;
        }
        else
        {
            rearLeft.motorTorque = 0;
            rearRight.motorTorque = 0;
        }
    }

    private void Steer()
    {
        float steering = steerAngle * steerInput;
        frontLeft.steerAngle = steering;
        frontRight.steerAngle = steering;
    }

    private void Brake()
    {
        float brake = 0f;
        
        if (brakeAction.IsPressed())
        {
            brake = brakeForce;
        }
        else if (Mathf.Abs(motorInput) < 0.1f && currentSpeed < 1f)
        {
            // Auto brake when stopped
            brake = brakeForce * 0.5f;
        }
        
        frontLeft.brakeTorque = brake;
        frontRight.brakeTorque = brake;
        rearLeft.brakeTorque = brake;
        rearRight.brakeTorque = brake;
    }

    private void UpdateWheelMeshes()
    {
        UpdateWheelMesh(frontLeft, frontLeftMesh);
        UpdateWheelMesh(frontRight, frontRightMesh);
        UpdateWheelMesh(rearLeft, rearLeftMesh);
        UpdateWheelMesh(rearRight, rearRightMesh);
    }

    private void UpdateWheelMesh(WheelCollider collider, Transform mesh)
    {
        if (mesh == null) return;
        
        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);
        
        mesh.position = position;
        mesh.rotation = rotation;
    }

    private void SetupWheels()
    {
        SetupWheel(frontLeft);
        SetupWheel(frontRight);
        SetupWheel(rearLeft);
        SetupWheel(rearRight);
    }

    private void SetupWheel(WheelCollider wheel)
    {
        if (wheel == null) return;
        
        // Suspension
        wheel.suspensionDistance = 0.2f;
        
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = 20000f;
        spring.damper = 2000f;
        spring.targetPosition = 0.5f;
        wheel.suspensionSpring = spring;
        
        // Friction
        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = 1.5f;
        wheel.forwardFriction = forward;
        
        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = 1.5f;
        wheel.sidewaysFriction = sideways;
        
        wheel.mass = 20f;
    }
}