using Lumina.Excel.Sheets;
using ReMakePlacePlugin.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;


namespace ReMakePlacePlugin
{
    public static class Utils
    {
        public static string GetExteriorPartDescriptor(ExteriorPartsType partsType)
        {
            return partsType switch
            {
                ExteriorPartsType.Roof => "Roof",
                ExteriorPartsType.Walls => "Exterior Wall",
                ExteriorPartsType.Windows => "Window",
                ExteriorPartsType.Door => "Door",
                ExteriorPartsType.RoofOpt => "Roof Decor",
                ExteriorPartsType.WallOpt => "Exterior Wall Decor",
                ExteriorPartsType.SignOpt => "Placard",
                ExteriorPartsType.Fence => "Fence",
                _ => "Unknown"
            };
        }

        public static string GetInteriorPartDescriptor(InteriorPartsType partsType)
        {
            return partsType switch
            {
                InteriorPartsType.Walls => "Wall",
                InteriorPartsType.Windows => "Window",
                InteriorPartsType.Door => "Door",
                InteriorPartsType.Floor => "Floor",
                InteriorPartsType.Light => "Light",
                _ => "Unknown"
            };
        }

        public static string GetFloorDescriptor(InteriorFloor floor)
        {
            return floor switch
            {
                InteriorFloor.Ground => "Ground Floor",
                InteriorFloor.Basement => "Basement",
                InteriorFloor.Upstairs => "Upper Floor",
                InteriorFloor.External => "Main",
                _ => "Unknown"
            };
        }

        public static float DistanceFromPlayer(HousingGameObject obj, Vector3 playerPos)
        {
            return Distance(new Vector3(playerPos.X, playerPos.Y, playerPos.Z), new Vector3(obj.X, obj.Y, obj.Z));
        }

        public static double FastDistance(Vector3 v1, Vector3 v2) // for comparison, when actual distance doesn't matter
        {
            var x1 = Math.Pow(v2.X - v1.X, 2);
            var y1 = Math.Pow(v2.Y - v1.Y, 2);
            var z1 = Math.Pow(v2.Z - v1.Z, 2);

            return x1 + y1 + z1;
        }

        public static float Distance(Vector3 v1, Vector3 v2)
        {
            return (float)Math.Sqrt(FastDistance(v1, v2));
        }

        public static void StainButton(string id, Stain color, Vector2 size)
        {
            var floatColor = StainToVector4(color.Color);
            ImGui.ColorButton($"##{id}", floatColor, ImGuiColorEditFlags.NoTooltip, size);
        }

        public static Vector4 StainToVector4(uint stainColor)
        {
            var s = 1.0f / 255.0f;

            return new Vector4()
            {
                X = ((stainColor >> 16) & 0xFF) * s,
                Y = ((stainColor >> 8) & 0xFF) * s,
                Z = ((stainColor >> 0) & 0xFF) * s,
                W = ((stainColor >> 24) & 0xFF) * s
            };
        }

        public static HousingItem GetNearestHousingItem(IEnumerable<HousingItem> items, Vector3 position)
        {
            return items
                .OrderBy(item => FastDistance(position, new Vector3(item.X, item.Y, item.Z)))
                .FirstOrDefault();
        }

        public static void OpenLink(String address)
        {
            Dalamud.Utility.Util.OpenLink(address);
        }

        public static void TeamcraftExport(Dictionary<string,int>itemList)
        {
            var teamcraftLink = MakeTeamcraftList(itemList);
            OpenLink(teamcraftLink);
        }

        public static class Base64Url
        {
            public static string Encode(string text)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-')
                    .Replace('/', '_');
            }

            public static string Decode(string text)
            {
                text = text.Replace('_', '/').Replace('-', '+');
                switch (text.Length % 4)
                {
                    case 2:
                        text += "==";
                        break;
                    case 3:
                        text += "=";
                        break;
                }
                return Encoding.UTF8.GetString(Convert.FromBase64String(text));
            }
        }

        public static string MakeTeamcraftList(Dictionary<string,int> itemList)
        {
            var teamcraftUrl = "https://ffxivteamcraft.com/import/";
            var itemListString = "";
            for (int i = 0; i < itemList.Count(); i++)
            {
                var listItemId = itemList.Keys.ElementAt(i);
                var listItemCount = itemList.Values.ElementAt(i);
                if (i != 0)
                {
                    itemListString = itemListString + ";";
                }
                itemListString = itemListString + $"{listItemId},null,{listItemCount}";
            }
            var b64EncodedList = Base64Url.Encode(itemListString);
            var teamcraftListLink = teamcraftUrl + b64EncodedList;
            return teamcraftListLink;
        }
    }
}