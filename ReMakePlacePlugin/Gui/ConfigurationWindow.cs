using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.MathHelpers;
using Lumina.Excel.Sheets;
using ReMakePlacePlugin.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using static ReMakePlacePlugin.Gui.UiHelpers;
using static ReMakePlacePlugin.ReMakePlacePlugin;

namespace ReMakePlacePlugin.Gui;

public class ConfigurationWindow : Window, IDisposable
{
    private string CustomTag = string.Empty;
    private readonly Dictionary<uint, uint> iconToFurniture = new() { };

    private readonly Vector4 PURPLE = new(0.26275f, 0.21569f, 0.56863f, 1f);
    private readonly Vector4 PURPLE_ALPHA = new(0.26275f, 0.21569f, 0.56863f, 0.5f);

    private FileDialogManager FileDialogManager { get; }
    public Configuration Config { get; init; }
    public ReMakePlacePlugin Plugin { get; init; }

    public ConfigurationWindow(ReMakePlacePlugin plugin)
        : base($"ReMakePlace v{Assembly.GetExecutingAssembly().GetName().Version}###ReMakePlacePlugin", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Plugin = plugin;
        Config = plugin.Config;
        FileDialogManager = new FileDialogManager()
        {
            AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
        };
    }

    public void Dispose()
    {
        // no-op
    }

    private void SafeMatch()
    {
        if (Memory.Instance.IsHousingMode())
            Plugin.MatchLayout();
    }

