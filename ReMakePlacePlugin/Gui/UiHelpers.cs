using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Lumina.Excel.Sheets;
using ReMakePlacePlugin.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.GroupPoseModule;
using static ReMakePlacePlugin.ReMakePlacePlugin;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace ReMakePlacePlugin.Gui
{
    public static class UiHelpers
    {
        /// <summary>
        /// Draw an icon from the Dalamud api.
        /// </summary>
        /// <param name="icon"> Item id, must be below 65000</param>
        /// <param name="size"></param>
        public static void DrawIcon(ushort icon, Vector2 size)
        {
            if (icon < 65000)
            {
                var iconTexture = DalamudApi.TextureProvider.GetFromGameIcon(new GameIconLookup(icon));
                ImGui.Image(iconTexture.GetWrapOrEmpty().Handle, size);
            }
        }

        /// <summary>Make a button with both an icon and text.</summary>
        public static bool IconTextButton(FontAwesomeIcon icon, string text)
        {
            var buttonClicked = false;

            var iconSize = GetIconSize(icon);
            var textSize = ImGui.CalcTextSize(text);
            var padding = ImGui.GetStyle().FramePadding;
            var spacing = ImGui.GetStyle().ItemSpacing;

            var buttonSizeX = iconSize.X + textSize.X + padding.X * 2 + spacing.X;
            var buttonSizeY = (iconSize.Y > textSize.Y ? iconSize.Y : textSize.Y) + padding.Y * 2;
            var buttonSize = new Vector2(buttonSizeX, buttonSizeY);

            if (ImGui.Button("###" + icon.ToIconString() + text, buttonSize))
            {
                buttonClicked = true;
            }

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() - buttonSize.X - padding.X);
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(icon.ToIconString());
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.Text(text);

            return buttonClicked;
        }

        /// <summary>Get the size of an icon.</summary>
        public static Vector2 GetIconSize(FontAwesomeIcon icon)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var iconSize = ImGui.CalcTextSize(icon.ToIconString());
            ImGui.PopFont();
            return iconSize;
        }
    }
}
