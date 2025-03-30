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

        public static string Localize(string toLocalize, int langId)
        {
            string localizedString = null;
            if (LocalizationStrings.ContainsKey(toLocalize))
            {
                ArrayList localizedStrings = null;
                LocalizationStrings.TryGetValue(toLocalize, out localizedStrings);
                if (localizedStrings[langId] != null)
                {
                    localizedString = localizedStrings[langId].ToString();
                }
                else
                {
                    localizedString = String.Format("String \"{0}\" not localized for language \"{1}\"", toLocalize, ((Lang)langId).ToString());
                }
            }
            return localizedString;
        }



    }
}

