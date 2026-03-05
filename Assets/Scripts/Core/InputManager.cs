using UnityEngine;

namespace PaperIO.Core
{
    /// <summary>
    /// Reads player input from keyboard, mouse, and touch (virtual joystick).
    /// Outputs a normalised TurnInput value in [-1, 1].
    ///
    /// Attach to the GameManager or a dedicated Input GameObject.
    /// Wire the JoystickBackground and JoystickHandle RectTransforms from
    /// the UI canvas for touch joystick support.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [Header("Virtual Joystick UI (optional)")]
        [Tooltip("The background circle of the on-screen joystick.")]
        public RectTransform joystickBackground;

        [Tooltip("The inner handle of the on-screen joystick.")]
        public RectTransform joystickHandle;

        [Tooltip("Joystick maximum handle radius in pixels.")]
        public float joystickRadius = 35f;

        [Tooltip("Joystick dead zone in pixels (input ignored inside).")]
        public float joystickDeadZone = 10f;

        // ── Public output ──────────────────────────────────────────────────────
        /// <summary>Horizontal turn intent in [-1, 1]. Positive = turn right.</summary>
        public float TurnInput { get; private set; }

        /// <summary>Set to true when touch joystick is active so keyboard is ignored.</summary>
        public bool JoystickActive { get; private set; }

        // ── Internal state ─────────────────────────────────────────────────────
        private int    _joystickTouchId = -1;
        private Vector2 _joystickOrigin;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            TurnInput = 0f;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            ReadKeyboard();
            ReadMouse();
#endif
            ReadTouch();

            // Joystick overrides keyboard when active.
            if (!JoystickActive)
                ReadKeyboard();
        }

        // ─────────────────────────────────────────────────────────────────────
        #region Keyboard

        private void ReadKeyboard()
        {
            if (Input.GetKey(KeyCode.LeftArrow)  || Input.GetKey(KeyCode.A))
                TurnInput = -1f;
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
                TurnInput = 1f;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Mouse (web / desktop fallback)

        private bool _mouseDown;
        private Vector2 _mousePrev;

        private void ReadMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _mouseDown = true;
                _mousePrev = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0))
                _mouseDown = false;

            if (_mouseDown)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _mousePrev;
                _mousePrev = Input.mousePosition;
                if (Mathf.Abs(delta.x) > 2f)
                    TurnInput = Mathf.Clamp(delta.x / 20f, -1f, 1f);
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────────────
        #region Touch / virtual joystick

        private void ReadTouch()
        {
            if (Input.touchCount == 0)
            {
                ResetJoystick();
                return;
            }

            foreach (Touch touch in Input.touches)
            {
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        // Only claim the left side of the screen for the joystick.
                        if (touch.position.x < Screen.width * 0.5f && _joystickTouchId == -1)
                        {
                            _joystickTouchId = touch.fingerId;
                            _joystickOrigin  = touch.position;
                            JoystickActive   = true;
                        }
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (touch.fingerId == _joystickTouchId)
                            UpdateJoystick(touch.position);
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (touch.fingerId == _joystickTouchId)
                            ResetJoystick();
                        break;
                }
            }
        }

        private void UpdateJoystick(Vector2 touchPos)
        {
            Vector2 delta = touchPos - _joystickOrigin;
            float distance = delta.magnitude;

            // Clamp handle to radius.
            Vector2 clamped = delta.normalized * Mathf.Min(distance, joystickRadius);

            // Update joystick handle visual if wired.
            if (joystickHandle != null)
                joystickHandle.anchoredPosition = clamped;

            // Dead zone.
            if (distance < joystickDeadZone)
            {
                TurnInput = 0f;
                return;
            }

            // X-axis of the joystick maps to turn input.
            TurnInput = Mathf.Clamp(clamped.x / joystickRadius, -1f, 1f);
        }

        private void ResetJoystick()
        {
            _joystickTouchId = -1;
            JoystickActive   = false;
            TurnInput        = 0f;

            if (joystickHandle != null)
                joystickHandle.anchoredPosition = Vector2.zero;
        }

        #endregion
    }
}
