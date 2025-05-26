namespace com.noctuagames.sdk.AdPlaceholder
{
    public class AdPlaceholderManager
    {
        private static AdPlaceholderManager _instance;
        public static AdPlaceholderManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AdPlaceholderManager();
                }
                return _instance;
            }
        }

        private IAdPlaceholder interstitial;

        public void InitAdPlaceHolder()
        {
            interstitial = new PlaceholderInterstitialAd();
        }

        public void LoadInterstitial()
        {
            interstitial.Load();
        }

        public void ShowInterstitial()
        {
            interstitial.Show();
        }

        public void CloseInterstitial()
        {
            interstitial.Close();
        }
    }
}