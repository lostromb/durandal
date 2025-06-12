using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils.Time
{
    public static class TimeZoneHelpers
    {
        private static readonly IDictionary<string, string> WINDOWS_TO_IANA_MAPPING = new Dictionary<string, string>()
        {
            { "Dateline Standard Time", "Etc/GMT-12" }, // International Date Line West
            { "Samoa Standard Time", "Pacific/Pago_Pago" }, // Midway, Samoa
            { "Hawaiian Standard Time", "Pacific/Honolulu" },
            { "Alaskan Standard Time", "America/Anchorage" }, // There are multiple zones in Alaska but we assume anchorage
            { "Pacific Standard Time", "America/Los_Angeles" },
            { "Mountain Standard Time", "America/Denver" },
            { "Mexico Standard Time 2", "America/Chihuahua" }, 
            { "U.S. Mountain Standard Time", "America/Phoenix" },
            { "Central Standard Time", "America/Chicago" },
            { "Canada Central Standard Time", "America/Yellowknife" }, // Saskatchewan
            { "Mexico Standard Time", "America/Mexico_City" }, // Guadalajara, Mexico City, Monterrey
            { "Central America Standard Time", "America/Merida" }, // Central America - guessing Merida
            { "Eastern Standard Time", "America/New_York" },
            { "U.S. Eastern Standard Time", "America/Indiana/Indianapolis" }, // Indiana (East)
            { "S.A. Pacific Standard Time", "America/Bogota" },
            { "Atlantic Standard Time", "America/Halifax" }, // Atlantic Time (Canada)
            { "S.A. Western Standard Time", "America/Argentina/San_Juan" }, // Georgetown, La Paz, San Juan
            { "Pacific S.A. Standard Time", "America/Santiago" },
            { "Newfoundland and Labrador Standard Time", "America/St_Johns" },
            { "E. South America Standard Time", "America/Sao_Paulo" }, // Brasilia
            { "S.A. Eastern Standard Time", "America/Guyana" }, // Georgetown
            { "Greenland Standard Time", "America/Godthab" },
            { "Mid-Atlantic Standard Time", "Etc/GMT-2" },
            { "Azores Standard Time", "Atlantic/Azores" },
            { "Cape Verde Standard Time", "Atlantic/Cape_Verde" },
            { "GMT Standard Time", "Etc/GMT" },
            { "Greenwich Standard Time", "Atlantic/Reykjavik" },
            { "Central Europe Standard Time", "Europe/Belgrade" },
            { "Central European Standard Time", "Europe/Warsaw" },
            { "Romance Standard Time", "Europe/Brussels" },
            { "W. Europe Standard Time", "Europe/Amsterdam" },
            { "W. Central Africa Standard Time", "Africa/Algiers" }, // Guessing here
            { "E. Europe Standard Time", "Europe/Minsk" },
            { "Egypt Standard Time", "Africa/Cairo" },
            { "FLE Standard Time", "Europe/Helsinki" },
            { "GTB Standard Time", "Europe/Athens" },
            { "Israel Standard Time", "Asia/Jerusalem" },
            { "South Africa Standard Time", "Africa/Johannesburg" },
            { "Russian Standard Time", "Europe/Moscow" },
            { "Arab Standard Time", "Asia/Riyadh" },
            { "E. Africa Standard Time", "Africa/Nairobi" },
            { "Arabic Standard Time", "Asia/Baghdad" },
            { "Iran Standard Time", "Asia/Tehran" },
            { "Arabian Standard Time", "Asia/Dubai" }, // Abu Dhabi
            { "Caucasus Standard Time", "Asia/Yerevan" }, // How different from Armenian Standard Time
            { "Transitional Islamic State of Afghanistan Standard Time", "Asia/Kabul" },
            { "Ekaterinburg Standard Time", "Asia/Yekaterinburg" },
            { "West Asia Standard Time", "Asia/Tashkent" },
            { "India Standard Time", "Asia/Kolkata" },
            { "Nepal Standard Time", "Asia/Kathmandu" },
            { "Central Asia Standard Time", "Asia/Dhaka" },
            { "Sri Lanka Standard Time", "Asia/Colombo" },
            { "N. Central Asia Standard Time", "Asia/Novosibirsk" },
            { "Myanmar Standard Time", "Asia/Yangon" },
            { "S.E. Asia Standard Time", "Asia/Bangkok" },
            { "North Asia Standard Time", "Asia/Krasnoyarsk" },
            { "China Standard Time", "Asia/Shanghai" },
            { "Singapore Standard Time", "Asia/Singapore" }, // also Asia/Kuala_Lumpur
            { "Taipei Standard Time", "Asia/Taipei" },
            { "W. Australia Standard Time", "Australia/Perth" },
            { "North Asia East Standard Time", "Asia/Irkutsk" },
            { "Korea Standard Time", "Asia/Seoul" },
            { "Tokyo Standard Time", "Asia/Tokyo" },
            { "Yakutsk Standard Time", "Asia/Yakutsk" },
            { "A.U.S. Central Standard Time", "Australia/Darwin" },
            { "Cen. Australia Standard Time", "Australia/Adelaide" },
            { "A.U.S. Eastern Standard Time", "Australia/Sydney" }, // also Australia/Melbourne
            { "E. Australia Standard Time", "Australia/Brisbane" },
            { "Tasmania Standard Time", "Australia/Hobart" },
            { "Vladivostok Standard Time", "Asia/Vladivostok" },
            { "West Pacific Standard Time", "Pacific/Guam" },
            { "Central Pacific Standard Time", "Asia/Magadan" },
            { "Fiji Islands Standard Time", "Pacific/Fiji" },
            { "New Zealand Standard Time", "Pacific/Auckland" },
            { "Tonga Standard Time", "Pacific/Tongatapu" },
            { "Azerbaijan Standard Time ", "Asia/Baku" },
            { "Middle East Standard Time", "Asia/Beirut" },
            { "Jordan Standard Time", "Asia/Amman" },
            { "Central Standard Time", "America/Monterrey" }, // How different from Mexico City? (GMT-06:00) Guadalajara, Mexico City, Monterrey - New
            { "Mountain Standard Time", "America/Chihuahua" }, // How different from mexico standard 2? (GMT-07:00) Chihuahua, La Paz, Mazatlan - New
            { "Pacific Standard Time", "America/Tijuana" },
            { "Namibia Standard Time", "Africa/Windhoek" },
            { "Georgian Standard Time", "Asia/Tbilisi" },
            { "Central Brazilian Standard Time", "America/Manaus" },
            { "Montevideo Standard Time", "America/Montevideo" },
            { "Armenian Standard Time", "Asia/Yerevan" },
            { "Venezuela Standard Time", "America/Caracas" },
            { "Argentina Standard Time", "America/Argentina/Buenos_Aires" },
            { "Morocco Standard Time", "Africa/Casablanca" },
            { "Pakistan Standard Time", "Asia/Karachi" },
            { "Mauritius Standard Time", "Indian/Mauritius" },
            { "UTC", "Etc/UTC" },
            { "Paraguay Standard Time", "America/Asuncion" },
            { "Kamchatka Standard Time", "Asia/Kamchatka" }
        };

        public static string MapWindowsToIANATimeZone(string windowsTimezoneName)
        {
            if (WINDOWS_TO_IANA_MAPPING.ContainsKey(windowsTimezoneName))
            {
                return WINDOWS_TO_IANA_MAPPING[windowsTimezoneName];
            }

            return string.Empty;
        }
    }
}
