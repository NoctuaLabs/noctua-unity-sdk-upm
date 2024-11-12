using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class Spinner : VisualElement
    {
        private const float StepAngle = 45f; // Each step is 45 degrees
        private float _currentAngle = 0f;
        private IVisualElementScheduledItem _scheduledRotation;

        public float StepInterval { get; set; } = 0.1f; // Default interval of 0.3 seconds

        // Constructor
        public Spinner()
        {
            // Set up the spinner style and background image
            style.width = new StyleLength(Length.Percent(100));
            style.height = new StyleLength(Length.Percent(100));
            style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>("Spinner")); // Replace with your image path
            style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.ScaleToFit);

            // Start the self-rotation
            StartRotation();
        }

        // Method to rotate the spinner in steps
        private void RotateStep()
        {
            _currentAngle = (_currentAngle + StepAngle) % 360f;
            style.rotate = new Rotate(_currentAngle);
        }

        private void StartRotation()
        {
            // Cancel any existing schedule
            StopRotation();

            // Schedule the rotation with a repeating interval
            _scheduledRotation = schedule.Execute(RotateStep).Every((long)(StepInterval * 1000));
        }

        private void StopRotation()
        {
            _scheduledRotation?.Pause();
            _scheduledRotation = null;
        }

        // Register the Spinner as a custom control for UXML
        public new class UxmlFactory : UxmlFactory<Spinner, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlFloatAttributeDescription _stepInterval = new() { name = "step-interval", defaultValue = 0.3f };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var spinner = (Spinner)ve;
                spinner.StepInterval = _stepInterval.GetValueFromBag(bag, cc);
                spinner.StartRotation();
            }
        }
    }

}
