using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;   // <-- ADD THIS LINE

public class IMUPublisher : MonoBehaviour
{
    private ROSConnection ros;
    private Rigidbody rb;

    [Header("ROS Topic")]
    public string imuTopic = "drone/imu";

    [Header("Publish Rate")]
    public float publishRate = 100f; // Hz

    private float timer = 0f;
    private float publishInterval;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        rb = GetComponent<Rigidbody>();
        publishInterval = 1f / publishRate;
        ros.RegisterPublisher<ImuMsg>(imuTopic);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= publishInterval)
        {
            timer = 0f;
            PublishIMU();
        }
    }

    void PublishIMU()
    {
        // Orientation (as quaternion)
        Quaternion orientation = transform.rotation;

        // Angular velocity (rad/s)
        Vector3 angularVelocity = rb.angularVelocity; // Unity uses rad/s

        // Linear acceleration (world space, minus gravity to get proper acceleration)
        Vector3 linearAcceleration = (rb.linearVelocity - Physics.gravity * Time.deltaTime) / Time.deltaTime;

        ImuMsg msg = new ImuMsg
        {
            header = new HeaderMsg
            {
                frame_id = "drone_imu",
                stamp = new TimeMsg
                {
                    sec = (uint)Time.time,
                    nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
                }
            },
            orientation = new QuaternionMsg
            {
                x = orientation.x,
                y = orientation.y,
                z = orientation.z,
                w = orientation.w
            },
            angular_velocity = new Vector3Msg
            {
                x = angularVelocity.x,
                y = angularVelocity.y,
                z = angularVelocity.z
            },
            linear_acceleration = new Vector3Msg
            {
                x = linearAcceleration.x,
                y = linearAcceleration.y,
                z = linearAcceleration.z
            }
        };

        // Fill covariance arrays (optional – set to zero if unknown)
        msg.orientation_covariance = new double[9];
        msg.angular_velocity_covariance = new double[9];
        msg.linear_acceleration_covariance = new double[9];

        ros.Publish(imuTopic, msg);
    }
}