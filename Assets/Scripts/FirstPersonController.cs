using UnityEngine;
using UnityEngine.InputSystem;

namespace PawnFix
{
    /// First-person controller: WASD/arrow move, mouse look, Shift sprint, C crouch, Space jump.
    /// Touch: left half of the screen = move stick, right half = look drag.
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        public Transform cameraPivot;

        [Header("Movement")]
        public float walkSpeed = 2.6f;
        public float sprintSpeed = 4.2f;
        public float crouchSpeed = 1.3f;
        public float acceleration = 12f;
        public float jumpHeight = 0.9f;
        public float gravity = -18f;

        [Header("Look")]
        public float mouseSensitivity = 0.12f;
        public float touchSensitivity = 0.15f;
        public float minPitch = -80f, maxPitch = 85f;

        [Header("Crouch")]
        public float standHeight = 1.75f;
        public float crouchHeight = 1.1f;
        public float eyeOffset = 0.12f;

        [Header("Feel")]
        public float bobAmount = 0.035f;
        public float bobFrequency = 9f;

        [Header("Game State")]
        [Tooltip("Cursor lock mode used only while active gameplay is running. Confined keeps Basketball Rush pointer drag-to-shoot working.")]
        public CursorLockMode gameplayCursorLock = CursorLockMode.Confined;

        [Tooltip("Hide the cursor during active gameplay. Menus, pause, and game over always show it.")]
        public bool hideCursorDuringGameplay = true;

        [Tooltip("Phase 1 is desktop-first. Touch movement/look is disabled because Basketball Rush already uses touch for shooting and UI.")]
        public bool enableTouchInput = false;

        [Tooltip("Optional. When the ball can be thrown and the pointer is held, player look/move is paused so drag-to-shoot is not disturbed.")]
        public BallThrowController ballThrowController;

        public bool LookEnabled { get; set; } = true;
        public bool MoveEnabled { get; set; } = true;

