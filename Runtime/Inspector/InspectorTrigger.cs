using System;
using UnityEngine;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// Listens for user gestures that should toggle the Noctua Inspector:
    ///  * shake 3× within 1 s (device)
    ///  * 4-finger tap  (emulator-friendly)
    ///  * Ctrl/⌘+Shift+D (editor / keyboard fallback)
    ///
    /// Attached to the auto-spawned <c>__NoctuaInspector</c> GameObject when
    /// <see cref="Noctua.IsSandbox"/> is true. Fires <see cref="OnTrigger"/>
    /// which the controller consumes to show/hide the overlay.
    /// </summary>
    public class InspectorTrigger : MonoBehaviour
    {
        private const float ShakeThresholdG = 2.7f;
        private const float LowPassAlpha = 0.1f;
        private const int ShakesNeeded = 3;
        private const float ShakeWindowSec = 1.0f;
        private const float ShakeCooldownSec = 2.0f;

        public event Action OnTrigger;

        private Vector3 _lowPass = Vector3.zero;
        private int _shakeCount = 0;
        private float _firstShakeAt = 0f;
        private float _lastFireAt = 0f;

        private void Update()
        {
            if (Time.realtimeSinceStartup - _lastFireAt < ShakeCooldownSec) return;

            if (DetectShake() || DetectFourFingerTap() || DetectKeyboardCombo())
            {
                _lastFireAt = Time.realtimeSinceStartup;
                _shakeCount = 0;
                try { OnTrigger?.Invoke(); } catch { /* swallow */ }
            }
        }

        private bool DetectShake()
        {
            // Some platforms (Editor, Desktop) return zero; skip cheaply.
            var accel = Input.acceleration;
            if (accel == Vector3.zero) return false;

            // High-pass via exponential moving-average low-pass subtraction.
            _lowPass = Vector3.Lerp(_lowPass, accel, LowPassAlpha);
            var hp = accel - _lowPass;
            var magnitude = hp.magnitude;
            if (magnitude < ShakeThresholdG) return false;

            var now = Time.realtimeSinceStartup;
            if (_shakeCount == 0 || now - _firstShakeAt > ShakeWindowSec)
            {
                _firstShakeAt = now;
                _shakeCount = 1;
                return false;
            }
            _shakeCount++;
            return _shakeCount >= ShakesNeeded;
        }

        private bool DetectFourFingerTap()
        {
            if (Input.touchCount != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetTouch(i).phase != TouchPhase.Began) return false;
            }
            return true;
        }

        private bool DetectKeyboardCombo()
        {
            // Ctrl+Shift+D (also macOS Cmd+Shift+D) — editor/desktop fallback.
            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return ctrl && shift && Input.GetKeyDown(KeyCode.D);
        }
    }
}