    public override void Draw()
    {
        SafeMatch();
        DrawScreen();

        Vector2 leftPanelSize = new Vector2(140 * ImGuiHelpers.GlobalScale, ImGui.GetWindowHeight() - 30 * ImGuiHelpers.GlobalScale);

        using (ImRaii.Child("LeftFloat", leftPanelSize, false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var availX = ImGui.GetContentRegionAvail().X;
            string pluginDir = Svc.PluginInterface.AssemblyLocation.DirectoryName!;
            var imagePath = Path.Combine(pluginDir, "images/icon.png");
            var image = Svc.Texture.GetFromFile(imagePath).GetWrapOrDefault();
            if (image != null)
                ImGui.Image(image.Handle, availX.ToVector2());

            using (ImRaii.Child("MenuAndSettings", ImGui.GetContentRegionAvail()))
            {
                DrawMainMenu();
                DrawGeneralSettings();
            }
        }

        ImGui.SameLine();

        ImGui.BeginChild("RightFloat", border: true);
        ImGui.Text($"Current file location:"); ImGui.SameLine();
        ImGui.Selectable((Config.SaveLocation.IsNullOrEmpty() ? "No File Selected" : Config.SaveLocation), false, ImGuiSelectableFlags.Disabled);
        ImGui.Text("Note: Missing items, incorrect dyes, and items on unselected floors are grayed out");
        DrawItemListRegion();
        ImGui.EndChild();
        this.FileDialogManager.Draw();
    }

    public override void PreDraw()
    {
        base.PreDraw();
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, PURPLE);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, PURPLE_ALPHA);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, PURPLE_ALPHA);
        ImGui.SetNextWindowSize(new Vector2(680, 550), ImGuiCond.FirstUseEver);
    }

    public override void PostDraw()
    {
        base.PostDraw();
        ImGui.PopStyleColor(3);
        ImGui.End();
    }

    #region Basic UI

    private void LogLayoutMode()
    {
        if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Island)
        {
            LogError("(Manage Furnishings -> Place Furnishing Glamours)");
        }
        else
        {
            LogError("(Housing -> Indoor/Outdoor Furnishings)");
        }
    }

    private bool CheckModeForSave()
    {
        if (Memory.Instance.IsHousingMode()) return true;

        LogError("Unable to save layouts outside of Layout mode");
        LogLayoutMode();
        return false;
    }

    private bool CheckModeForLoad(bool ApplyLayout = false)
    {
        if (!Memory.Instance.IsHousingMode())
        {
            if (ApplyLayout)
                LogError("Unable to load and apply layouts outside of Rotate Layout mode");

            return false;
        }

        if (ApplyLayout)
        {
            if (!Memory.Instance.CanEditItem())
            {
                LogError("Unable to load and apply layouts outside of Rotate Layout mode");
                return false;
            }
        }
        else
        {
            if (!Memory.Instance.CanEditItem() && !Memory.Instance.CanDyeItem())
            {
                LogError("Unable to load layouts outside of Rotate Layout mode or Furnishing Color mode");
                return false;
            }
        }

        return true;
    }

    private bool CheckModeForApplyDyes()
    {
        if (!Memory.Instance.IsHousingMode() || !Memory.Instance.CanDyeItem())
        {
            LogError("Unable to load and apply dyes outside of Furnishing Color mode");
            return false;
        }

        return true;
    }

    private void SaveLayoutToFile()
    {
        if (!CheckModeForSave()) return;

        try
        {
            Plugin.GetGameLayout();
            ReMakePlacePlugin.LayoutManager.ExportLayout();
        }
        catch (Exception e)
        {
            LogError($"Save Error: {e.Message}", e.StackTrace);
        }
    }

    private void LoadLayoutFromFile(bool ApplyLayout = false)
    {
        if (!Config.SaveLocation.IsNullOrEmpty())
        {
            try
            {
                SaveLayoutManager.ImportLayout(Config.SaveLocation);
                Log(String.Format("Imported {0} items", Plugin.InteriorItemList.Count + Plugin.ExteriorItemList.Count));

                if (CheckModeForLoad(ApplyLayout))
                {
                    Plugin.MatchLayout();
                    Config.ResetRecord();
                    if (ApplyLayout)
                        Plugin.ApplyLayout();
                }
                else
                    Config.ResetRecord();
            }
            catch (Exception e)
            {
                LogError($"Load Error: {e.Message}", e.StackTrace);
            }
        }
    }

    private void ApplyDyesFromFile()
    {
        if (!Config.SaveLocation.IsNullOrEmpty())
        {
            try
            {
                SaveLayoutManager.ImportLayout(Config.SaveLocation);
                Log(String.Format("Imported {0} items", Plugin.InteriorItemList.Count + Plugin.ExteriorItemList.Count));

                if (CheckModeForApplyDyes())
                {
                    Plugin.MatchLayout();
                    Config.ResetRecord();
                    Plugin.ApplyDyes();
                }
                else
                    Config.ResetRecord();
            }
            catch (Exception e)
            {
                LogError($"Apply Dyes Error: {e.Message}", e.StackTrace);
            }
        }
    }

    private void PlaceItemsFromFile()
    {
        if (!Config.SaveLocation.IsNullOrEmpty())
        {
            try
            {
                SaveLayoutManager.ImportLayout(Config.SaveLocation);
                Log(String.Format("Imported {0} items", Plugin.InteriorItemList.Count + Plugin.ExteriorItemList.Count));

                if (CheckModeForLoad())
                {
                    Plugin.MatchLayout();
                    Config.ResetRecord();
                    Plugin.ApplyLayout(true);
                }
                else
                    Config.ResetRecord();
            }
            catch (Exception e)
            {
                LogError($"Place Items Error: {e.Message}", e.StackTrace);
            }
        }
    }

    private void DrawItemListRegion()
    {
        ImGui.BeginChild("ItemListRegion");
        ImGui.PushStyleColor(ImGuiCol.Header, PURPLE_ALPHA);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, PURPLE);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, PURPLE);

        var furnitureSections = new List<(string label, List<HousingItem> items, List<Fixture> fixtures, bool unused)>
            {
                ("Interior",Plugin.InteriorItemList, Plugin.Layout.interiorFixture, false),
                ("Exterior",Plugin.ExteriorItemList, Plugin.Layout.exteriorFixture, false),
                ("Unused", Plugin.UnusedItemList, new List<Fixture>{ }, true)
            };

        foreach (var section in furnitureSections)
        {
            ImGui.PushID(section.label);

            if (ImGui.CollapsingHeader($"{section.label} Furniture", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (section.unused) { DrawItemList(section.items, true); }
                else { DrawItemList(section.items); }
            }

            if (!section.unused)
            {
                if (ImGui.CollapsingHeader($"{section.label} Fixture", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    DrawFixtureList(section.fixtures);
                }
            }
            ImGui.PopID();
        }
        ImGui.PopStyleColor(3);
        ImGui.EndChild();
    }

    unsafe private void DrawGeneralSettings()
    {
        if (ImGui.Checkbox("Select Previous Dye", ref Config.SelectPreviousDye)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("Automatically selects the previously used dye when opening the furniture Dyeing menu\nStill requires you to confirm");

        if (ImGui.Checkbox("Auto. Confirm Dye", ref Config.AutoConfirmDye)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("Will automatically press 'Yes' when dyeing");

        if (ImGui.Checkbox("Use Rare Dyes", ref Config.UseRareStains)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("Will allow usage of rare dyes such as Pure White, Jet Black, etc.");

        //ImGui.BeginChild("SettingsPanel", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeightWithSpacing() * 7));
        if (ImGui.Checkbox("Label Furniture", ref Config.DrawScreen)) Config.Save();
        if (Config.ShowTooltips && ImGui.IsItemHovered())
            ImGui.SetTooltip("Show furniture names on the screen");

        //ImGui.SameLine();ImGui.Dummy(new Vector2(10, 0));ImGui.SameLine();

        if (ImGui.Checkbox("Show Tooltips", ref Config.ShowTooltips)) Config.Save();

        bool hasFloors = false;
        try
        {
            hasFloors = Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors && !Memory.Instance.GetIndoorHouseSize().Equals("Apartment");
        }
        catch (NullReferenceException)
        {
            // Thanks zbee
        }

        DrawMainMenuButton($"Teamcraft Export", () =>
        {
            var allItemsList = new Dictionary<string, int>();
            for (int i = 0; i < Plugin.InteriorItemList.Count(); i++)
            {
                var itemId = Plugin.InteriorItemList[i].ItemKey.ToString();
                if (allItemsList.ContainsKey(itemId))
                {
                    allItemsList[itemId]++;
                }
                else
                {
                    allItemsList.Add(itemId, 1);
                }
            }
            for (int i = 0; i < Plugin.ExteriorItemList.Count(); i++)
            {
                var itemId = Plugin.ExteriorItemList[i].ItemKey.ToString();
                if (allItemsList.ContainsKey(itemId))
                {
                    allItemsList[itemId]++;
                }
                else
                {
                    allItemsList.Add(itemId, 1);
                }
            }
            Utils.TeamcraftExport(allItemsList);
        },
        Config.SaveLocation.IsNullOrEmpty(),
        "Generates a list import link for TeamCraft",
        ImGui.GetContentRegionAvail().X);
        if (Config.SaveLocation.IsNullOrEmpty())
        {
            if (Config.ShowTooltips && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                ImGui.SetTooltip("No active file to export");
            }
        }

        ImGui.Text("Placement Interval");

        ImGui.Dummy(new Vector2(5, 0)); ImGui.SameLine();
        ImGui.PushItemWidth(60);
        if (ImGui.InputInt("ms", ref Config.LoadInterval))
        {
            Config.Save();
        }
        ImGui.PopItemWidth();
        if (Config.ShowTooltips && ImGui.IsItemHovered()) ImGui.SetTooltip("Time interval between furniture placements when applying a layout. If this is too low (e.g. 200 ms), some placements may be skipped over.");

        ImGui.Dummy(new Vector2(10, 0));

        if (hasFloors)
        {
            ImGui.Text("Enabled Floors");
            float height = ImGui.GetFrameHeightWithSpacing() * 3 + ImGui.GetStyle().WindowPadding.Y;
            float width = 120;
            ImGui.BeginChild("FloorSelection", new Vector2(width, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            //if (ImGui.CollapsingHeader("Enabled Floors")){
            if (Memory.Instance.HasUpperFloor() && ImGui.Checkbox("Upper Floor", ref Config.UpperFloor)) Config.Save();
            if (ImGui.Checkbox("Ground Floor", ref Config.GroundFloor)) Config.Save();
            if (ImGui.Checkbox("Basement", ref Config.Basement)) Config.Save();
            //}
            ImGui.EndChild();
        }
        //ImGui.EndChild();
    }

    unsafe private void DrawMainMenu()
    {
        Vector2 menuDimensions = ImGui.GetContentRegionAvail();

        DrawMainMenuButton($"Open File", () =>
        {
            string saveName = Config.SaveLocation.IsNullOrEmpty()
                ? "save"
                : Path.GetFileNameWithoutExtension(Config.SaveLocation);

            FileDialogManager.OpenFileDialog("Select a Layout File", ".json", (ok, res) =>
            {
                if (!ok) return;
                Config.SaveLocation = res.FirstOrDefault("");
                Config.Save();
                LoadLayoutFromFile();
            }, 1, Path.GetDirectoryName(Config.SaveLocation));
        },
        false,
        "Select a file to open",
        menuDimensions.X);

        DrawMainMenuButton("Apply", () =>
        {
            Config.Save();
            LoadLayoutFromFile(true);
        },
        Config.SaveLocation.IsNullOrEmpty(),
        "Attempt to apply layout from current file location",
        menuDimensions.X);

        var ctrlKeyPressed = ImGui.GetIO().KeyCtrl;
        var dyeingItems = ReMakePlacePlugin.CurrentlyDyeingItems;

        DrawMainMenuButton(dyeingItems ? "Stop Dyeing" : "Apply Dyes", () =>
        {
            if (dyeingItems)
            {
                Plugin.StopDyeingItems();
            }
            else
            {
                Config.Save();
                ApplyDyesFromFile();
            }
        },
        dyeingItems ? false : (Config.SaveLocation.IsNullOrEmpty() || !ctrlKeyPressed),
        dyeingItems ? "Will stop applying Dyes to furnitures" :
            (ctrlKeyPressed ? "Attempt to apply dyes, Furnishing Color window needs to be open" : "Hold CTRL to apply dyes"),
        menuDimensions.X);

        //DrawMainMenuButton("Place Items Down", () =>
        //{
        //    Config.Save();
        //    Log($"Number of Items interior {Plugin.InteriorItemList.Count}");
        //    //ReMakePlacePlugin.PlaceItemInternal();
        //    //PlaceItemsFromFile();
        //},
        //Config.SaveLocation.IsNullOrEmpty(),
        //"Tries to place items from Inventory & Storage",
        //menuDimensions.X);

        DrawMainMenuButton("Save As", () =>
        {
            if (CheckModeForSave())
            {
                string saveName = Config.SaveLocation.IsNullOrEmpty()
                    ? "save"
                    : Path.GetFileNameWithoutExtension(Config.SaveLocation);

                FileDialogManager.SaveFileDialog("Select a Save Location", ".json", saveName, "json", (ok, res) =>
                {
                    if (!ok) return;
                    Config.SaveLocation = res;
                    Config.Save();
                    SaveLayoutToFile();
                }, Path.GetDirectoryName(Config.SaveLocation));
            }
        },
        false,
        "Save layout to a new file location",
        menuDimensions.X);

        DrawMainMenuButton("Save",
            SaveLayoutToFile,
            Config.SaveLocation.IsNullOrEmpty(),
            "Save layout to current file location",
            menuDimensions.X);
    }

    private void DrawMainMenuButton(string label, System.Action onClick, bool disabled = false, string? tooltip = null, float width = 100)
    {
        float height = ImGui.GetFrameHeight();

        ImGui.BeginDisabled(disabled);
        if (ImGui.Button(label, new Vector2(width, height)) && !disabled)
            onClick();
        ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tooltip);
    }


    private void DrawRow(int i, HousingItem housingItem, bool showSetPosition = true, int childIndex = -1)
    {
        ImGui.TableNextColumn();
        if (showSetPosition)
        {
            string uniqueID = childIndex == -1 ? i.ToString() : i.ToString() + "_" + childIndex.ToString();
            bool noMatch = housingItem.ItemStruct == IntPtr.Zero;

            if (!noMatch)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0.0f, 0.0f));
                ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
                if (ImGui.Button("Set" + "##" + uniqueID))
                {
                    Plugin.MatchLayout();

                    if (housingItem.ItemStruct != IntPtr.Zero)
                    {
                        SetItemPosition(housingItem);
                    }
                    else
                    {
                        LogError($"Unable to set position for {housingItem.Name}");
                    }
                }
                ImGui.PopStyleVar(2);
            }
            ImGui.TableNextColumn();
        }

        var displayName = housingItem.Name;

        if (Svc.Data.GetExcelSheet<Item>().TryGetRow(housingItem.ItemKey, out var item))
        {
            DrawIcon(item.Icon, new Vector2(20, 20));
            ImGui.SameLine();
        }

        if (housingItem.ItemStruct == IntPtr.Zero)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        }
        ImGui.Text(displayName);
        if (ImGui.IsItemClicked()) ImGui.SetClipboardText(displayName);
        ImGui.TableNextColumn();

        if (!housingItem.CorrectLocation) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        string posText = $"{housingItem.X:0.0###}, {housingItem.Y:0.0###}, {housingItem.Z:0.0###}";
        float posX = (ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(posText).X - ImGui.GetScrollX());
        if (posX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(posX);
        ImGui.Text(posText);
        if (!housingItem.CorrectLocation) ImGui.PopStyleColor();
        ImGui.TableNextColumn();

        if (!housingItem.CorrectRotation) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
        string rotateText = $"{Utils.radToDeg(housingItem.Rotate):0.00##}";
        posX = (ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - ImGui.CalcTextSize(rotateText).X - ImGui.GetScrollX());
        if (posX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(posX);
        ImGui.Text(rotateText);
        if (!housingItem.CorrectRotation) ImGui.PopStyleColor();
        ImGui.TableNextColumn();

        var stain = Svc.Data.GetExcelSheet<Stain>().GetRowOrDefault(housingItem.Stain);
        var colorName = stain?.Name;

        if (housingItem.Stain != 0)
        {
            Utils.StainButton("dye_" + i, stain.Value, new Vector2(20));
            ImGui.SameLine();

            if (!housingItem.DyeMatch) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
            ImGui.Text($"{colorName}");
            if (!housingItem.DyeMatch) ImGui.PopStyleColor();
        }
        else if (housingItem.MaterialItemKey != 0)
        {
            if (Svc.Data.GetExcelSheet<Item>().TryGetRow(housingItem.MaterialItemKey, out var mitem))
            {
                if (!housingItem.DyeMatch) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1));
                DrawIcon(mitem.Icon, new Vector2(20, 20));
                ImGui.SameLine();
                ImGui.Text(mitem.Name.ToString());
                if (!housingItem.DyeMatch) ImGui.PopStyleColor();
            }
        }
    }

    private void DrawFixtureList(List<Fixture> fixtureList)
    {
        try
        {
            if (ImGui.Button("Clear"))
            {
                fixtureList.Clear();
                Config.Save();
            }

            if (ImGui.BeginTable("FixtureList", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.None, 2);
                ImGui.TableSetupColumn("Fixture", ImGuiTableColumnFlags.None, 1);
                ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.None, 5);
                ImGui.TableHeadersRow();

                foreach (var fixture in fixtureList)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(fixture.level);

                    ImGui.TableNextColumn();
                    ImGui.Text(fixture.type);

                    ImGui.TableNextColumn();
                    if (Svc.Data.GetExcelSheet<Item>().TryGetRow(fixture.itemId, out var item))
                    {
                        DrawIcon(item.Icon, new Vector2(20, 20));
                        ImGui.SameLine();
                    }
                    ImGui.Text(fixture.name);
                }

                ImGui.EndTable();
            }
        }
        catch (Exception e)
        {
            LogError(e.Message, e.StackTrace);
        }

    }

    private void DrawItemList(List<HousingItem> itemList, bool isUnused = false)
    {
        if (ImGui.Button("Sort"))
        {
            itemList.Sort((x, y) =>
            {
                if (x.Name.CompareTo(y.Name) != 0)
                    return x.Name.CompareTo(y.Name);
                if (x.X.CompareTo(y.X) != 0)
                    return x.X.CompareTo(y.X);
                if (x.Y.CompareTo(y.Y) != 0)
                    return x.Y.CompareTo(y.Y);
                if (x.Z.CompareTo(y.Z) != 0)
                    return x.Z.CompareTo(y.Z);
                if (x.Rotate.CompareTo(y.Rotate) != 0)
                    return x.Rotate.CompareTo(y.Rotate);
                return 0;
            });
            Config.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            itemList.Clear();
            Config.Save();
        }
        ImGui.SameLine();
        if (IconTextButton(FontAwesomeIcon.SyncAlt, "Refresh"))
        {
            LoadLayoutFromFile();
        }

        // name, position, r, color, set
        int columns = isUnused ? 4 : 5;


        if (ImGui.BeginTable("ItemList", columns, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Reorderable))
        {
            if (!isUnused)
            {
                ImGui.TableSetupColumn("Set", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, 25f * ImGuiHelpers.GlobalScale); // Making this fixed with can render it truncated and unreadable on higher scalings
            }

            // Stretch columns with relative weights
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch, 1.8f);           // Wider
            ImGui.TableSetupColumn("Position (X,Y,Z)", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Rotation", ImGuiTableColumnFlags.WidthStretch, 0.5f);
            ImGui.TableSetupColumn("Dye/Material", ImGuiTableColumnFlags.WidthStretch, 1.0f);

            ImGui.TableHeadersRow();

            for (int i = 0; i < itemList.Count(); i++)
            {
                var housingItem = itemList[i];

                ImGui.TableNextRow();
                DrawRow(i, housingItem, !isUnused);
                if (housingItem.ItemStruct == IntPtr.Zero)
                {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndTable();
        }
    }

    #endregion

    #region Draw Screen

    protected void DrawScreen()
    {
        if (Config.DrawScreen)
            DrawItemOnScreen();
    }

    private unsafe void DrawItemOnScreen()
    {

        if (Memory.Instance == null) return;

        var itemList = Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors ? Plugin.InteriorItemList : Plugin.ExteriorItemList;

        for (int i = 0; i < itemList.Count(); i++)
        {
            var player = Svc.Objects.LocalPlayer;
            if (player == null) return;
            var playerPos = player.Position;
            var housingItem = itemList[i];

            if (housingItem.ItemStruct == IntPtr.Zero) continue;

            var itemStruct = (HousingItemStruct*)housingItem.ItemStruct;

            var itemPos = new Vector3(itemStruct->Position.X, itemStruct->Position.Y, itemStruct->Position.Z);
            if (Config.HiddenScreenItemHistory.IndexOf(i) >= 0) continue;
            if (Config.DrawDistance > 0 && (playerPos - itemPos).Length() > Config.DrawDistance)
                continue;
            var displayName = housingItem.Name;
            if (Svc.GameGui.WorldToScreen(itemPos, out var screenCoords))
            {
                ImGui.PushID("HousingItemWindow" + i);
                ImGui.SetNextWindowPos(new Vector2(screenCoords.X, screenCoords.Y));
                ImGui.SetNextWindowBgAlpha(0.8f);
                if (ImGui.Begin("HousingItem" + i,
                    ImGuiWindowFlags.NoDecoration |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav))
                {

                    ImGui.Text(displayName);

                    ImGui.SameLine();

                    if (ImGui.Button("Set" + "##ScreenItem" + i.ToString()))
                    {
                        if (!Memory.Instance.CanEditItem())
                        {
                            LogError("Unable to set position while not in rotate layout mode");
                            continue;
                        }

                        SetItemPosition(housingItem);
                        Config.HiddenScreenItemHistory.Add(i);
                        Config.Save();
                    }

                    ImGui.SameLine();
                    ImGui.End();
                }

                ImGui.PopID();
            }
        }
    }

    #endregion
}