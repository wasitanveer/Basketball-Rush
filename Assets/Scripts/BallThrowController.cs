using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BasketballState
{
    Free,
    Controlled,
    Charging,
    InFlight
}

[RequireComponent(typeof(Rigidbody))]
public class BallThrowController : MonoBehaviour
{
    private const float CourtResetDelay = 2f;

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
    [Tooltip("When unchecked, the player cannot control or shoot the ball. In-flight shots still finish and reset.")]
    public bool canThrow = true;

    [Header("Ball Control")]
    [Tooltip("Maximum distance at which the player can pick up the ball.")]
    public float ballPickupRadius = 2f;

    [Tooltip("Seconds of holding R required to reach maximum shot power.")]
    public float maxChargeTime = 1.2f;

    [Tooltip("Optional point where a controlled ball is held. Found by name if empty.")]
    public Transform ballControlPoint;

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

    [Tooltip("How long a made basket is celebrated before the ball resets, so the player can see it drop through the net.")]
    public float basketResetDelay = 1f;

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
    [Tooltip("Random pitch range applied to impact sounds so repeated bounces do not sound identical.")]
    [Range(0f, 0.3f)] public float impactPitchVariation = 0.09f;
    [Tooltip("Minimum collision speed needed to play an impact sound.")]
    public float minImpactSoundSpeed = 1f;
    [Tooltip("Minimum time between ball impact sounds.")]
    public float impactSoundCooldown = 0.08f;

    [Tooltip("Optional explicit collision targets. Found by name if empty.")]
    public Transform courtTarget;
    public Transform rimTarget;
    public Transform backboardTarget;

    private const int BallAudioSourceCount = 4;

    private Rigidbody ballRb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector2 dragStart;
    private Vector2 dragCurrent;
    private Vector2 smoothedDrag;
    private bool isDragging;
    private bool pointerWasHeldLastFrame;
    private bool ballInFlight;
    private float shotTime;
    private float stoppedTime;
    private float nextImpactSoundTime;
    private bool courtResetPending;
    private float courtResetTimer;
    private bool basketResetPending;
    private float basketResetTimer;
    private bool scoredThisShot;
    private int rimContactsThisShot;
    private int backboardContactsThisShot;
    private AudioSource[] ballAudioSources;
    private int nextAudioSourceIndex;

    private Camera kickCamera;
    private Vector3 baseCameraPosition;
    private float cameraKickTimer;
    private bool cameraKickActive;

    private MeshRenderer ballRenderer;
    private MaterialPropertyBlock ballPropertyBlock;
    private float shotGlowTimer;
    private BasketballTrail ballTrail;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private Transform playerTransform;
    private Collider ballCollider;
    private float chargeNormalized;

    public BasketballState State { get; private set; } = BasketballState.Free;
    public bool IsCharging => State == BasketballState.Charging;
    public bool IsInFlight => ballInFlight;

    public bool HadRimOrBackboardContactThisShot => rimContactsThisShot > 0 || backboardContactsThisShot > 0;

    public event Action<bool> ShotEnded;

    public float CurrentPowerNormalized
    {
        get
        {
            if (State != BasketballState.Charging || ballInFlight || !canThrow)
            {
                return 0f;
            }

            return Mathf.Clamp01(chargeNormalized);
        }
    }

    private float DragResolutionScale => Mathf.Max(0.25f, Screen.height / Mathf.Max(1f, referenceScreenHeight));
    private float ScaledMaxDragDistance => Mathf.Max(1f, maxDragDistance * DragResolutionScale);
    private float ScaledMinUpwardDrag => Mathf.Max(1f, minUpwardDrag * DragResolutionScale);

    private void Awake()
    {
        ballRb = GetComponent<Rigidbody>();
        ballCollider = GetComponent<Collider>();
        playerTransform = GameObject.Find("Player")?.transform;
        if (ballControlPoint == null)
        {
            GameObject controlPointObject = GameObject.Find("BallControlPoint");
            if (controlPointObject != null) ballControlPoint = controlPointObject.transform;
        }

        if (ballCollider != null && playerTransform != null)
        {
            Collider playerCollider = playerTransform.GetComponent<Collider>();
            if (playerCollider != null) Physics.IgnoreCollision(ballCollider, playerCollider, true);
        }

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
        ballRb.isKinematic = false;
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        State = BasketballState.Free;
    }

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

    public void CancelAim()
    {
        if (State == BasketballState.Charging)
        {
            chargeNormalized = 0f;
            State = BasketballState.Controlled;
        }
    }

    public void RestartBall()
    {
        ResetBall();
        shotTime = 0f;
        stoppedTime = 0f;
        chargeNormalized = 0f;
        State = BasketballState.Free;
        ClearShotGlow();
        StopCameraKick();
        canThrow = true;
    }

