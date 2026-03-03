namespace com.noctuagames.sdk
{
    /// <summary>
    /// Provides locale data (language, country, currency) without
    /// depending on the Noctua static singleton.
    /// </summary>
    public interface ILocaleProvider
    {
        /// <summary>
        /// Gets the current language code (e.g. "en", "id", "vi").
        /// </summary>
        /// <returns>An ISO 639-1 language code string.</returns>
        string GetLanguage();

        /// <summary>
        /// Gets the current country code (e.g. "US", "ID", "VN").
        /// </summary>
        /// <returns>An ISO 3166-1 alpha-2 country code string.</returns>
        string GetCountry();

        /// <summary>
        /// Gets the current currency code (e.g. "USD", "IDR", "VND").
        /// </summary>
        /// <returns>An ISO 4217 currency code string.</returns>
        string GetCurrency();

        /// <summary>
        /// Gets the localized translation string for the given text key.
        /// </summary>
        /// <param name="textKey">The locale text key to look up.</param>
        /// <returns>The translated string, or a fallback if not found.</returns>
        string GetTranslation(LocaleTextKey textKey);
    }
}
