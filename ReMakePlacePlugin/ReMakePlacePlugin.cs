using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using ReMakePlacePlugin.Objects;
using ReMakePlacePlugin.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using static ReMakePlacePlugin.Memory;
using HousingFurniture = Lumina.Excel.Sheets.HousingFurniture;
using TaskManager = ECommons.Automation.NeoTaskManager.TaskManager;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ReMakePlacePlugin
{

    public class ReMakePlacePlugin : IDalamudPlugin
    {
        public string Name => $"ReMakePlace Plugin v7.3.2";

        private string[] commandNames = ["remakeplace", "rmp", "makeplace"];
        public PluginUi Gui { get; private set; }
        public Configuration Config { get; private set; }

        public static List<HousingItem> ItemsToPlace = new List<HousingItem>();

        public static List<HousingItem> ItemsToDye = new List<HousingItem>();

        private delegate bool UpdateLayoutDelegate(IntPtr a1);
        private HookWrapper<UpdateLayoutDelegate> IsSaveLayoutHook;

        // Function for selecting an item, usually used when clicking on one in game.        
        public delegate void SelectItemDelegate(IntPtr housingStruct, IntPtr item);
        private static HookWrapper<SelectItemDelegate> SelectItemHook;

        public delegate long GetSelectedHousingItemAddressDelegate(long housingManager);
        private static HookWrapper<GetSelectedHousingItemAddressDelegate> GetSelectedHousingItemAddressHook;

        public delegate void InteractWithHousingItemDelegate(long agentHousingPtr, long unk);
        private static HookWrapper<InteractWithHousingItemDelegate> InteractWithHousingItemHook;

        public static bool CurrentlyPlacingItems = false;

        public static bool CurrentlyDyeingItems = false;

        public static bool OriginalPlaceAnywhere = false;

        public static bool ApplyChange = false;

        public static SaveLayoutManager LayoutManager;

        public static bool logHousingDetour = false;

        internal static Location PlotLocation = new Location();

        public Layout Layout = new Layout();
        public List<HousingItem> InteriorItemList = new List<HousingItem>();
        public List<HousingItem> ExteriorItemList = new List<HousingItem>();
        public List<HousingItem> UnusedItemList = new List<HousingItem>();

        private HookWrapper<AtkUnitBase.Delegates.FireCallback> AddonFireCallbackHook;
        private Stain? PreviouslySelectedStain = null;
        private bool IsSelectingDye = false;

        private TaskManager TaskManager;

        public void Dispose()
        {
            HookManager.Dispose();

            DalamudApi.ClientState.TerritoryChanged -= TerritoryChanged;
            foreach (string commandName in commandNames)
            {
                DalamudApi.CommandManager.RemoveHandler($"/{commandName}");
            }
            Gui?.Dispose();

            DalamudApi.AddonLifecycle.UnregisterListener(OnPostSetupDyeConfirm);

            ECommonsMain.Dispose();
        }

        public ReMakePlacePlugin(IDalamudPluginInterface pi)
        {
            ECommonsMain.Init(pi, this);

            var config = new TaskManagerConfiguration()
            {
                OnTaskException = (task, ex, ref @continue, ref abort) =>
                {
                    LogError($"Error during dyeing task '{task.Name}'.");
                    CurrentlyDyeingItems = false;
                },
                OnTaskTimeout = (task, ref remainingTimeMs) =>
                {
                    LogError($"Timeout during dyeing task '{task.Name}'.");
                    CurrentlyDyeingItems = false;
                },
                TimeLimitMS = 5000,
            };

            TaskManager = new TaskManager(config);

            DalamudApi.Initialize(pi);

            Config = DalamudApi.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Config.Save();

            Initialize();

            foreach (string commandName in commandNames)
            {
                DalamudApi.CommandManager.AddHandler($"/{commandName}", new CommandInfo(CommandHandler)
                {
                    HelpMessage = "load config window."
                });
            }
            Gui = new PluginUi(this);
            DalamudApi.ClientState.TerritoryChanged += TerritoryChanged;

            HousingData.Init(this);
            Memory.Init();
            LayoutManager = new SaveLayoutManager(this, Config);

            DalamudApi.PluginLog.Info("ReMakePlace Plugin v7.3.2 initialized");
        }
        public unsafe void Initialize()
        {

            IsSaveLayoutHook = HookManager.Hook<UpdateLayoutDelegate>("40 53 48 83 ec 20 48 8b d9 48 8b 0d ?? ?? ?? ?? e8 ?? ?? ?? ?? 33 d2 48 8b c8 e8 ?? ?? ?? ?? 84 c0 75 ?? 38 83 ?? 01 00 00", IsSaveLayoutDetour);

            SelectItemHook = HookManager.Hook<SelectItemDelegate>("48 85 D2 0F 84 49 09 00 00 53 41 56 48 83 EC 48 48 89 6C 24 60 48 8B DA 48 89 74 24 70 4C 8B F1", SelectItemDetour);

            PlaceItemHook = HookManager.Hook<PlaceItemDelegate>("48 89 5C 24 10 48 89 74  24 18 57 48 83 EC 20 4c 8B 41 18 33 FF 0F B6 F2", PlaceItemDetour);

            UpdateYardObjHook = HookManager.Hook<UpdateYardDelegate>("48 89 74 24 18 57 48 83 ec 20 b8 dc 02 00 00 0f b7 f2 ??", UpdateYardObj);

            GetGameObjectHook = HookManager.Hook<GetObjectDelegate>("48 89 5c 24 08 48 89 74 24 10 57 48 83 ec 20 0f b7 f2 33 db 0f 1f 40 00 0f 1f 84 00 00 00 00 00", GetGameObject);

            GetObjectFromIndexHook = HookManager.Hook<GetActiveObjectDelegate>("81 fa 90 01 00 00 75 08 48 8b 81 88 0c 00 00 c3 0f b7 81 90 0c 00 00 3b d0 72 03 33 c0 c3", GetObjectFromIndex);

            GetYardIndexHook = HookManager.Hook<GetIndexDelegate>("48 89 5c 24 10 57 48 83 ec 20 0f b6 d9", GetYardIndex);

            // Dyeing management (Auto Confirm Dyeing Prompt (MiragePrismMiragePlateConfirm) & Select previous dye (ColorantColoring))
            DalamudApi.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, new[] { "MiragePrismMiragePlateConfirm", "ColorantColoring" }, OnPostSetupDyeConfirm);

            AddonFireCallbackHook = HookManager.HookAddress<AtkUnitBase.Delegates.FireCallback>(AtkUnitBase.Addresses.FireCallback.Value, FireCallbackDetour);

            InteractWithHousingItemHook = HookManager.Hook<InteractWithHousingItemDelegate>("48 85 D2 0F 84 ?? ?? ?? ?? 57 41 56 48 83 EC ?? 0F B6 81", InteractWithHousingItemDetour);
            GetSelectedHousingItemAddressHook = HookManager.Hook<GetSelectedHousingItemAddressDelegate>("E8 ?? ?? ?? ?? 48 85 C0 75 ?? E8 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 84 C0 75 ?? E8 ?? ?? ?? ?? 48 8B C8 E8 ?? ?? ?? ?? 8B 8B", GetSelectedHousingItemAddressDetour);
        }

        private unsafe long GetSelectedHousingItemAddressDetour(long housingManager)
        {
            return GetSelectedHousingItemAddressHook.Original(housingManager);
        }

        private unsafe void InteractWithHousingItemDetour(long agentHousingPtr, long unk)
        {
            InteractWithHousingItemHook.Original(agentHousingPtr, unk);
        }

        private unsafe void InteractWithSelectedItem()
        {
            var agentHousing = Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Housing);
            var housingManager = HousingManager.Instance();

            var stuff = GetSelectedHousingItemAddressHook.Original((long)housingManager);
            if (stuff == 0)
            {
                LogError("No item selected to interact with.");
                return;
            }

            InteractWithHousingItemHook.Original((long)agentHousing, stuff);
        }

        /// <summary>
        /// Hook the FireCallback method to capture dye selection from the ColorantColoring addon.<br/>
        /// Used to know what dye the user selected last, so we can re-select it when re-opening the dye window later.
        /// </summary>
        private unsafe bool FireCallbackDetour(AtkUnitBase* addonPtr, uint valueCount, AtkValue* values, bool close)
        {
            var ret = AddonFireCallbackHook.Original(addonPtr, valueCount, values, close);

            var addonName = addonPtr->NameString;
            if (addonName != "ColorantColoring")
                return ret;

            if (IsSelectingDye)
                return ret;

            // From SimpleTweaks from Caraxi: https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Debugging/AddonDebug.cs#L358-L412
            var atkValueList = new List<object>();
            try
            {
                var a = values;
                for (var i = 0; i < valueCount; i++)
                {
                    switch (a->Type)
                    {
                        case ValueType.Int:
                            {
                                atkValueList.Add(a->Int);
                                break;
                            }
                        case ValueType.String:
                            {
                                atkValueList.Add(Marshal.PtrToStringUTF8(new IntPtr(a->String)));
                                break;
                            }
                        case ValueType.UInt:
                            {
                                atkValueList.Add(a->UInt);
                                break;
                            }
                        case ValueType.Bool:
                            {
                                atkValueList.Add(a->Byte != 0);
                                break;
                            }
                        default:
                            {
                                atkValueList.Add($"Unknown Type: {a->Type}");
                                break;
                            }
                    }
                    a++;
                }
            }
            catch
            {
                return ret;
            }

            if (atkValueList.Count <= 0 || atkValueList[0] is not int callbackFirstValue || callbackFirstValue != 5)
                return ret;

            if (atkValueList.Count < 2 || atkValueList[2] is not int)
                return ret;

            var stainId = (int)atkValueList[2];

            var stainSheet = DalamudApi.DataManager.GetExcelSheet<Stain>();
            if (stainSheet == null)
                return ret;

            if (stainSheet.TryGetRow((uint)stainId, out var stain))
                PreviouslySelectedStain = stain;

            return ret;
        }

        public void OnPostSetupDyeConfirm(AddonEvent type, AddonArgs args)
        {
            if (!Memory.Instance.IsHousingMode())
                return;

            switch (args.AddonName)
            {
                case "MiragePrismMiragePlateConfirm":
                    {
                        if (!Config.AutoConfirmDye)
                            return;

                        DalamudApi.Framework.RunOnFrameworkThread(AutoConfirmDyePrompt);
                        return;
                    }
                case "ColorantColoring":
                    {
                        if (!Config.SelectPreviousDye)
                            return;

                        DalamudApi.Framework.RunOnFrameworkThread(SelectPreviousShade);
                        return;
                    }
                default:
                    return;
            }
        }

        public unsafe bool AutoConfirmDyePrompt()
        {
            if (IsAddonReady("MiragePrismMiragePlateConfirm", out var dyeConfirmAddon))
            {
                Callback.Fire(dyeConfirmAddon, true, 0);
                return true;
            }
            return false;
        }

        public unsafe void SelectPreviousShade()
        {
            if (CurrentlyDyeingItems || PreviouslySelectedStain == null)
                return;

            IsSelectingDye = true;

            if (IsAddonReady("ColorantColoring", out var colorantColoringAddon))
            {
                var callback = StainCallbackHelper.GetCallbackValuesForStain(PreviouslySelectedStain.Value);
                if (callback == null)
                {
                    IsSelectingDye = false;
                    return;
                }

                Callback.Fire(colorantColoringAddon, true, callback.Value.Shade.GetCallbackValues());

                DalamudApi.Framework.RunOnTick(SelectPreviousDye, TimeSpan.FromMilliseconds(100));
            }
            else
            {
                IsSelectingDye = false;
            }
        }

        public unsafe void SelectPreviousDye()
        {
            try
            {
                if (CurrentlyDyeingItems || PreviouslySelectedStain == null)
                    return;

                if (IsAddonReady("ColorantColoring", out var colorantColoringAddon))
                {
                    var callback = StainCallbackHelper.GetCallbackValuesForStain(PreviouslySelectedStain.Value);
                    if (callback == null)
                        return;

                    Callback.Fire(colorantColoringAddon, true, callback.Value.Stain.GetCallbackValues());
                }
            }
            finally
            {
                IsSelectingDye = false;
            }
        }

        public delegate void PlaceItemDelegate(IntPtr housingStruct, IntPtr item);
        private static HookWrapper<PlaceItemDelegate> PlaceItemHook;
        unsafe static public void PlaceItemDetour(IntPtr housing, IntPtr item)
        {
            /*
            The call made by the XIV client has some strange behaviour.
            It can either place the item pointer passed to it or it retrieves the activeItem from the housing object.
            I had previously speculated that this lead to crashes when I implemented this and Jaws coppied it but better memory management seems to ave resolved the issue.
            Updated to use the actual item since we handle them more safely elsewhere.
            */
            DalamudApi.PluginLog.Debug($"item detour housing {housing + 24}");
            DalamudApi.PluginLog.Debug($"item detour item {item}");
            PlaceItemHook.Original(housing, item);
        }
        unsafe static public void PlaceItem(IntPtr item)
        {
            PlaceItemDetour((IntPtr)Memory.Instance.HousingStructure, item);
        }


        internal delegate ushort GetIndexDelegate(byte type, byte objStruct);
        internal static HookWrapper<GetIndexDelegate> GetYardIndexHook;
        internal static ushort GetYardIndex(byte plotNumber, byte inventoryIndex)
        {
            var result = GetYardIndexHook.Original(plotNumber, inventoryIndex);
            return result;
        }

        internal delegate IntPtr GetActiveObjectDelegate(IntPtr ObjList, uint index);

        internal static IntPtr GetObjectFromIndex(IntPtr ObjList, uint index)
        {
            var result = GetObjectFromIndexHook.Original(ObjList, index);
            return result;
        }

        internal delegate IntPtr GetObjectDelegate(IntPtr ObjList, ushort index);
        internal static HookWrapper<GetObjectDelegate> GetGameObjectHook;
        internal static HookWrapper<GetActiveObjectDelegate> GetObjectFromIndexHook;

        internal static IntPtr GetGameObject(IntPtr ObjList, ushort index)
        {
            return GetGameObjectHook.Original(ObjList, index);
        }

        public delegate void UpdateYardDelegate(IntPtr housingStruct, ushort index);
        private static HookWrapper<UpdateYardDelegate> UpdateYardObjHook;


        private void UpdateYardObj(IntPtr objectList, ushort index)
        {
            UpdateYardObjHook.Original(objectList, index);
        }

        unsafe static public void SelectItemDetour(IntPtr housing, IntPtr item)
        {
            SelectItemHook.Original(housing, item);
        }

        unsafe static public void SelectItem(IntPtr item)
        {
            SelectItemDetour((IntPtr)Memory.Instance.HousingStructure, item);
        }

        public unsafe void RecursivelyPlaceItems()
        {

            try
            {
                while (ItemsToPlace.Count > 0)
                {
                    var item = ItemsToPlace.First();
                    ItemsToPlace.RemoveAt(0);

                    if (item.ItemStruct == IntPtr.Zero) continue;

                    if (item.CorrectLocation && item.CorrectRotation)
                    {
                        Log($"{item.Name} is already correctly placed");
                        continue;
                    }

                    DalamudApi.Framework.RunOnTick(RecursivelyPlaceItems, TimeSpan.FromMilliseconds(Config.LoadInterval));

                    SetItemPosition(item);
                    return;
                }

            }
            catch (Exception e)
            {
                LogError($"Error: {e.Message}", e.StackTrace);
            }

            Cleanup();

            void Cleanup()
            {
                Memory.Instance.SetPlaceAnywhere(OriginalPlaceAnywhere);
                CurrentlyPlacingItems = false;
                Log("Finished applying layout");
            }
        }

        unsafe public static void SetItemPosition(HousingItem rowItem)
        {

            if (!Memory.Instance.CanEditItem())
            {
                LogError("Unable to set position outside of Rotate Layout mode");
                return;
            }

            if (rowItem.ItemStruct == IntPtr.Zero) return;

            Log("Placing " + rowItem.Name);

            var MemInstance = Memory.Instance;

            logHousingDetour = true;
            ApplyChange = true;

            SelectItem(rowItem.ItemStruct);


            Vector3 position = new Vector3(rowItem.X, rowItem.Y, rowItem.Z);
            Vector3 rotation = new Vector3();

            rotation.Y = (float)(rowItem.Rotate * 180 / Math.PI);

            if (MemInstance.GetCurrentTerritory() == Memory.HousingArea.Outdoors)
            {
                var rotateVector = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -PlotLocation.rotation);
                position = Vector3.Transform(position, rotateVector) + PlotLocation.ToVector();
                rotation.Y = (float)((rowItem.Rotate - PlotLocation.rotation) * 180 / Math.PI);
            }
            MemInstance.WritePosition(position);
            MemInstance.WriteRotation(rotation);

            PlaceItem(rowItem.ItemStruct);

            DalamudApi.PluginLog.Debug($"{rowItem.GetLocation()}");
            rowItem.CorrectLocation = true;
            rowItem.CorrectRotation = true;

        }

        public void ApplyLayout()
        {
            if (CurrentlyPlacingItems)
            {
                Log($"Already placing items");
                return;
            }

            CurrentlyPlacingItems = true;
            Log($"Applying layout with interval of {Config.LoadInterval}ms");

            ItemsToPlace.Clear();

            List<HousingItem> placedLast = new List<HousingItem>();

            List<HousingItem> toBePlaced;

            if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors)
            {
                toBePlaced = new List<HousingItem>();
                foreach (var houseItem in InteriorItemList)
                {
                    if (IsSelectedFloor(houseItem.Y))
                    {
                        toBePlaced.Add(houseItem);
                    }
                }
            }
            else
            {
                toBePlaced = new List<HousingItem>(ExteriorItemList);
            }

            foreach (var item in toBePlaced)
            {
                if (item.IsTableOrWallMounted)
                {
                    placedLast.Add(item);
                }
                else
                {
                    ItemsToPlace.Add(item);
                }
            }

            ItemsToPlace.AddRange(placedLast);


            if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Outdoors)
            {
                GetPlotLocation();
            }

            OriginalPlaceAnywhere = Memory.Instance.GetPlaceAnywhere();
            Memory.Instance.SetPlaceAnywhere(true);

            RecursivelyPlaceItems();
        }

        public void ApplyDyes()
        {
            if (CurrentlyDyeingItems)
            {
                Log($"Already dyeing items");
                return;
            }

            CurrentlyDyeingItems = true;
            Log($"Applying dyes with interval of {Config.LoadInterval}ms");

            ItemsToDye.Clear();

            List<HousingItem> toBeDyed;

            if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors)
            {
                toBeDyed = new List<HousingItem>();
                foreach (var houseItem in InteriorItemList)
                {
                    if (IsSelectedFloor(houseItem.Y) && !houseItem.DyeMatch)
                    {
                        toBeDyed.Add(houseItem);
                    }
                }
            }
            else
            {
                toBeDyed = new List<HousingItem>();
                foreach (var houseItem in ExteriorItemList)
                {
                    if (!houseItem.DyeMatch)
                    {
                        toBeDyed.Add(houseItem);
                    }
                }
            }

            ItemsToDye.AddRange(toBeDyed);

            if (ItemsToDye.Count == 0)
            {
                Log("No items need dyeing");
                CurrentlyDyeingItems = false;
                return;
            }

            Log($"Found {ItemsToDye.Count} items to dye");

            DyeAllItems();
        }

        public unsafe void DyeAllItems()
        {
            try
            {
                if (ItemsToDye.Count == 0)
                {
                    CurrentlyDyeingItems = false;
                    Log("Finished applying dyes");
                    if (IsAddonReady("ColorantColoring", out var addon))
                        Callback.Fire(addon, true, 2);
                    return;
                }

                var item = ItemsToDye.First();
                ItemsToDye.RemoveAt(0);

                if (item.ItemStruct == IntPtr.Zero)
                {
                    DyeAllItems();
                    return;
                }

                if (item.DyeMatch)
                {
                    Log($"{item.Name} is already correctly dyed");
                    DyeAllItems();
                    return;
                }

                SetItemDye(item);
            }
            catch (Exception e)
            {
                LogError($"Error: {e.Message}", e.StackTrace);
                CurrentlyDyeingItems = false;
                Log("Finished applying dyes with errors");
            }
        }

        unsafe public void SetItemDye(HousingItem rowItem)
        {
            if (!Memory.Instance.CanDyeItem())
            {
                LogError("Unable to dye item outside of Furnishing Color mode");
                CurrentlyDyeingItems = false;
                return;
            }

            if (rowItem.ItemStruct == IntPtr.Zero)
            {
                DyeAllItems();
                return;
            }

            Log($"Dyeing {rowItem.Name}");

            Stain stain;
            if (!DalamudApi.DataManager.GetExcelSheet<Stain>().TryGetRow(rowItem.Stain, out stain))
            {
                LogError($"Invalid stain ID {rowItem.Stain} for item {rowItem.Name}");
                DyeAllItems();
                return;
            }

            // Check if dye addon is open, if yes close it, if not continue
            TaskManager.Enqueue(() =>
            {
                if (IsAddonReady("ColorantColoring", out var addon))
                    Callback.Fire(addon, true, 2);

                return true;
            }, "Close Dye addon if open");

            TaskManager.Enqueue(() => SelectItem(rowItem.ItemStruct), "SelectItem");
            TaskManager.EnqueueDelay(100);
            TaskManager.Enqueue(InteractWithSelectedItem, "Interact with previously selected Item");
            TaskManager.Enqueue(() => IsAddonReady("ColorantColoring", out var a), "Wait for Dye addon");
            TaskManager.Enqueue(() =>
            {
                if (IsAddonReady("ColorantColoring", out var addon))
                {
                    var callback = StainCallbackHelper.GetCallbackValuesForStain(stain);
                    if (callback == null)
                        return false;

                    Callback.Fire(addon, true, callback.Value.Shade.GetCallbackValues());
                    return true;
                }
                return false;
            }, "Select Shade");
            TaskManager.EnqueueDelay(100);
            TaskManager.Enqueue(() =>
            {
                if (IsAddonReady("ColorantColoring", out var addon))
                {
                    var callback = StainCallbackHelper.GetCallbackValuesForStain(stain);
                    if (callback == null)
                        return false;

                    Callback.Fire(addon, true, callback.Value.Stain.GetCallbackValues());
                    return true;
                }
                return false;
            }, "Select Dye");

            // Check if "Dye" button is greyed, if yes skip item
            TaskManager.Enqueue(() =>
            {
                if (IsAddonReady("ColorantColoring", out var addon))
                {
                    try
                    {
                        var nineGridNode = GenericHelpers.GetNodeByIDChain(addon->RootNode, 1, 64, 68, 3)->GetAsAtkNineGridNode();
                        if (nineGridNode->Color.RGBA == 0xFFFFFFB2)
                        {
                            // Dye button is disabled, skip item
                            Log($"Dye button is disabled for {rowItem.Name}, skipping.");
                            TaskManager.Abort();
                            DyeAllItems();
                            return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        Log($"Dye button is disabled for {rowItem.Name}, skipping.");
                        TaskManager.Abort();
                        DyeAllItems();
                        return true;
                    }
                }
                return false;
            }, "Check if Dye button is disabled");

            // Check if red "x" is visible, if yes skip item because it means we don't have enough dye.
            // Otherwise, click "Dye" button
            TaskManager.Enqueue(() =>
            {
                if (IsAddonReady("ColorantColoring", out var addon))
                {
                    try
                    {
                        var indexOfDye = stain.SubOrder - 1;
                        var nodeIndex = indexOfDye == 0 ? 2 : 21000 + indexOfDye;

                        var redCrossImageNode = GenericHelpers.GetNodeByIDChain(addon->RootNode, 1, 22, 34, nodeIndex, 2, 4)->GetAsAtkImageNode();
                        if (redCrossImageNode->IsVisible())
                        {
                            Log($"Not enough dye for {rowItem.Name}.");
                            TaskManager.Abort();
                            DyeAllItems();
                            return true;
                        }
                        else
                        {
                            Callback.Fire(addon, true, 0);
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        Log($"Not enough dye for {rowItem.Name}.");
                        TaskManager.Abort();
                        DyeAllItems();
                        return true;
                    }
                }
                return false;
            }, "Check if player has enough dye, if yes click Dye button");

            TaskManager.EnqueueDelay(100);

            // Check if dye addon is still open, if yes close it, if not rowItem.DyeMatch = true;
            TaskManager.Enqueue(() =>
            {
                if (IsAddonReady("ColorantColoring", out var addon))
                    Callback.Fire(addon, true, 2);
                else
                    rowItem.DyeMatch = true;

                return true;
            }, "Close Dye addon or mark item as dyed");

            // Wait configured delay (Min 100ms)
            TaskManager.EnqueueDelay(Math.Max(Config.LoadInterval, 100));

            TaskManager.Enqueue(() =>
            {
                DyeAllItems();
                return true;
            }, "Process next item");
        }

        public unsafe bool IsAddonReady(string addonName, out AtkUnitBase* addonPtr)
        {
            AtkUnitBasePtr addonFromName = DalamudApi.GameGui.GetAddonByName(addonName);
            if (addonFromName == IntPtr.Zero)
            {
                addonPtr = null;
                return false;
            }

            var addon = (AtkUnitBase*)addonFromName.Address;

            if (!GenericHelpers.IsAddonReady(addon))
            {
                addonPtr = null;
                return false;
            }

            addonPtr = addon;
            return true;
        }


        public bool MatchItem(HousingItem item, uint itemKey)
        {
            if (item.ItemStruct != IntPtr.Zero) return false;       // this item is already matched. We can skip

            return item.ItemKey == itemKey && IsSelectedFloor(item.Y);
        }

        public unsafe bool MatchExactItem(HousingItem item, uint itemKey, HousingGameObject obj)
        {
            if (!MatchItem(item, itemKey)) return false;

            if (item.Stain != obj.color) return false;

            var matNumber = obj.Item->MaterialManager->MaterialSlot1;

            if (item.MaterialItemKey == 0 && matNumber == 0) return true;
            else if (item.MaterialItemKey != 0 && matNumber == 0) return false;

            var matItemKey = HousingData.Instance.GetMaterialItemKey(item.ItemKey, matNumber);
            if (matItemKey == 0) return true;

            return matItemKey == item.MaterialItemKey;

        }

        public unsafe void MatchLayout()
        {

            List<HousingGameObject> allObjects = null;
            Memory Mem = Memory.Instance;

            Quaternion rotateVector = new();
            var currentTerritory = Mem.GetCurrentTerritory();

            switch (currentTerritory)
            {
                case HousingArea.Indoors:
                    Mem.TryGetNameSortedHousingGameObjectList(out allObjects);
                    InteriorItemList.ForEach(item =>
                    {
                        item.ItemStruct = IntPtr.Zero;
                    });
                    break;

                case HousingArea.Outdoors:
                    GetPlotLocation();
                    allObjects = Mem.GetExteriorPlacedObjects();
                    ExteriorItemList.ForEach(item =>
                    {
                        item.ItemStruct = IntPtr.Zero;
                    });
                    rotateVector = Quaternion.CreateFromAxisAngle(Vector3.UnitY, PlotLocation.rotation);
                    break;
                case HousingArea.Island:
                    Mem.TryGetIslandGameObjectList(out allObjects);
                    ExteriorItemList.ForEach(item =>
                    {
                        item.ItemStruct = IntPtr.Zero;
                    });
                    break;
            }

            List<HousingGameObject> unmatched = new List<HousingGameObject>();

            // first we find perfect match
            foreach (var gameObject in allObjects)
            {
                if (!IsSelectedFloor(gameObject.Y)) continue;

                uint furnitureKey = gameObject.housingRowId;
                HousingItem houseItem = null;

                Vector3 localPosition = new Vector3(gameObject.X, gameObject.Y, gameObject.Z);
                float localRotation = gameObject.rotation;

                if (currentTerritory == HousingArea.Indoors)
                {
                    var furniture = DalamudApi.DataManager.GetExcelSheet<HousingFurniture>().GetRow(furnitureKey);
                    var itemKey = furniture.Item.Value.RowId;
                    houseItem = Utils.GetNearestHousingItem(
                        InteriorItemList.Where(item => MatchExactItem(item, itemKey, gameObject)),
                        localPosition
                    );
                }
                else
                {
                    if (currentTerritory == HousingArea.Outdoors)
                    {
                        localPosition = Vector3.Transform(localPosition - PlotLocation.ToVector(), rotateVector);
                        localRotation += PlotLocation.rotation;
                    }

                    var furniture = DalamudApi.DataManager.GetExcelSheet<HousingYardObject>().GetRow(furnitureKey);
                    var itemKey = furniture.Item.Value.RowId;
                    houseItem = Utils.GetNearestHousingItem(
                        ExteriorItemList.Where(item => MatchExactItem(item, itemKey, gameObject)),
                        localPosition
                    );

                }

                if (houseItem == null)
                {
                    unmatched.Add(gameObject);
                    continue;
                }

                // check if it's already correctly placed & rotated
                var locationError = houseItem.GetLocation() - localPosition;
                houseItem.CorrectLocation = locationError.Length() < 0.00001;

                // check for -180 and 180 - also 0
                float absRotation = Math.Abs(localRotation) + Math.Abs(houseItem.Rotate);
                houseItem.CorrectRotation =
                    Math.Abs(localRotation - houseItem.Rotate) < 0.001 ||
                    Math.Abs(absRotation - 2 * Math.PI) < 0.001 ||
                    absRotation < 0.001;

                houseItem.ItemStruct = (IntPtr)gameObject.Item;
            }

            UnusedItemList.Clear();

            // then we match even if the dye doesn't fit
            foreach (var gameObject in unmatched)
            {

                uint furnitureKey = gameObject.housingRowId;
                HousingItem houseItem = null;

                Item item;
                Vector3 localPosition = new Vector3(gameObject.X, gameObject.Y, gameObject.Z);
                float localRotation = gameObject.rotation;

                if (currentTerritory == HousingArea.Indoors)
                {
                    var furniture = DalamudApi.DataManager.GetExcelSheet<HousingFurniture>().GetRow(furnitureKey);
                    item = furniture.Item.Value;
                    houseItem = Utils.GetNearestHousingItem(
                        InteriorItemList.Where(hItem => MatchItem(hItem, item.RowId)),
                        new Vector3(gameObject.X, gameObject.Y, gameObject.Z)
                    );
                }
                else
                {
                    if (currentTerritory == HousingArea.Outdoors)
                    {
                        localPosition = Vector3.Transform(localPosition - PlotLocation.ToVector(), rotateVector);
                        localRotation += PlotLocation.rotation;
                    }
                    var furniture = DalamudApi.DataManager.GetExcelSheet<HousingYardObject>().GetRow(furnitureKey);
                    item = furniture.Item.Value;
                    houseItem = Utils.GetNearestHousingItem(
                        ExteriorItemList.Where(hItem => MatchItem(hItem, item.RowId)),
                        localPosition
                    );
                }
                if (houseItem == null)
                {
                    var unmatchedItem = new HousingItem(
                    item,
                    gameObject.color,
                    gameObject.X,
                    gameObject.Y,
                    gameObject.Z,
                    gameObject.rotation);
                    UnusedItemList.Add(unmatchedItem);
                    continue;
                }

                // check if it's already correctly placed & rotated
                var locationError = houseItem.GetLocation() - localPosition;
                houseItem.CorrectLocation = locationError.LengthSquared() < 0.0001;
                houseItem.CorrectRotation = localRotation - houseItem.Rotate < 0.001;

                houseItem.DyeMatch = false;

                houseItem.ItemStruct = (IntPtr)gameObject.Item;

            }

        }

        public unsafe void GetPlotLocation()
        {
            var mgr = Memory.Instance.HousingModule->outdoorTerritory;
            var territoryId = Memory.Instance.GetTerritoryTypeId();

            if (!DalamudApi.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
            {
                LogError($"Cannot identify territory: {territoryId}");
                return;
            }

            var placeName = row.Name.ToString();

            PlotLocation = Plots.Map[placeName][mgr->Plot + 1];
        }


        public unsafe void LoadExterior()
        {

            SaveLayoutManager.LoadExteriorFixtures();

            ExteriorItemList.Clear();

            var mgr = Memory.Instance.HousingModule->outdoorTerritory;

            var objectListAddr = (IntPtr)(&mgr->ObjectList);
            var activeObjList = (IntPtr)(mgr->Objects) - 0x08;

            var exteriorItems = Memory.GetContainer(InventoryType.HousingExteriorPlacedItems);

            GetPlotLocation();

            var rotateVector = Quaternion.CreateFromAxisAngle(Vector3.UnitY, PlotLocation.rotation);

            switch (PlotLocation.size)
            {
                case "s":
                    Layout.houseSize = "Small";
                    break;
                case "m":
                    Layout.houseSize = "Medium";
                    break;
                case "l":
                    Layout.houseSize = "Large";
                    break;

            }

            Layout.exteriorScale = 1;
            Layout.properties["entranceLayout"] = PlotLocation.entranceLayout;

            for (int i = 0; i < exteriorItems->Size; i++)
            {
                var item = exteriorItems->GetInventorySlot(i);
                if (item == null || item->ItemId == 0) continue;

                if (!DalamudApi.DataManager.GetExcelSheet<Item>().TryGetRow(item->ItemId, out var itemRow)) continue;

                var itemInfoIndex = GetYardIndex(mgr->Plot, (byte)i);

                var itemInfo = HousingObjectManager.GetItemInfo(mgr, itemInfoIndex);
                if (itemInfo == null)
                {
                    continue;
                }

                var location = new Vector3(itemInfo->Position.X, itemInfo->Position.Y, itemInfo->Position.Z);

                var newLocation = Vector3.Transform(location - PlotLocation.ToVector(), rotateVector);

                var housingItem = new HousingItem(
                    itemRow,
                    item->Stains[0],
                    newLocation.X,
                    newLocation.Y,
                    newLocation.Z,
                    itemInfo->Rotation + PlotLocation.rotation
                );

                var gameObj = (HousingGameObject*)GetObjectFromIndex(activeObjList, (uint)itemInfo->Index);

                if (gameObj == null)
                {
                    gameObj = (HousingGameObject*)GetGameObject(objectListAddr, itemInfoIndex);

                    if (gameObj != null)
                    {

                        location = new Vector3(gameObj->X, gameObj->Y, gameObj->Z);

                        newLocation = Vector3.Transform(location - PlotLocation.ToVector(), rotateVector);

                        housingItem.X = newLocation.X;
                        housingItem.Y = newLocation.Y;
                        housingItem.Z = newLocation.Z;
                    }
                }

                if (gameObj != null)
                {
                    housingItem.ItemStruct = (IntPtr)gameObj->Item;
                }

                ExteriorItemList.Add(housingItem);
            }

            Config.Save();
        }

        public bool IsSelectedFloor(float y)
        {
            if (Memory.Instance.GetCurrentTerritory() != Memory.HousingArea.Indoors || Memory.Instance.GetIndoorHouseSize().Equals("Apartment")) return true;

            if (y < -0.001) return Config.Basement;
            if (y >= -0.001 && y < 6.999) return Config.GroundFloor;

            if (y >= 6.999)
            {
                if (Memory.Instance.HasUpperFloor()) return Config.UpperFloor;
                else return Config.GroundFloor;
            }

            return false;
        }


        public unsafe void LoadInterior()
        {
            SaveLayoutManager.LoadInteriorFixtures();

            List<HousingGameObject> dObjects;
            Memory.Instance.TryGetNameSortedHousingGameObjectList(out dObjects);

            InteriorItemList.Clear();

            foreach (var gameObject in dObjects)
            {
                uint furnitureKey = gameObject.housingRowId;

                if (!DalamudApi.DataManager.GetExcelSheet<HousingFurniture>().TryGetRow(furnitureKey, out var furniture)) continue;

                if (!furniture.Item.IsValid) continue;

                Item item = furniture.Item.Value;

                if (item.RowId == 0) continue;

                if (!IsSelectedFloor(gameObject.Y)) continue;

                var housingItem = new HousingItem(item, gameObject);
                housingItem.ItemStruct = (IntPtr)gameObject.Item;

                if (gameObject.Item != null && gameObject.Item->MaterialManager != null)
                {
                    ushort material = gameObject.Item->MaterialManager->MaterialSlot1;
                    housingItem.MaterialItemKey = HousingData.Instance.GetMaterialItemKey(item.RowId, material);
                }

                InteriorItemList.Add(housingItem);
            }

            Config.Save();

        }


        public unsafe void LoadIsland()
        {
            SaveLayoutManager.LoadIslandFixtures();

            List<HousingGameObject> objects;
            Memory.Instance.TryGetIslandGameObjectList(out objects);
            ExteriorItemList.Clear();

            foreach (var gameObject in objects)
            {
                uint furnitureKey = gameObject.housingRowId;

                if (!DalamudApi.DataManager.GetExcelSheet<HousingFurniture>().TryGetRow(furnitureKey, out var furniture)) continue;
                if (!furniture.Item.IsValid) continue;

                Item item = furniture.Item.Value;

                if (item.RowId == 0) continue;

                var housingItem = new HousingItem(item, gameObject);
                housingItem.ItemStruct = (IntPtr)gameObject.Item;

                ExteriorItemList.Add(housingItem);
            }

            Config.Save();
        }

        public void GetGameLayout()
        {

            Memory Mem = Memory.Instance;
            var currentTerritory = Mem.GetCurrentTerritory();

            var itemList = currentTerritory == HousingArea.Indoors ? InteriorItemList : ExteriorItemList;
            itemList.Clear();

            switch (currentTerritory)
            {
                case HousingArea.Outdoors:
                    LoadExterior();
                    break;

                case HousingArea.Indoors:
                    LoadInterior();
                    break;

                case HousingArea.Island:
                    LoadIsland();
                    break;
            }

            DalamudApi.PluginLog.Debug(String.Format("Loaded {0} furniture items", itemList.Count));

            Config.HiddenScreenItemHistory = new List<int>();
            Config.Save();
        }


        public bool IsSaveLayoutDetour(IntPtr housingStruct)
        {
            var result = IsSaveLayoutHook.Original(housingStruct);

            if (ApplyChange)
            {
                ApplyChange = false;
                return true;
            }

            return result;
        }


        private void TerritoryChanged(ushort e)
        {
            Config.DrawScreen = false;
            Config.Save();
        }

        public unsafe void CommandHandler(string command, string arguments)
        {
            var args = arguments.Trim().Replace("\"", string.Empty);

            try
            {
                if (string.IsNullOrEmpty(args) || args.Equals("config", StringComparison.OrdinalIgnoreCase))
                {
                    Gui.ConfigWindow.Visible = !Gui.ConfigWindow.Visible;
                }
            }
            catch (Exception e)
            {
                LogError(e.Message, e.StackTrace);
            }
        }

        public static void Log(string message, string detail_message = "")
        {
            var msg = $"{message}";
            DalamudApi.PluginLog.Info(detail_message == "" ? msg : detail_message);
            DalamudApi.ChatGui.Print(msg);
        }
        public static void LogError(string message, string detail_message = "")
        {
            var msg = $"{message}";
            DalamudApi.PluginLog.Error(msg);

            if (detail_message.Length > 0) DalamudApi.PluginLog.Error(detail_message);

            DalamudApi.ChatGui.PrintError(msg);
        }

    }

}
