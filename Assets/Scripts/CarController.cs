using UnityEngine;
using UnityEngine.InputSystem; // REQUIRED: Add this namespace

public class CarController : MonoBehaviour
{
    [Header("Car Settings")]
    public float motorTorque = 2000f;
    public float brakeTorque = 4000f;
    public float maxSteeringAngle = 30f;
    
    [Header("Input Setup (New System)")]
    // Assign these in the Inspector if using referencing, 
    // OR this script handles auto-creation if you generated the C# class.
    public InputActionAsset inputActions; 
    private InputAction _moveAction;
    private InputAction _brakeAction;

    [Header("Stability")]
    public Vector3 centerOfMassOffset;
    public float antiRollForce = 5000f;

    [Header("Wheel Colliders")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Wheel Transforms")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    private Rigidbody _rb;
    private float _currentSteerAngle;
    private float _currentBreakForce;
    private float _currentAcceleration;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.centerOfMass += centerOfMassOffset;

        // --- NEW INPUT SYSTEM SETUP ---
        // Ideally, drag your .inputactions asset into the slot in Inspector.
        // We find the specific actions by string name "Car/Move" etc.
        if (inputActions != null)
        {
            _moveAction = inputActions.FindAction("Move");
            _brakeAction = inputActions.FindAction("Brake");
        }
        else 
        {
            Debug.LogError("Please assign the Input Action Asset in the Inspector!");
        }
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
        _brakeAction?.Enable();
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
        _brakeAction?.Disable();
    }

    private void FixedUpdate()
    {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
        ApplyAntiRoll();
    }

    private void GetInput()
    {
        // --- UPDATED INPUT LOGIC ---
        // Read Vector2 from the Move action (X=Turn, Y=Drive)
        Vector2 moveInput = _moveAction.ReadValue<Vector2>();

        float driveInput = moveInput.y; // W/S or Up/Down
        float turnInput = moveInput.x;  // A/D or Left/Right

        _currentAcceleration = motorTorque * driveInput;
        _currentSteerAngle = maxSteeringAngle * turnInput;
        
        // Read Brake button status
        if (_brakeAction.IsPressed())
            _currentBreakForce = brakeTorque;
        else
            _currentBreakForce = 0f;
    }

    // ... Rest of the functions (HandleMotor, HandleSteering, etc.) remain exactly the same as the previous script ...
    
    private void HandleMotor()
    {
        rearLeftCollider.motorTorque = _currentAcceleration;
        rearRightCollider.motorTorque = _currentAcceleration;

        frontLeftCollider.brakeTorque = _currentBreakForce;
        frontRightCollider.brakeTorque = _currentBreakForce;
        rearLeftCollider.brakeTorque = _currentBreakForce;
        rearRightCollider.brakeTorque = _currentBreakForce;
    }

    private void HandleSteering()
    {
        frontLeftCollider.steerAngle = _currentSteerAngle;
        frontRightCollider.steerAngle = _currentSteerAngle;
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.rotation = rot;
        wheelTransform.position = pos;
    }

    private void ApplyAntiRoll()
    {
        ApplyAntiRollForce(frontLeftCollider, frontRightCollider);
        ApplyAntiRollForce(rearLeftCollider, rearRightCollider);
    }

    private void ApplyAntiRollForce(WheelCollider wheelL, WheelCollider wheelR)
    {
        float travelL = 1.0f;
        float travelR = 1.0f;

        bool groundedL = wheelL.GetGroundHit(out WheelHit hitL);
        if (groundedL) travelL = (-wheelL.transform.InverseTransformPoint(hitL.point).y - wheelL.radius) / wheelL.suspensionDistance;

        bool groundedR = wheelR.GetGroundHit(out WheelHit hitR);
        if (groundedR) travelR = (-wheelR.transform.InverseTransformPoint(hitR.point).y - wheelR.radius) / wheelR.suspensionDistance;

        float antiRollFactor = (travelL - travelR) * antiRollForce;

        if (groundedL) _rb.AddForceAtPosition(wheelL.transform.up * -antiRollFactor, wheelL.transform.position);
        if (groundedR) _rb.AddForceAtPosition(wheelR.transform.up * antiRollFactor, wheelR.transform.position);
    }
}