    public void OnBasketScored()
    {
        scoredThisShot = true;
        basketResetPending = true;
        basketResetTimer = 0f;
    }

    private void Update()
    {
        if (ballInFlight)
        {
            UpdateFlight();
        }
        else
        {
            UpdateKeyboardControl(Keyboard.current);
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
            pointerWasHeldLastFrame = false;
            return;
        }

        bool justPressed = pressed || (held && !pointerWasHeldLastFrame);
        pointerWasHeldLastFrame = held;

        if (justPressed)
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
        else if (isDragging && (released || !held))
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
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        ballRb.linearVelocity = throwDirection * power;

        ballInFlight = true;
        shotTime = 0f;
        stoppedTime = 0f;
        courtResetPending = false;
        courtResetTimer = 0f;
        basketResetPending = false;
        basketResetTimer = 0f;
        scoredThisShot = false;
        rimContactsThisShot = 0;
        backboardContactsThisShot = 0;

        StartReleaseFeedback();
    }

    private void UpdateKeyboardControl(Keyboard keyboard)
    {
        if (State == BasketballState.Free)
        {
            if (canThrow && keyboard != null && keyboard.fKey.wasPressedThisFrame) TryPickupBall();
            return;
        }

        if (State != BasketballState.Controlled && State != BasketballState.Charging) return;

        PositionControlledBall();

        if (!canThrow)
        {
            if (State == BasketballState.Charging) CancelAim();
            return;
        }

        if (keyboard == null) return;

        if (keyboard.fKey.wasPressedThisFrame)
        {
            DropBall();
            return;
        }

        if (State == BasketballState.Controlled)
        {
            if (keyboard.rKey.wasPressedThisFrame) BeginCharge();
            return;
        }

        chargeNormalized = Mathf.Clamp01(chargeNormalized + Time.deltaTime / Mathf.Max(0.01f, maxChargeTime));
        if (keyboard.rKey.wasReleasedThisFrame || !keyboard.rKey.isPressed) ShootBall();
    }

    private bool CanPickupBall()
    {
        if (!canThrow || State != BasketballState.Free || ballInFlight || playerTransform == null) return false;

        Vector3 offset = transform.position - playerTransform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= ballPickupRadius * ballPickupRadius;
    }

    private void TryPickupBall()
    {
        if (!CanPickupBall()) return;

        State = BasketballState.Controlled;
        chargeNormalized = 0f;
        ballRb.isKinematic = true;
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        PositionControlledBall();
    }

    private void DropBall()
    {
        if (State != BasketballState.Controlled && State != BasketballState.Charging) return;

        State = BasketballState.Free;
        chargeNormalized = 0f;
        ballRb.isKinematic = false;
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.useGravity = true;
    }

    private void BeginCharge()
    {
        if (State != BasketballState.Controlled || !canThrow) return;
        chargeNormalized = 0f;
        State = BasketballState.Charging;
    }

    private void ShootBall()
    {
        if (State != BasketballState.Charging || aimCamera == null) return;

        PositionControlledBall();

        float powerAmount = Mathf.Pow(Mathf.Clamp01(chargeNormalized), Mathf.Max(0.01f, powerCurveExponent));
        Quaternion aimRotation = aimCamera.transform.rotation * Quaternion.Euler(-launchAngle, 0f, 0f);
        Vector3 throwDirection = aimRotation * Vector3.forward;
        float power = Mathf.Lerp(minPower, maxPower, powerAmount);

        ballRb.isKinematic = false;
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        ballRb.useGravity = true;
        ballRb.linearVelocity = throwDirection * power;

        Vector3 spinAxis = Vector3.Cross(Vector3.up, throwDirection);
        if (spinAxis.sqrMagnitude > 0.0001f)
        {
            ballRb.angularVelocity = spinAxis.normalized * -4f;
        }

        ballInFlight = true;
        State = BasketballState.InFlight;
        chargeNormalized = 0f;
        shotTime = 0f;
        stoppedTime = 0f;
        courtResetPending = false;
        courtResetTimer = 0f;
        basketResetPending = false;
        basketResetTimer = 0f;
        scoredThisShot = false;
        rimContactsThisShot = 0;
        backboardContactsThisShot = 0;

        StartReleaseFeedback();
    }

    private void PositionControlledBall()
    {
        Vector3 position = GetControlledBallPosition();
        transform.position = position;
    }

    private Vector3 GetControlledBallPosition()
    {
        if (ballControlPoint != null) return ballControlPoint.position;
        if (aimCamera != null)
        {
            return aimCamera.transform.position + aimCamera.transform.forward * 0.45f - aimCamera.transform.up * 0.15f;
        }
        return transform.position;
    }

