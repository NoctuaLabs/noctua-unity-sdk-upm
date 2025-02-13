using System.Collections.Generic;

public class Country
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string PhoneCode { get; set; }

    public Country(string code, string name, string phoneCode)
    {
        Code = code;
        Name = name;
        PhoneCode = phoneCode;
    }
}

public static class CountryData
{
    public static List<Country> Countries = new List<Country>
    {
        new Country("AF", "Afghanistan", "+93"),
        new Country("AM", "Armenia", "+374"),
        new Country("AZ", "Azerbaijan", "+994"),
        new Country("BH", "Bahrain", "+973"),
        new Country("BD", "Bangladesh", "+880"),
        new Country("BT", "Bhutan", "+975"),
        new Country("BN", "Brunei Darussalam", "+673"),
        new Country("KH", "Cambodia", "+855"),
        new Country("CN", "China", "+86"),
        new Country("GE", "Georgia", "+995"),
        new Country("HK", "Hong Kong", "+852"),
        new Country("IN", "India", "+91"),
        new Country("ID", "Indonesia", "+62"),
        new Country("IR", "Iran, Islamic Republic of", "+98"),
        new Country("IQ", "Iraq", "+964"),
        new Country("IL", "Israel", "+972"),
        new Country("JP", "Japan", "+81"),
        new Country("JO", "Jordan", "+962"),
        new Country("KZ", "Kazakhstan", "+7"),
        new Country("KR", "Korea, Republic of", "+82"),
        new Country("KW", "Kuwait", "+965"),
        new Country("KG", "Kyrgyzstan", "+996"),
        new Country("LA", "Lao People's Democratic Republic", "+856"),
        new Country("MY", "Malaysia", "+60"),
        new Country("MV", "Maldives", "+960"),
        new Country("MN", "Mongolia", "+976"),
        new Country("MM", "Myanmar", "+95"),
        new Country("NP", "Nepal", "+977"),
        new Country("OM", "Oman", "+968"),
        new Country("PK", "Pakistan", "+92"),
        new Country("PH", "Philippines", "+63"),
        new Country("QA", "Qatar", "+974"),
        new Country("SA", "Saudi Arabia", "+966"),
        new Country("SG", "Singapore", "+65"),
        new Country("LK", "Sri Lanka", "+94"),
        new Country("SY", "Syrian Arab Republic", "+963"),
        new Country("TW", "Taiwan, Province of China", "+886"),
        new Country("TJ", "Tajikistan", "+992"),
        new Country("TH", "Thailand", "+66"),
        new Country("TM", "Turkmenistan", "+993"),
        new Country("AE", "United Arab Emirates", "+971"),
        new Country("UZ", "Uzbekistan", "+998"),
        new Country("VN", "Viet Nam", "+84"),
        new Country("YE", "Yemen", "+967"),
        new Country("AX", "Åland Islands", "+358"),
        new Country("AL", "Albania", "+355"),
        new Country("DZ", "Algeria", "+213"),
        new Country("AS", "American Samoa", "+1-684"),
        new Country("AD", "Andorra", "+376"),
        new Country("AO", "Angola", "+244"),
        new Country("AI", "Anguilla", "+1-264"),
        new Country("AQ", "Antarctica", "+672"),
        new Country("AG", "Antigua and Barbuda", "+1-268"),
        new Country("AR", "Argentina", "+54"),
        new Country("AW", "Aruba", "+297"),
        new Country("AU", "Australia", "+61"),
        new Country("AT", "Austria", "+43"),
        new Country("BS", "Bahamas", "Unknown"),
        new Country("BB", "Barbados", "Unknown"),
        new Country("BY", "Belarus", "+375"),
        new Country("BE", "Belgium", "+32"),
        new Country("BZ", "Belize", "+501"),
        new Country("BJ", "Benin", "+229"),
        new Country("BM", "Bermuda", "+1-441"),
        new Country("BO", "Bolivia, Plurinational State of", "+591"),
        new Country("BQ", "Bonaire, Sint Eustatius and Saba", "+599"),
        new Country("BA", "Bosnia and Herzegovina", "+387"),
        new Country("BW", "Botswana", "+267"),
        new Country("BV", "Bouvet Island", "Unknown"),
        new Country("BR", "Brazil", "+55"),
        new Country("IO", "British Indian Ocean Territory", "+246"),
        new Country("BG", "Bulgaria", "+359"),
        new Country("BF", "Burkina Faso", "+226"),
        new Country("BI", "Burundi", "+257"),
        new Country("CM", "Cameroon", "Unknown"),
        new Country("CA", "Canada", "Unknown"),
        new Country("CV", "Cape Verde", "+238"),
        new Country("KY", "Cayman Islands", "+1-345"),
        new Country("CF", "Central African Republic", "+236"),
        new Country("TD", "Chad", "+235"),
        new Country("CL", "Chile", "+56"),
        new Country("CX", "Christmas Island", "+61"),
        new Country("CC", "Cocos (Keeling) Islands", "+61"),
        new Country("CO", "Colombia", "+57"),
        new Country("KM", "Comoros", "+269"),
        new Country("CG", "Congo", "+242"),
        new Country("CD", "Congo, the Democratic Republic of the", "+243"),
        new Country("CK", "Cook Islands", "+682"),
        new Country("CR", "Costa Rica", "+506"),
        new Country("CI", "Côte d'Ivoire", "+225"),
        new Country("HR", "Croatia", "+385"),
        new Country("CU", "Cuba", "+53"),
        new Country("CW", "Curaçao", "+599"),
        new Country("CY", "Cyprus", "+357"),
        new Country("CZ", "Czech Republic", "+420"),
        new Country("DK", "Denmark", "+45"),
        new Country("DJ", "Djibouti", "+253"),
        new Country("DM", "Dominica", "+1-767"),
        new Country("DO", "Dominican Republic", "+1-809"),
        new Country("EC", "Ecuador", "+593"),
        new Country("EG", "Egypt", "+20"),
        new Country("SV", "El Salvador", "+503"),
        new Country("GQ", "Equatorial Guinea", "+240"),
        new Country("ER", "Eritrea", "+291"),
        new Country("EE", "Estonia", "+372"),
        new Country("ET", "Ethiopia", "+251"),
        new Country("FK", "Falkland Islands (Malvinas)", "Unknown"),
        new Country("FO", "Faroe Islands", "+298"),
        new Country("FJ", "Fiji", "+679"),
        new Country("FI", "Finland", "+358"),
        new Country("FR", "France", "+33"),
        new Country("GF", "French Guiana", "+594"),
        new Country("PF", "French Polynesia", "+689"),
        new Country("TF", "French Southern Territories", "+262"),
        new Country("GA", "Gabon", "+241"),
        new Country("GM", "Gambia", "+220"),
        new Country("DE", "Germany", "+49"),
        new Country("GH", "Ghana", "+233"),
        new Country("GI", "Gibraltar", "+350"),
        new Country("GR", "Greece", "+30"),
        new Country("GL", "Greenland", "+299"),
        new Country("GD", "Grenada", "+1-473"),
        new Country("GP", "Guadeloupe", "Unknown"),
        new Country("GU", "Guam", "+1-671"),
        new Country("GT", "Guatemala", "+502"),
        new Country("GG", "Guernsey", "+44"),
        new Country("GN", "Guinea", "+224"),
        new Country("GW", "Guinea-Bissau", "+245"),
        new Country("GY", "Guyana", "+592"),
        new Country("HT", "Haiti", "+509"),
        new Country("HM", "Heard Island and McDonald Islands", "Unknown"),
        new Country("VA", "Holy See (Vatican City State)", "+39"),
        new Country("HN", "Honduras", "+504"),
        new Country("HU", "Hungary", "+36"),
        new Country("IS", "Iceland", "+354"),
        new Country("IE", "Ireland", "+353"),
        new Country("IM", "Isle of Man", "+44"),
        new Country("IT", "Italy", "+39"),
        new Country("JM", "Jamaica", "+1-876"),
        new Country("JE", "Jersey", "+44"),
        new Country("KE", "Kenya", "+254"),
        new Country("KI", "Kiribati", "+686"),
        new Country("KP", "Korea, Democratic People's Republic of", "+850"),
        new Country("LV", "Latvia", "+371"),
        new Country("LB", "Lebanon", "+961"),
        new Country("LS", "Lesotho", "+266"),
        new Country("LR", "Liberia", "+231"),
        new Country("LY", "Libya", "+218"),
        new Country("LI", "Liechtenstein", "+423"),
        new Country("LT", "Lithuania", "+370"),
        new Country("LU", "Luxembourg", "+352"),
        new Country("MO", "Macao", "+853"),
        new Country("MK", "Macedonia, the Former Yugoslav Republic of", "+389"),
        new Country("MG", "Madagascar", "+261"),
        new Country("MW", "Malawi", "+265"),
        new Country("ML", "Mali", "+223"),
        new Country("MT", "Malta", "+356"),
        new Country("MH", "Marshall Islands", "+692"),
        new Country("MQ", "Martinique", "+596"),
        new Country("MR", "Mauritania", "+222"),
        new Country("MU", "Mauritius", "+230"),
        new Country("YT", "Mayotte", "+262"),
        new Country("MX", "Mexico", "+52"),
        new Country("FM", "Micronesia, Federated States of", "+691"),
        new Country("MD", "Moldova, Republic of", "+373"),
        new Country("MC", "Monaco", "+377"),
        new Country("ME", "Montenegro", "+382"),
        new Country("MS", "Montserrat", "+1-664"),
        new Country("MA", "Morocco", "+212"),
        new Country("MZ", "Mozambique", "+258"),
        new Country("NA", "Namibia", "+264"),
        new Country("NR", "Nauru", "+674"),
        new Country("NL", "Netherlands", "+31"),
        new Country("NC", "New Caledonia", "+687"),
        new Country("NZ", "New Zealand", "+64"),
        new Country("NI", "Nicaragua", "+505"),
        new Country("NE", "Niger", "+227"),
        new Country("NG", "Nigeria", "+234"),
        new Country("NU", "Niue", "+683"),
        new Country("NF", "Norfolk Island", "+672"),
        new Country("MP", "Northern Mariana Islands", "+1-670"),
        new Country("NO", "Norway", "+47"),
        new Country("PW", "Palau", "+680"),
        new Country("PS", "Palestine, State of", "+970"),
        new Country("PA", "Panama", "+507"),
        new Country("PG", "Papua New Guinea", "+675"),
        new Country("PY", "Paraguay", "+595"),
        new Country("PE", "Peru", "+51"),
        new Country("PN", "Pitcairn", "+64"),
        new Country("PL", "Poland", "+48"),
        new Country("PT", "Portugal", "+351"),
        new Country("PR", "Puerto Rico", "+1-787"),
        new Country("RE", "Réunion", "+262"),
        new Country("RO", "Romania", "+40"),
        new Country("RU", "Russian Federation", "+7"),
        new Country("RW", "Rwanda", "+250"),
        new Country("BL", "Saint Barthélemy", "+590"),
        new Country("SH", "Saint Helena, Ascension and Tristan da Cunha", "+290"),
        new Country("KN", "Saint Kitts and Nevis", "+1-869"),
        new Country("LC", "Saint Lucia", "+1-758"),
        new Country("MF", "Saint Martin (French part)", "+590"),
        new Country("PM", "Saint Pierre and Miquelon", "+508"),
        new Country("VC", "Saint Vincent and the Grenadines", "+1-784"),
        new Country("WS", "Samoa", "+685"),
        new Country("SM", "San Marino", "+378"),
        new Country("ST", "Sao Tome and Principe", "+239"),
        new Country("SN", "Senegal", "+221"),
        new Country("RS", "Serbia", "+381"),
        new Country("SC", "Seychelles", "+248"),
        new Country("SL", "Sierra Leone", "+232"),
        new Country("SX", "Sint Maarten (Dutch part)", "+1-721"),
        new Country("SK", "Slovakia", "+421"),
        new Country("SI", "Slovenia", "+386"),
        new Country("SB", "Solomon Islands", "+677"),
        new Country("SO", "Somalia", "+252"),
        new Country("ZA", "South Africa", "+27"),
        new Country("GS", "South Georgia and the South Sandwich Islands", "+500"),
        new Country("SS", "South Sudan", "+211"),
        new Country("ES", "Spain", "+34"),
        new Country("SD", "Sudan", "+249"),
        new Country("SR", "Suriname", "+597"),
        new Country("SJ", "Svalbard and Jan Mayen", "+47"),
        new Country("SZ", "Swaziland", "+268"),
        new Country("SE", "Sweden", "+46"),
        new Country("CH", "Switzerland", "+41"),
        new Country("TZ", "Tanzania, United Republic of", "+255"),
        new Country("TL", "Timor-Leste", "+670"),
        new Country("TG", "Togo", "+228"),
        new Country("TK", "Tokelau", "+690"),
        new Country("TO", "Tonga", "+676"),
        new Country("TT", "Trinidad and Tobago", "+1-868"),
        new Country("TN", "Tunisia", "+216"),
        new Country("TR", "Turkey", "+90"),
        new Country("TC", "Turks and Caicos Islands", "+1-649"),
        new Country("TV", "Tuvalu", "+688"),
        new Country("UG", "Uganda", "+256"),
        new Country("UA", "Ukraine", "+380"),
        new Country("GB", "United Kingdom", "+44"),
        new Country("US", "United States", "+1"),
        new Country("UM", "United States Minor Outlying Islands", "+1"),
        new Country("UY", "Uruguay", "+598"),
        new Country("VU", "Vanuatu", "+678"),
        new Country("VE", "Venezuela, Bolivarian Republic of", "+58"),
        new Country("VG", "Virgin Islands, British", "+1-284"),
        new Country("VI", "Virgin Islands, U.S.", "+1-340"),
        new Country("WF", "Wallis and Futuna", "+681"),
        new Country("EH", "Western Sahara", "+212"),
        new Country("ZM", "Zambia", "+260"),
        new Country("ZW", "Zimbabwe", "+263")
    };
}
