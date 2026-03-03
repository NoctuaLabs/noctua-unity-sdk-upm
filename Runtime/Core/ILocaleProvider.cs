namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides locale data (language, country, currency) without
    /// depending on the Noctua static singleton.
    /// </summary>
    public interface ILocaleProvider
    {
        string GetLanguage();
        string GetCountry();
        string GetCurrency();
    }
}