    private void UpdateFlight()
    {
        shotTime += Time.deltaTime;

        if (transform.position.y < fallResetHeight)
        {
            ResetBall();
            return;
        }

        if (basketResetPending)
        {
            basketResetTimer += Time.deltaTime;

            if (basketResetTimer >= basketResetDelay)
            {
                ResetBall();
            }

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
        bool wasInFlight = ballInFlight;
        bool wasScored = scoredThisShot;

        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        ballRb.isKinematic = false;
        ballRb.useGravity = true;

        transform.SetPositionAndRotation(startPosition, startRotation);

        ballInFlight = false;
        State = BasketballState.Free;
        chargeNormalized = 0f;
        isDragging = false;
        pointerWasHeldLastFrame = false;
        smoothedDrag = Vector2.zero;
        nextImpactSoundTime = 0f;
        courtResetPending = false;
        courtResetTimer = 0f;
        basketResetPending = false;
        basketResetTimer = 0f;
        scoredThisShot = false;
        rimContactsThisShot = 0;
        backboardContactsThisShot = 0;
        ClearShotGlow();

        if (ballTrail != null)
        {
            ballTrail.ClearTrail();
        }

        StopBallSounds();

        if (wasInFlight)
        {
            ShotEnded?.Invoke(wasScored);
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

        Haptics.Light();
        PlayBallSound(shotReleaseClip, 0.65f, 1f);
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
        if (ballAudioSources == null || ballAudioSources.Length != BallAudioSourceCount)
        {
            ballAudioSources = new AudioSource[BallAudioSourceCount];

            for (int i = 0; i < BallAudioSourceCount; i++)
            {
                AudioSource source;

                if (i == 0)
                {
                    source = ballAudioSource;
                    if (source == null)
                    {
                        source = GetComponent<AudioSource>();
                    }

                    if (source == null)
                    {
                        source = gameObject.AddComponent<AudioSource>();
                    }
                }
                else
                {
                    source = gameObject.AddComponent<AudioSource>();
                }

                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.dopplerLevel = 0.15f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 2f;
                source.maxDistance = 30f;
                source.volume = 1f;
                source.pitch = 1f;

                ballAudioSources[i] = source;
            }
        }

        ballAudioSource = ballAudioSources[0];

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

    private void PlayBallSound(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || ballAudioSources == null || ballAudioSources.Length == 0)
        {
            return;
        }

        AudioSource source = ballAudioSources[nextAudioSourceIndex];
        nextAudioSourceIndex = (nextAudioSourceIndex + 1) % ballAudioSources.Length;

        if (source == null)
        {
            return;
        }

        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.PlayOneShot(clip, Mathf.Clamp01(volume * ballAudioVolume));
    }

    private void StopBallSounds()
    {
        if (ballAudioSources == null)
        {
            return;
        }

        for (int i = 0; i < ballAudioSources.Length; i++)
        {
            if (ballAudioSources[i] != null)
            {
                ballAudioSources[i].Stop();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!ballInFlight || collision.collider == null)
        {
            return;
        }

        GameObject hitObject = collision.collider.gameObject;
        bool hitCourt = courtTarget != null && hitObject == courtTarget.gameObject;
        bool hitRim = rimTarget != null && hitObject == rimTarget.gameObject;
        bool hitBackboard = backboardTarget != null && hitObject == backboardTarget.gameObject;

        if (hitRim)
        {
            rimContactsThisShot++;
        }

        if (hitBackboard)
        {
            backboardContactsThisShot++;
        }

        if (hitCourt)
        {
            StartCourtResetCountdown();
        }

        float speed = collision.relativeVelocity.magnitude;

        if (speed >= 0.8f)
        {
            if (hitRim)
            {
                Haptics.Medium();
            }
            else if (hitBackboard)
            {
                Haptics.Light();
            }
        }

        if (speed < minImpactSoundSpeed || Time.time < nextImpactSoundTime)
        {
            return;
        }

        AudioClip clip = null;

        if (hitCourt)
        {
            clip = courtBounceClip;
        }
        else if (hitRim)
        {
            clip = rimHitClip;
        }
        else if (hitBackboard)
        {
            clip = backboardHitClip;
        }

        if (clip == null)
        {
            return;
        }

        float impact = Mathf.InverseLerp(minImpactSoundSpeed, 10f, speed);
        float pitchVariation = Mathf.Max(0f, impactPitchVariation);
        float pitch = UnityEngine.Random.Range(1f - pitchVariation, 1f + pitchVariation);
        PlayBallSound(clip, Mathf.Lerp(0.35f, 0.85f, impact), pitch);
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
