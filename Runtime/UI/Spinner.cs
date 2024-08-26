using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class Spinner : VisualElement
    {
        private float rotationSpeed = 1000f;  // Set rotation speed
        private VisualElement rotatingRectangle;

        public Spinner()
        {
            // Set up the rotating rectangle as a child of the Spinner
            rotatingRectangle = new VisualElement();
            rotatingRectangle.style.width = 2;   // Width
            rotatingRectangle.style.height = 24; // Height (15 times the width)
            rotatingRectangle.style.backgroundColor = new Color(0.231f, 0.510f, 0.965f);  // Hex color #3B82F6

            // Add the rectangle to the Spinner element
            this.Add(rotatingRectangle);

            // Register the callback for the update event
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // Start the rotation animation when the geometry is ready
            RegisterCallback<DetachFromPanelEvent>(evt => UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged));
            Application.onBeforeRender += Rotate;
        }

        private void Rotate()
        {
            // Rotate the rectangle forever
            float angle = rotationSpeed * Time.deltaTime;
            rotatingRectangle.transform.rotation = Quaternion.Euler(0, 0, rotatingRectangle.transform.rotation.eulerAngles.z + angle);
        }

        // Optionally, you can expose a method to set the rotation speed
        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }
    }
}
