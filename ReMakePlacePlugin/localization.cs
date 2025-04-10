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
            {"langName", new ArrayList() {"English","中文","Française","Deutsche","日本語"}},
            {"lang", new ArrayList() {"Language","文","Langue","Sprache","言語"}},
            {"level", new ArrayList() {"Level","楼","Étage","Stock","階"}},
            {"fixture", new ArrayList() {"Fixture",null,null,null,null}},
            {"item", new ArrayList() {"Item",null,"Objet",null,null}},
            {"intFurn", new ArrayList() {"Interior Furniture",null,null,null,null}},
            {"extFurn", new ArrayList() {"Exterior Furniture",null,null,null,null}},
            {"intFixt", new ArrayList() {"Interior Fixtures",null,null,null,null}},
            {"extFixt", new ArrayList() {"Exterior Fixtures",null,null,null,null}},
            {"unusedFurn", new ArrayList() {"Unused Furniture",null,null,null,null}},
            {"islePlaceError", new ArrayList() {"(Manage Furnishings -> Place Furnishing Glamours)",null,null,null,null}},
            {"housePlaceError", new ArrayList() {"(Housing -> Indoor/Outdoor Furnishings)",null,null,null,null}},
            {"layoutError", new ArrayList() {"Unable to save layouts outside of Layout mode",null,null,null,null}},
            {"rotateError", new ArrayList() {"Unable to load and apply layouts outside of Rotate Layout mode",null,null,null,null}},
            {"labelFurn", new ArrayList() {"Label Furniture",null,null,null,null}},
            {"showNames", new ArrayList() {"Show furniture names on the screen",null,null,null,null}},
            {"shopTooltips", new ArrayList() {"Show Tooltips",null,null,null,null}},
            {"layout", new ArrayList() {"Layout",null,"Aménagement",null,null}},
            {"curFileLoc", new ArrayList() {"Current file location",null,null,null,null}},
            {"save", new ArrayList() {"Save",null,"Enregistrer",null,null}},
            {"saveToCurFile", new ArrayList() {"Save layout to current file location",null,null,null,null}},
            {"saveAs", new ArrayList() {"Save As",null,null,null,null}},
            {"saveToLoc", new ArrayList() {"Select a Save Location",null,null,null,null}},
            {"saveToFile", new ArrayList() {"Save layout to file",null,null,null,null}},
            {"load", new ArrayList() {"Load",null,null,null,null}},
            {"loadFromCurFile", new ArrayList() {"Load layout from current file location",null,null,null,null}},
            {"loadFrom", new ArrayList() {"Load From",null,null,null,null}},
            {"selectFile", new ArrayList() {"Select a Layout File",null,null,null,null}},
            {"loadFromFile", new ArrayList() {"Load layout from file",null,null,null,null}},
            {"applyLayout", new ArrayList() {"Apply Layout",null,null,null,null}},
            {"placeInterval", new ArrayList() {"Placement Interval (ms)",null,null,null,null}},
            {"placeIntervalWarn", new ArrayList() {"Time interval between furniture placements when applying a layout. If this is too low (e.g. 200 ms), some placements may be skipped over.",null,null,null,null}},
            {"selectedFloors", new ArrayList() {"Selected Floors",null,null,null,null}},
            {"basement", new ArrayList() {"Basement",null,"Sous-Sol",null,null}},
            {"groundFloor", new ArrayList() {"Ground Floor",null,"rez-de-chaussée",null,null}},
            {"upperFloor", new ArrayList() {"Upper Floor",null,null,null,null}},
            {"clear", new ArrayList() {"Clear",null,null,null,null}},
            {"fixtList", new ArrayList() {"Fixture List",null,null,null,null}},
            {"missingWarn", new ArrayList() {"Note: Missing items, incorrect dyes, and items on unselected floors are grayed out",null,null,null,null}},
            {"position", new ArrayList() {"Position",null,null,null,null}},
            {"rotation", new ArrayList() {"Rotation",null,null,null,null}},
            {"dyeMat", new ArrayList() {"Dye/Material",null,null,null,null}},
            {"setPos", new ArrayList() {"Set Position",null,null,null,null}},
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

