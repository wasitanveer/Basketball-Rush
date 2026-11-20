using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BallThrowController : MonoBehaviour
{
    private const float CourtResetDelay = 1f;

    [Header("Aim")]
    [Tooltip("Camera used to decide the throw direction. If empty, Camera.main is used.")]
    public Camera aimCamera;

    [Tooltip("Upward angle of the shot in degrees. A high arc is needed for the ball to drop through the rim.")]
    public float launchAngle = 65f;

    [Tooltip("Maximum left/right aim angle in degrees.")]
    public float maxYawAngle = 35f;

    [Tooltip("Upward drag distance in pixels that gives maximum power.")]
    public float maxDragDistance = 300f;

    [Tooltip("Screen height in pixels the drag distances are tuned for. Drag distances scale with the actual screen height so power feels consistent at any resolution.")]
    public float referenceScreenHeight = 1080f;

    [Header("Input")]
    [Tooltip("When unchecked, the player cannot start a new throw. In-flight shots still finish and reset.")]
    public bool canThrow = true;

    [Header("Throw Power")]
    [Tooltip("Throw speed for the smallest valid upward drag.")]
    public float minPower = 4f;

    [Tooltip("Maximum throw speed. This is the power limit.")]
    public float maxPower = 14f;

    [Tooltip("Minimum upward drag in pixels required to throw.")]
    public float minUpwardDrag = 30f;

    [Header("Reset")]
    [Tooltip("Maximum time a shot may last before the ball is reset.")]
    public float shotTimeout = 6f;

    [Tooltip("Delay after the ball stops before it is reset.")]
    public float resetDelay = 1f;

    [Tooltip("The ball resets if it falls below this world Y position.")]
    public float fallResetHeight = -2f;

    [Header("Physics")]
    [Tooltip("Physics material used by the ball. If empty, the BasketballPhysics material is loaded from Resources.")]
    public PhysicsMaterial ballPhysicsMaterial;

    [Header("Aim Feel")]
    [Tooltip("How quickly the horizontal aim follows the mouse. Higher values are snappier.")]
    public float aimSmoothing = 20f;

    [Tooltip("Softens small left/right drags for more predictable aiming.")]
    public float horizontalAimExponent = 1.15f;

    [Tooltip("Fraction of the maximum drag ignored around the center when aiming left/right.")]
    public float horizontalDeadZone = 0.03f;

    [Header("Power Feel")]
    [Tooltip("Power curve exponent. Values below 1 give slightly more power at short drags.")]
    public float powerCurveExponent = 0.9f;

    [Header("Release Feedback")]
    public bool enableCameraKick = true;
    public float cameraKickBack = 0.03f;
    public float cameraKickDown = 0.01f;
    public float cameraKickDuration = 0.14f;

    public bool enableShotGlow = true;
    public float shotGlowDuration = 0.12f;
    public float shotGlowIntensity = 1.6f;

    [Header("Audio")]
    [Tooltip("Optional AudioSource for ball sounds. One is created automatically if this is empty.")]
    public AudioSource ballAudioSource;

    public AudioClip shotReleaseClip;
    public AudioClip courtBounceClip;
    public AudioClip rimHitClip;
    public AudioClip backboardHitClip;

    [Range(0f, 1f)] public float ballAudioVolume = 0.8f;
    [Tooltip("Minimum collision speed needed to play an impact sound.")]
    public float minImpactSoundSpeed = 1f;
    [Tooltip("Minimum time between ball impact sounds.")]
    public float impactSoundCooldown = 0.08f;

    [Tooltip("Optional explicit collision targets. Found by name if empty.")]
    public Transform courtTarget;
    public Transform rimTarget;
    public Transform backboardTarget;

    private Rigidbody ballRb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector2 dragStart;
    private Vector2 dragCurrent;
    private Vector2 smoothedDrag;
    private bool isDragging;
    private bool ballInFlight;
    private float shotTime;
    private float stoppedTime;
    private float nextImpactSoundTime;
    private bool courtResetPending;
    private float courtResetTimer;

    private Camera kickCamera;
    private Vector3 baseCameraPosition;
    private float cameraKickTimer;
    private bool cameraKickActive;

    private MeshRenderer ballRenderer;
    private MaterialPropertyBlock ballPropertyBlock;
    private float shotGlowTimer;
    private BasketballTrail ballTrail;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        ballRb = GetComponent<Rigidbody>();

        startPosition = transform.position;
        startRotation = transform.rotation;

        ballRb.mass = 0.62f;
        ballRb.linearDamping = 0.05f;
        ballRb.angularDamping = 0.08f;
        ballRb.maxAngularVelocity = 50f;
        ballRb.interpolation = RigidbodyInterpolation.Interpolate;
        ballRb.sleepThreshold = 0.01f;
        ballRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        ballRb.isKinematic = true;
        ballRb.useGravity = true;

        Collider ballCollider = GetComponent<Collider>();
        if (ballCollider != null)
        {
            if (ballPhysicsMaterial == null)
            {
                ballPhysicsMaterial = Resources.Load<PhysicsMaterial>("BasketballPhysics");
            }

            if (ballPhysicsMaterial == null)
            {
                ballPhysicsMaterial = new PhysicsMaterial("BasketballPhysics")
                {
                    dynamicFriction = 0.6f,
                    staticFriction = 0.6f,
                    bounciness = 0.65f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
            }

            ballCollider.sharedMaterial = ballPhysicsMaterial;
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }

        kickCamera = aimCamera;
        if (kickCamera != null)
        {
            baseCameraPosition = kickCamera.transform.position;
        }

        ballRenderer = GetComponent<MeshRenderer>();
        if (ballRenderer != null)
        {
            ballPropertyBlock = new MaterialPropertyBlock();
            ballRenderer.GetPropertyBlock(ballPropertyBlock);
            ballPropertyBlock.SetColor(EmissionColorId, Color.black);
            ballRenderer.SetPropertyBlock(ballPropertyBlock);
        }

        ballTrail = GetComponent<BasketballTrail>();
        if (ballTrail == null)
        {
            ballTrail = gameObject.AddComponent<BasketballTrail>();
        }

        SetupAudio();
    }

    public bool IsInFlight => ballInFlight;

    public float CurrentPowerNormalized
    {
        get
        {
            if (!isDragging || ballInFlight || !canThrow)
            {
                return 0f;
            }

            return CalculatePowerNormalized((dragCurrent - dragStart).y);
        }
    }

    private float DragResolutionScale => Mathf.Max(0.25f, Screen.height / Mathf.Max(1f, referenceScreenHeight));
    private float ScaledMaxDragDistance => Mathf.Max(1f, maxDragDistance * DragResolutionScale);
    private float ScaledMinUpwardDrag => Mathf.Max(1f, minUpwardDrag * DragResolutionScale);

    private float CalculatePowerNormalized(float upwardDrag)
    {
        float powerAmount = Mathf.Clamp01(upwardDrag / ScaledMaxDragDistance);
        return Mathf.Pow(powerAmount, Mathf.Max(0.01f, powerCurveExponent));
    }

    public void ResetBallNow()
    {
        if (ballInFlight)
        {
            ResetBall();
        }
    }

    public void RestartBall()
    {
        ResetBall();
        shotTime = 0f;
        stoppedTime = 0f;
        dragStart = Vector2.zero;
        dragCurrent = Vector2.zero;
        smoothedDrag = Vector2.zero;
        ClearShotGlow();
        StopCameraKick();
        canThrow = true;
    }

    private void Update()
    {
        if (ballInFlight)
        {
            UpdateFlight();
        }
        else if (canThrow)
        {
            UpdateAim();
        }
        else
        {
            isDragging = false;
        }

        UpdateTrailVisibility();
    }

    private void UpdateTrailVisibility()
    {
        if (ballTrail == null)
        {
            return;
        }

        ballTrail.SetTrailActive(ballInFlight && canThrow);
    }

    private void LateUpdate()
    {
        UpdateCameraKick();
        UpdateShotGlow();
    }

    private void UpdateAim()
    {
        if (!TryReadPointer(out Vector2 pointerPosition, out bool pressed, out bool held, out bool released))
        {
            return;
        }

        if (pressed)
        {
            isDragging = true;
            dragStart = pointerPosition;
            dragCurrent = dragStart;
            smoothedDrag = Vector2.zero;
        }
        else if (isDragging && held)
        {
            dragCurrent = pointerPosition;
            SmoothAimDrag(Time.deltaTime);
        }
        else if (isDragging && released)
        {
            dragCurrent = pointerPosition;
            SmoothAimDrag(0.05f);
            isDragging = false;
            ThrowBall();
        }
    }

    private static bool TryReadPointer(out Vector2 position, out bool pressed, out bool held, out bool released)
    {
        position = Vector2.zero;
        pressed = false;
        held = false;
        released = false;

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var primaryTouch = touchscreen.primaryTouch;
            if (primaryTouch.press.isPressed || primaryTouch.press.wasPressedThisFrame || primaryTouch.press.wasReleasedThisFrame)
            {
                position = primaryTouch.position.ReadValue();
                pressed = primaryTouch.press.wasPressedThisFrame;
                held = primaryTouch.press.isPressed;
                released = primaryTouch.press.wasReleasedThisFrame;
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            pressed = mouse.leftButton.wasPressedThisFrame;
            held = mouse.leftButton.isPressed;
            released = mouse.leftButton.wasReleasedThisFrame;
            return true;
        }

        return false;
    }

    private void SmoothAimDrag(float deltaTime)
    {
        Vector2 rawDrag = dragCurrent - dragStart;
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, aimSmoothing) * Mathf.Max(0.0001f, deltaTime));
        smoothedDrag = Vector2.Lerp(smoothedDrag, rawDrag, blend);
    }

    private void ThrowBall()
    {
        if (aimCamera == null)
        {
            return;
        }

        Vector2 rawDrag = dragCurrent - dragStart;
        if (rawDrag.y < ScaledMinUpwardDrag)
        {
            return;
        }

        float powerAmount = CalculatePowerNormalized(rawDrag.y);

        float horizontal = Mathf.Clamp(smoothedDrag.x / ScaledMaxDragDistance, -1f, 1f);
        float absHorizontal = Mathf.Abs(horizontal);
        if (absHorizontal <= horizontalDeadZone)
        {
            horizontal = 0f;
        }
        else
        {
            horizontal = Mathf.Sign(horizontal) *
                Mathf.Pow((absHorizontal - horizontalDeadZone) / (1f - horizontalDeadZone),
                Mathf.Max(0.01f, horizontalAimExponent));
        }

        float yaw = horizontal * maxYawAngle;

        Quaternion aimRotation = aimCamera.transform.rotation * Quaternion.Euler(-launchAngle, yaw, 0f);
        Vector3 throwDirection = aimRotation * Vector3.forward;
        float power = Mathf.Lerp(minPower, maxPower, powerAmount);

        ballRb.isKinematic = false;
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ballRb.linearVelocity = throwDirection * power;

        ballInFlight = true;
        shotTime = 0f;
        stoppedTime = 0f;
        courtResetPending = false;
        courtResetTimer = 0f;

        StartReleaseFeedback();
    }

    private void UpdateFlight()
    {
        shotTime += Time.deltaTime;

        if (transform.position.y < fallResetHeight)
        {
            ResetBall();
            return;
        }

        if (courtResetPending)
        {
            courtResetTimer += Time.deltaTime;

            if (courtResetTimer >= CourtResetDelay)
            {
                ResetBall();
            }

            return;
        }

        bool ballIsSlow = ballRb.linearVelocity.sqrMagnitude < 0.05f;
        bool timedOut = shotTime >= shotTimeout;

        if (timedOut || (ballIsSlow && shotTime > 0.75f))
        {
            stoppedTime += Time.deltaTime;

            if (stoppedTime >= resetDelay)
            {
                ResetBall();
            }
        }
        else
        {
            stoppedTime = 0f;
        }
    }

    private void ResetBall()
    {
        ballRb.isKinematic = false;
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        ballRb.isKinematic = true;

        transform.SetPositionAndRotation(startPosition, startRotation);

        ballInFlight = false;
        isDragging = false;
        smoothedDrag = Vector2.zero;
        nextImpactSoundTime = 0f;
        courtResetPending = false;
        courtResetTimer = 0f;
        ClearShotGlow();

        if (ballTrail != null)
        {
            ballTrail.ClearTrail();
        }

        if (ballAudioSource != null)
        {
            ballAudioSource.Stop();
        }
    }

    private void StartReleaseFeedback()
    {
        if (enableCameraKick && kickCamera != null && cameraKickDuration > 0f)
        {
            cameraKickTimer = cameraKickDuration;
            cameraKickActive = true;
        }

        if (enableShotGlow && ballRenderer != null && shotGlowDuration > 0f)
        {
            shotGlowTimer = shotGlowDuration;
        }

        PlayBallSound(shotReleaseClip, 0.65f);
    }

    private void UpdateCameraKick()
    {
        if (!cameraKickActive || kickCamera == null)
        {
            return;
        }

        cameraKickTimer -= Time.deltaTime;

        float t = cameraKickDuration > 0f ? Mathf.Clamp01(cameraKickTimer / cameraKickDuration) : 0f;
        float eased = t * t;
        Vector3 offset = (-kickCamera.transform.forward * cameraKickBack - kickCamera.transform.up * cameraKickDown) * eased;
        kickCamera.transform.position = baseCameraPosition + offset;

        if (cameraKickTimer <= 0f)
        {
            StopCameraKick();
        }
    }

    private void StopCameraKick()
    {
        cameraKickActive = false;
        cameraKickTimer = 0f;

        if (kickCamera != null)
        {
            kickCamera.transform.position = baseCameraPosition;
        }
    }

    private void UpdateShotGlow()
    {
        if (shotGlowTimer <= 0f || ballRenderer == null || ballPropertyBlock == null)
        {
            return;
        }

        shotGlowTimer -= Time.deltaTime;

        float t = shotGlowDuration > 0f ? Mathf.Clamp01(shotGlowTimer / shotGlowDuration) : 0f;
        float eased = t * t;
        Color glow = new Color(
            shotGlowIntensity * eased,
            shotGlowIntensity * 0.35f * eased,
            shotGlowIntensity * 0.08f * eased,
            1f);

        ballPropertyBlock.SetColor(EmissionColorId, glow);
        ballRenderer.SetPropertyBlock(ballPropertyBlock);

        if (shotGlowTimer <= 0f)
        {
            ClearShotGlow();
        }
    }

    private void ClearShotGlow()
    {
        shotGlowTimer = 0f;

        if (ballRenderer != null && ballPropertyBlock != null)
        {
            ballPropertyBlock.SetColor(EmissionColorId, Color.black);
            ballRenderer.SetPropertyBlock(ballPropertyBlock);
        }
    }

    private void SetupAudio()
    {
        if (ballAudioSource == null)
        {
            ballAudioSource = GetComponent<AudioSource>();
        }

        if (ballAudioSource == null)
        {
            ballAudioSource = gameObject.AddComponent<AudioSource>();
        }

        ballAudioSource.playOnAwake = false;
        ballAudioSource.loop = false;
        ballAudioSource.spatialBlend = 0f;
        ballAudioSource.volume = 1f;

        if (shotReleaseClip == null) shotReleaseClip = Resources.Load<AudioClip>("Audio/ShotRelease");
        if (courtBounceClip == null) courtBounceClip = Resources.Load<AudioClip>("Audio/CourtBounce");
        if (rimHitClip == null) rimHitClip = Resources.Load<AudioClip>("Audio/RimHit");
        if (backboardHitClip == null) backboardHitClip = Resources.Load<AudioClip>("Audio/BackboardHit");

        if (courtTarget == null)
        {
            GameObject court = GameObject.Find("Court");
            if (court != null) courtTarget = court.transform;
        }

        if (rimTarget == null)
        {
            GameObject rim = GameObject.Find("Rim");
            if (rim != null) rimTarget = rim.transform;
        }

        if (backboardTarget == null)
        {
            GameObject backboard = GameObject.Find("Backboard");
            if (backboard != null) backboardTarget = backboard.transform;
        }
    }

    private void PlayBallSound(AudioClip clip, float volume)
    {
        if (clip == null || ballAudioSource == null)
        {
            return;
        }

        ballAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume * ballAudioVolume));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!ballInFlight || collision.collider == null)
        {
            return;
        }

        GameObject hitObject = collision.collider.gameObject;

        if (courtTarget != null && hitObject == courtTarget.gameObject)
        {
            StartCourtResetCountdown();
        }

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSoundSpeed || Time.time < nextImpactSoundTime)
        {
            return;
        }

        AudioClip clip = null;

        if (courtTarget != null && hitObject == courtTarget.gameObject)
        {
            clip = courtBounceClip;
        }
        else if (rimTarget != null && hitObject == rimTarget.gameObject)
        {
            clip = rimHitClip;
        }
        else if (backboardTarget != null && hitObject == backboardTarget.gameObject)
        {
            clip = backboardHitClip;
        }

        if (clip == null)
        {
            return;
        }

        float impact = Mathf.InverseLerp(minImpactSoundSpeed, 10f, speed);
        PlayBallSound(clip, Mathf.Lerp(0.35f, 0.85f, impact));
        nextImpactSoundTime = Time.time + Mathf.Max(0.02f, impactSoundCooldown);
    }

    private void StartCourtResetCountdown()
    {
        if (courtResetPending)
        {
            return;
        }

        courtResetPending = true;
        courtResetTimer = 0f;
    }
}
