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
        /*
        The key is what should be referenced elsewhere when a string is in need of localization.
        Language order within a specific dictonary entry corresponds to the index of the language in the Lang enum.
        Entries left null will show an error in the UI stating the key for the string and that it is not localized.
        When using Localization.Localize() make sure the key exists in this dictonary or it will crash dalamud.
        */
        public static Dictionary<string, ArrayList> LocalizationStrings = new Dictionary<string, ArrayList>(){
            {"langName", new ArrayList() {"English","中文","Française","Deutsche","日本語"}},
            {"lang", new ArrayList() {"Language","语言","Langue","Sprache","言語"}},
            {"level", new ArrayList() {"Level","层级","Étage","Stock","階"}},
            {"fixture", new ArrayList() {"Fixture","固定位置",null,null,null}},
            {"item", new ArrayList() {"Item","家具","Objet",null,null}},
            {"intFurn", new ArrayList() {"Interior Furniture","室内家具",null,null,null}},
            {"extFurn", new ArrayList() {"Exterior Furniture","室外庭具",null,null,null}},
            {"intFixt", new ArrayList() {"Interior Fixtures","室内固定位置",null,null,null}},
            {"extFixt", new ArrayList() {"Exterior Fixtures","室外固定位置",null,null,null}},
            {"unusedFurn", new ArrayList() {"Unused Furniture","未在列表上的已放置家具",null,null,null}},
            {"islePlaceError", new ArrayList() {"(Manage Furnishings -> Place Furnishing Glamours)","(管理庭具 -> 投影布置庭具)",null,null,null}},
            {"housePlaceError", new ArrayList() {"(Housing -> Indoor/Outdoor Furnishings)","(房屋 -> 布置家具)",null,null,null}},
            {"layoutError", new ArrayList() {"Unable to save layouts outside of Layout mode","无法在布局模式之外保存布局",null,null,null}},
            {"rotateError", new ArrayList() {"Unable to load and apply layouts outside of Rotate Layout mode","在旋转布局模式外无法加载和应用布局",null,null,null}},
            {"labelFurn", new ArrayList() {"Label Furniture","家具标签",null,null,null}},
            {"showNames", new ArrayList() {"Show furniture names on the screen","为已放置家具添加名称标签",null,null,null}},
            {"shopTooltips", new ArrayList() {"Show Tooltips","显示工具提示",null,null,null}},
            {"layout", new ArrayList() {"Layout","布局","Aménagement",null,null}},
            {"curFileLoc", new ArrayList() {"Current file location","当前文件位置",null,null,null}},
            {"save", new ArrayList() {"Save","保存","Enregistrer",null,null}},
            {"saveToCurFile", new ArrayList() {"Save layout to current file location","布局保存到当前文件位置",null,null,null}},
            {"saveAs", new ArrayList() {"Save As","保存至",null,null,null}},
            {"saveToLoc", new ArrayList() {"Select a Save Location","选择保存位置",null,null,null}},
            {"saveToFile", new ArrayList() {"Save layout to file","将布局保存到文件",null,null,null}},
            {"load", new ArrayList() {"Load","加载",null,null,null}},
            {"loadFromCurFile", new ArrayList() {"Load layout from current file location","从当前文件位置加载布局",null,null,null}},
            {"loadFrom", new ArrayList() {"Load From","选择文件",null,null,null}},
            {"selectFile", new ArrayList() {"Select a Layout File","选择布局文件",null,null,null}},
            {"loadFromFile", new ArrayList() {"Load layout from file","从文件中加载布局",null,null,null}},
            {"applyLayout", new ArrayList() {"Apply Layout","应用布局",null,null,null}},
            {"placeInterval", new ArrayList() {"Placement Interval (ms)","放置间隔（毫秒）",null,null,null}},
            {"placeIntervalWarn", new ArrayList() {"Time interval between furniture placements when applying a layout. If this is too low (e.g. 200 ms), some placements may be skipped over.","应用布局时家具放置操作之间的时间间隔。若该间隔过短（如 200 毫秒），某些放置操作可能会被跳过。",null,null,null}},
            {"selectedFloors", new ArrayList() {"Selected Floors","选择楼层",null,null,null}},
            {"basement", new ArrayList() {"Basement","地下室","Sous-Sol",null,null}},
            {"groundFloor", new ArrayList() {"Ground Floor","一楼","rez-de-chaussée",null,null}},
            {"upperFloor", new ArrayList() {"Upper Floor","二楼","étage supérieur",null,null}},
            {"clear", new ArrayList() {"Clear","清除","effacer",null,null}},
            {"fixtList", new ArrayList() {"Fixture List","固定位置列表",null,null,null}},
            {"missingWarn", new ArrayList() {"Note: Missing items, incorrect dyes, and items on unselected floors are grayed out","注释：缺失的物品、不正确的染料以及未选中楼层的物品均显示为灰色。",null,null,null}},
            {"position", new ArrayList() {"Position","坐标",null,null,null}},
            {"rotation", new ArrayList() {"Rotation","角度",null,null,null}},
            {"dyeMat", new ArrayList() {"Dye/Material","染剂/材料",null,null,null}},
            {"setPos", new ArrayList() {"Set Position","放置坐标",null,null,null}},
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

