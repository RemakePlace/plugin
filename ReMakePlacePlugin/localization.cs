using System;
using System.Collections;
using System.Collections.Generic;

namespace ReMakePlacePlugin
{

    public static class Localization
    {
        public enum Lang
        {
            en,
            cn,
            fr,
            de,
            jp
        }
        public static Dictionary<string, ArrayList> LocalizationStrings = new Dictionary<string, ArrayList>(){
            {"Level", new ArrayList() {"Level","楼","Étage","Stock",null}}
        };

        public static string Localize(string toLocalize)
        {
            
            string localizedString = null;
            if (LocalizationStrings.ContainsKey(toLocalize))
            {
                ArrayList localizedStrings = null;
                LocalizationStrings.TryGetValue(toLocalize, out localizedStrings);
                if (localizedStrings[(int)Configuration.PluginLang] != null)
                {
                    localizedString = localizedStrings[(int)Configuration.PluginLang].ToString();
                }
                else
                {
                    localizedString = String.Format("String \"{0}\" not localized for language \"{1}\"", toLocalize, Configuration.PluginLang);
                }
            }
            return localizedString;
        }



    }
}

