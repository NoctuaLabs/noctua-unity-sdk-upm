using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// A custom UI Toolkit visual element that displays a rotating spinner animation, used as a loading indicator.
    /// </summary>
    public class Spinner : VisualElement
    {
        private const float StepAngle = 45f; // Each step is 45 degrees
        private float _currentAngle = 0f;
        private IVisualElementScheduledItem _scheduledRotation;

        /// <summary>
        /// Gets or sets the interval in seconds between each rotation step.
        /// </summary>
        public float StepInterval { get; set; } = 0.1f; // Default interval of 0.3 seconds

        /// <summary>
        /// Creates a spinner with the specified pixel dimensions.
        /// </summary>
        /// <param name="_width">The width in pixels.</param>
        /// <param name="_height">The height in pixels.</param>
        public Spinner(float _width, float _height)
        {
            style.width = _width;
            style.height = _height;

            Initialize();
        }

        /// <summary>
        /// Creates a spinner that fills 100% of its parent container.
        /// </summary>
        public Spinner()
        {                   
            style.width = new StyleLength(Length.Percent(100));
            style.height = new StyleLength(Length.Percent(100));

            Initialize();
        }

        private void Initialize()
        {
            // Set up the spinner style and background image     
            name = "Spinner";
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

        /// <summary>
        /// UXML factory for creating Spinner elements in UXML documents.
        /// </summary>
        public new class UxmlFactory : UxmlFactory<Spinner, UxmlTraits> { }

        /// <summary>
        /// UXML traits for the Spinner element, exposing the step-interval attribute.
        /// </summary>
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
