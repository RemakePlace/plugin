using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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

        internal static string Localize(string toLocalize,int langId)
        {
            string localizedString = null;
            if (LocalizationStrings.ContainsKey(toLocalize)){
                ArrayList localizedStrings = null;
                LocalizationStrings.TryGetValue(toLocalize,out localizedStrings);
                localizedString = localizedStrings[langId].ToString();
            }
            if (localizedString == null) {
                localizedString = String.Format("String \"{0}\" not localized",toLocalize);
            }
            return localizedString;
        }



    }
}