        CharacterController cc;
        Vector3 velocity;
        Vector3 planarVel;
        float pitch, yaw, bobT, currentHeight;
        bool crouching;
        int moveTouchId = -1, lookTouchId = -1;
        Vector2 moveTouchStart, moveTouchVec;
        bool cursorLocked;
        bool cursorVisible = true;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (!cameraPivot)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null) cameraPivot = childCamera.transform;
            }
            if (ballThrowController == null) ballThrowController = FindAnyObjectByType<BallThrowController>();
            yaw = transform.eulerAngles.y;
            currentHeight = standHeight;
            cc.height = standHeight;
            cc.center = new Vector3(0, standHeight / 2f, 0);
            SetCursorLocked(false);
        }

        void Start()
        {
            SetCursorLocked(false);
        }

        void OnDisable()
        {
            SetCursorLocked(false);
        }

        public void SetCursorLocked(bool locked)
        {
            if (Application.isMobilePlatform) return;

            CursorLockMode targetLock = locked ? gameplayCursorLock : CursorLockMode.None;
            bool targetVisible = locked ? !hideCursorDuringGameplay : true;

            if (cursorLocked == locked && cursorVisible == targetVisible &&
                Cursor.lockState == targetLock && Cursor.visible == targetVisible)
            {
                return;
            }

            Cursor.lockState = targetLock;
            Cursor.visible = targetVisible;
            cursorLocked = locked;
            cursorVisible = targetVisible;
        }

        bool GameplayInputActive()
        {
            if (Time.timeScale <= 0.0001f) return false;

            GameHUD hud = GameHUD.Instance;
            if (hud == null) return false;

            return hud.HasGameStarted && !hud.IsCountdownActive && !hud.IsTimerFinished;
        }

        bool PointerAiming()
        {
            return ballThrowController != null &&
                   ballThrowController.canThrow &&
                   !Application.isMobilePlatform &&
                   Mouse.current != null &&
                   Mouse.current.leftButton.isPressed;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool gameplayActive = GameplayInputActive();
            bool aiming = gameplayActive && PointerAiming();
            bool canLook = gameplayActive && LookEnabled && !aiming;
            bool canMove = gameplayActive && MoveEnabled && !aiming;

            SetCursorLocked(gameplayActive);

            Vector2 moveInput = Vector2.zero;
            Vector2 lookDelta = Vector2.zero;
            bool sprint = false, jump = false;

            if (kb != null && canMove)
            {
                moveInput.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                moveInput.y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                sprint = kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
                if (kb.cKey.wasPressedThisFrame) crouching = !crouching;
            }

            if (mouse != null && canLook && Cursor.lockState != CursorLockMode.None)
                lookDelta += mouse.delta.ReadValue() * mouseSensitivity;

            if (enableTouchInput && (canMove || canLook))
                ReadTouch(ref moveInput, ref lookDelta);

            if (canLook)
            {
                yaw += lookDelta.x;
                pitch = Mathf.Clamp(pitch - lookDelta.y, minPitch, maxPitch);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            if (!canMove) moveInput = Vector2.zero;

            // crouch (stand up only if there is headroom)
            if (!crouching && currentHeight < standHeight - 0.05f && !HasHeadroom()) crouching = true;
            float targetH = crouching ? crouchHeight : standHeight;
            currentHeight = Mathf.Lerp(currentHeight, targetH, Time.deltaTime * 10f);
            cc.height = currentHeight;
            cc.center = new Vector3(0, currentHeight / 2f, 0);

            float speed = crouching ? crouchSpeed : (sprint ? sprintSpeed : walkSpeed);
            Vector3 wish = (transform.right * moveInput.x + transform.forward * moveInput.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            planarVel = Vector3.Lerp(planarVel, wish * speed, Time.deltaTime * acceleration);

            if (cc.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -2f;
                if (jump && !crouching && canMove) velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            velocity.y += gravity * Time.deltaTime;
            cc.Move((planarVel + Vector3.up * velocity.y) * Time.deltaTime);

            // head bob
            float hSpeed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
            if (cc.isGrounded && hSpeed > 0.2f) bobT += Time.deltaTime * bobFrequency * (hSpeed / walkSpeed);
            else bobT = Mathf.Lerp(bobT, Mathf.Round(bobT / Mathf.PI) * Mathf.PI, Time.deltaTime * 6f);
            float bob = Mathf.Sin(bobT) * bobAmount * Mathf.Clamp01(hSpeed / walkSpeed);
            cameraPivot.localPosition = new Vector3(0, currentHeight - eyeOffset + bob, 0);
        }

        bool HasHeadroom()
        {
            float r = cc.radius * 0.9f;
            Vector3 from = transform.position + Vector3.up * (currentHeight - r);
            return !Physics.SphereCast(from, r, Vector3.up, out _, standHeight - currentHeight, ~0, QueryTriggerInteraction.Ignore);
        }

        void ReadTouch(ref Vector2 move, ref Vector2 look)
        {
            var ts = Touchscreen.current;
            if (ts == null) return;
            float half = Screen.width * 0.5f;
            foreach (var t in ts.touches)
            {
                int id = t.touchId.ReadValue();
                var phase = t.phase.ReadValue();
                Vector2 pos = t.position.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (pos.x < half && moveTouchId < 0) { moveTouchId = id; moveTouchStart = pos; }
                    else if (pos.x >= half && lookTouchId < 0) lookTouchId = id;
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    if (id == moveTouchId) { moveTouchId = -1; moveTouchVec = Vector2.zero; }
                    if (id == lookTouchId) lookTouchId = -1;
                }
                else
                {
                    if (id == moveTouchId) moveTouchVec = Vector2.ClampMagnitude((pos - moveTouchStart) / (Screen.height * 0.12f), 1f);
                    if (id == lookTouchId) look += t.delta.ReadValue() * touchSensitivity;
                }
            }
            if (moveTouchId >= 0) move += moveTouchVec;
        }
    }
}
