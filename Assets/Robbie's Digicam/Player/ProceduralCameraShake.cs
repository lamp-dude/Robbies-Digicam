using System;
using UnityEngine;

//kinda finicky
[RequireComponent(typeof(Camera))]
public sealed class ProceduralCameraShake : MonoBehaviour
{
    [Header("Master")]
    [SerializeField, Range(0f, 3f)] private float overallStrength = 1f;
    [SerializeField, Range(0f, 0.45f)] private float frequencyWander = 0.22f;

    [Header("Idle breathing")]
    [SerializeField] private float breathingHeight = 0.006f;
    [SerializeField] private float breathingCycleSeconds = 4.5f;

    [Header("Moving - position in meters")]
    [SerializeField] private Vector3 movingPositionAmplitude = new Vector3(0.055f, 0.04f, 0.012f);
    [SerializeField] private Vector3 movingPositionFrequency = new Vector3(1.28f, 1.05f, 0.69f);

    [Header("Moving - rotation in degrees")]
    [SerializeField] private Vector3 movingRotationAmplitude = new Vector3(0.3f, 0.25f, 0.04f);
    [SerializeField] private Vector3 movingRotationFrequency = new Vector3(1.18f, 1.43f, 0.76f);

    [Header("Movement response")]
    [SerializeField, Range(1f, 2f)] private float sprintMultiplier = 1.35f;
    [SerializeField, Range(0f, 0.5f)] private float sidewaysLeanDegrees = 0.18f;
    [SerializeField] private float landingDip = 0.025f;

    private CharacterController controller;
    private WaveGroup movingPositionWave;
    private WaveGroup movingRotationWave;
    private Vector3 restLocalPosition;
    private Quaternion aimLocalRotation;
    private float movingBlend;
    private float movingBlendVelocity;
    private float sprintBlend;
    private float sprintBlendVelocity;
    private float breathingPhase;
    private float sidewaysVelocity;
    private float landingOffset;
    private float landingVelocity;
    private float previousVerticalSpeed;
    private bool previouslyGrounded;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            Initialize();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        controller = GetComponentInParent<CharacterController>();
        restLocalPosition = transform.localPosition;
        aimLocalRotation = transform.localRotation;

        var random = new System.Random(Environment.TickCount ^ GetInstanceID());
        movingPositionWave = new WaveGroup(random);
        movingRotationWave = new WaveGroup(random);
        initialized = true;
    }

    public void SetAimRotation(Quaternion localRotation)
    {
        aimLocalRotation = localRotation;
    }

    private void LateUpdate()
    {
        if (!initialized)
            Initialize();
        if (controller == null)
            return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f)
            return;

        Vector3 localVelocity = controller.transform.InverseTransformDirection(controller.velocity);
        float planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
        bool grounded = controller.isGrounded;
        float targetBlend = grounded ? Mathf.InverseLerp(0.15f, 2.2f, planarSpeed) : 0f;
        float movementTransition = targetBlend > movingBlend ? 0.45f : 0.75f;
        movingBlend = Mathf.SmoothDamp(movingBlend, targetBlend, ref movingBlendVelocity, movementTransition, Mathf.Infinity, dt);
        float targetSprint = Mathf.InverseLerp(3.6f, 6.25f, planarSpeed);
        sprintBlend = Mathf.SmoothDamp(sprintBlend, targetSprint, ref sprintBlendVelocity, 0.7f, Mathf.Infinity, dt);
        sidewaysVelocity = Mathf.Lerp(sidewaysVelocity, localVelocity.x, 1f - Mathf.Exp(-2.5f * dt));
        float movingScale = movingBlend * Mathf.Lerp(1f, sprintMultiplier, sprintBlend);

        // A slow, small breath moves only vertically while idle. Its tempo varies gently; it does not rotate or rock the camera.
        float time = Time.time;
        float breathRate = 1f + 0.08f * (Mathf.PerlinNoise(17.3f, time * 0.13f) * 2f - 1f);
        breathingPhase += dt * Mathf.PI * 2f * breathRate / Mathf.Max(1f, breathingCycleSeconds);
        float breath = Mathf.Sin(breathingPhase) * breathingHeight * (1f - 0.45f * movingBlend);

        // The axes drift independently and vary in strength and speed. Only
        // the motion blend changes their contribution between player statas.
        Vector3 position = movingPositionWave.Sample(movingPositionAmplitude, movingPositionFrequency, frequencyWander, dt, time) * movingScale;
        position.y += breath;
        Vector3 rotation = movingRotationWave.Sample(movingRotationAmplitude, movingRotationFrequency, frequencyWander, dt, time) * movingScale;
        rotation.z -= sidewaysLeanDegrees * Mathf.Clamp(sidewaysVelocity / 4f, -1f, 1f);

        if (grounded && !previouslyGrounded && previousVerticalSpeed < -2f)
        {
            float severity = Mathf.InverseLerp(2f, 11f, -previousVerticalSpeed);
            landingVelocity -= landingDip * severity * 12f;
        }
        landingVelocity += (-80f * landingOffset - 18f * landingVelocity) * dt;
        landingOffset += landingVelocity * dt;
        position.y += landingOffset;

        // The player's update sets the aim this aditive pose is applied once
        // just before renderin. There is no extra smoothing to hide shaek.
        transform.localPosition = restLocalPosition + position * overallStrength;
        transform.localRotation = aimLocalRotation * Quaternion.Euler(rotation * overallStrength);

        previousVerticalSpeed = controller.velocity.y;
        previouslyGrounded = grounded;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || !initialized)
            return;

        transform.localPosition = restLocalPosition;
        transform.localRotation = aimLocalRotation;
    }

    private sealed class WaveGroup
    {
        private WaveAxis x;
        private WaveAxis y;
        private WaveAxis z;

        public WaveGroup(System.Random random)
        {
            x = new WaveAxis(random);
            y = new WaveAxis(random);
            z = new WaveAxis(random);
        }

        public Vector3 Sample(Vector3 amplitude, Vector3 frequency, float wander, float dt, float time)
        {
            return new Vector3(
                x.Sample(amplitude.x, frequency.x, wander, dt, time),
                y.Sample(amplitude.y, frequency.y, wander, dt, time),
                z.Sample(amplitude.z, frequency.z, wander, dt, time));
        }
    }

    private struct WaveAxis
    {
        private float primaryPhase;
        private float secondaryPhase;
        private readonly float wanderSeed;

        public WaveAxis(System.Random random)
        {
            primaryPhase = (float)random.NextDouble() * Mathf.PI * 2f;
            secondaryPhase = (float)random.NextDouble() * Mathf.PI * 2f;
            wanderSeed = (float)random.NextDouble() * 900f + 20f;
        }

        public float Sample(float amplitude, float frequency, float wander, float dt, float time)
        {
            float modulation = Mathf.PerlinNoise(wanderSeed, time * 0.24f) * 2f - 1f;
            float amplitudeDrift = 0.6f + 0.8f * Mathf.PerlinNoise(wanderSeed + 53.7f, time * 0.31f);
            float cycles = Mathf.Max(0f, frequency) * (1f + modulation * wander) * dt * Mathf.PI * 2f;
            primaryPhase = Mathf.Repeat(primaryPhase + cycles, Mathf.PI * 2f);
            secondaryPhase = Mathf.Repeat(secondaryPhase + cycles * 1.618f, Mathf.PI * 2f);
            return amplitude * amplitudeDrift * (0.72f * Mathf.Sin(primaryPhase) + 0.28f * Mathf.Sin(secondaryPhase));
        }
    }
}
