using System.Collections;
using System.Diagnostics.Tracing;
using com.noctuagames.sdk.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    public class NoctuaBehaviour : MonoBehaviour
    {
        private GameObject _noctuaGameObject;
        private WelcomeNotificationPresenter _welcome;

        private void Awake()
        {
            _noctuaGameObject = new GameObject("NoctuaUIGameObject");
            _noctuaGameObject.transform.SetParent(gameObject.transform);
            _noctuaGameObject.SetActive(true);
            var uiDoc = _noctuaGameObject.AddComponent<UIDocument>();
            uiDoc.visualTreeAsset = ScriptableObject.CreateInstance<VisualTreeAsset>();
            uiDoc.panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");

            _welcome = _noctuaGameObject.AddComponent<WelcomeNotificationPresenter>();

            Noctua.Init();
        }

        private void Start()
        {
            StartCoroutine(StartNoctua());
        }

        private IEnumerator StartNoctua()
        {
            yield return Noctua.Auth.Authenticate();
            yield return _welcome.Show();
        }
    }
}