﻿using Lumina.Excel.Sheets;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ReMakePlacePlugin
{
    public enum SortType
    {
        Distance,
        Name
    }

    public enum ExteriorPartsType
    {
        None = -1,
        Roof = 0,
        Walls,
        Windows,
        Door,
        RoofOpt,
        WallOpt,
        SignOpt,
        Fence
    }

    public enum InteriorFloor
    {
        None = -1,
        Ground = 0,
        Upstairs,
        Basement,
        External
    }

    public enum InteriorPartsType
    {
        None = -1,
        Walls,
        Windows,
        Door,
        Floor,
        Light
    }

    public struct CommonFixture
    {
        public bool IsExterior;
        public int FixtureType;
        public int FixtureKey;
        public Stain? Stain;
        public Item? Item;

        public CommonFixture(bool isExterior, int fixtureType, int fixtureKey, Stain? stain, Item item)
        {
            IsExterior = isExterior;
            FixtureType = fixtureType;
            FixtureKey = fixtureKey;
            Stain = stain;
            Item = item;
        }
    }

    public enum HousingLayoutMode
    {
        None,
        Move,
        Rotate,
        Store,
        Place,
        Remove = 6
    }

    public enum ItemState
    {
        None = 0,
        Hover,
        SoftSelect,
        Active
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct HousingItemStruct
    {
        [FieldOffset(0x50)] public Vector3 Position;
        [FieldOffset(0x60)] public Quaternion Rotation;
        [FieldOffset(0xF8)] public ItemMaterialManager* MaterialManager;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct ItemMaterialManager
    {
        [FieldOffset(0xcc)] public ushort MaterialSlot1;
    }


    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct HousingStructure
    {
        [FieldOffset(0x0)] public HousingLayoutMode Mode;
        [FieldOffset(0x4)] public HousingLayoutMode LastMode;
        [FieldOffset(0x8)] public ItemState State;
        [FieldOffset(0x10)] public HousingItemStruct* HoverItem;
        [FieldOffset(0x18)] public HousingItemStruct* ActiveItem;
        [FieldOffset(0xB8)] public bool Rotating;
    }



    // This is just a GameObject
    [StructLayout(LayoutKind.Explicit, Size = 0x1D0)]
    public unsafe struct HousingGameObject
    {
        [FieldOffset(0x030)] public fixed byte name[64];
        [FieldOffset(0x080)] public uint housingRowId;
        [FieldOffset(0x084)] public uint OwnerID;
        [FieldOffset(0x0A0)] public float X;
        [FieldOffset(0x0A4)] public float Y;
        [FieldOffset(0x0A8)] public float Z;
        [FieldOffset(0x0f8)] public HousingItemStruct* Item;
        [FieldOffset(0x0B0)] public float rotation;
        [FieldOffset(0x198)] public uint housingRowId2;
        [FieldOffset(0x1A0)] public byte color;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct HousingObjectManager
    {
        [FieldOffset(0x0010)] public IntPtr ObjectList;
        [FieldOffset(0x8980)] public fixed ulong Objects[400];

        [FieldOffset(0x96A2)] public byte Ward;
        [FieldOffset(0x96A8)] public byte Plot;

        [FieldOffset(0x96E8)] public HousingGameObject* IndoorActiveObject2;
        [FieldOffset(0x96F0)] public HousingGameObject* IndoorHoverObject;
        [FieldOffset(0x96F8)] public HousingGameObject* IndoorActiveObject;
        [FieldOffset(0x9AB8)] public HousingGameObject* OutdoorActiveObject2;
        [FieldOffset(0x9AC0)] public HousingGameObject* OutdoorHoverObject;
        [FieldOffset(0x9AC8)] public HousingGameObject* OutdoorActiveObject;

        public static FFXIVClientStructs.FFXIV.Client.Game.HousingFurniture* GetItemInfo(HousingObjectManager* mgr, int index)
        {
            var objectListAddr = (IntPtr)mgr + 0x10;

            return (FFXIVClientStructs.FFXIV.Client.Game.HousingFurniture*)(objectListAddr + (0x30 * index));
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct HousingModule
    {
        [FieldOffset(0x0)] public HousingObjectManager* currentTerritory;
        [FieldOffset(0x8)] public HousingObjectManager* outdoorTerritory;
        [FieldOffset(0x10)] public HousingObjectManager* indoorTerritory;
        // [FieldOffset(0x9704)] public uint CurrentIndoorFloor;

        public HousingObjectManager* GetCurrentManager()
        {
            return outdoorTerritory != null ? outdoorTerritory : indoorTerritory;
        }

        public bool IsOutdoors()
        {
            return outdoorTerritory != null;
        }

        public bool IsIndoors()
        {
            return indoorTerritory != null;
        }

    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct LayoutWorld
    {
        [FieldOffset(0x20)] public LayoutManager* ActiveLayout;
        [FieldOffset(0x40)] public HousingStructure* HousingStruct;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct LayoutManager
    {
        [FieldOffset(0x20)] public uint TerritoryTypeId;
        [FieldOffset(0x80)] private readonly IntPtr _housingController;
        [FieldOffset(0x90)] private readonly IntPtr _indoorAreaData;

        public HousingController? HousingController
        {
            get
            {
                if (_housingController == IntPtr.Zero) return null;
                // return Unsafe.Read<HousingController>(_housingController.ToPointer());
                // return Marshal.PtrToStructure<HousingController>(_housingController);
                // return *(HousingController*) _housingController;

                return global::ReMakePlacePlugin.HousingController.Get(_housingController);
            }
        }

        public IndoorAreaData? IndoorAreaData
        {
            get
            {
                if (_indoorAreaData == IntPtr.Zero) return null;
                return global::ReMakePlacePlugin.IndoorAreaData.Get(_indoorAreaData);
            }
        }
    }

    public unsafe struct IndoorAreaData
    {
        public static IndoorAreaData Get(IntPtr address)
        {
            return new(address);
        }

        private IndoorAreaData(IntPtr thisPtr)
        {
            this._thisPtr = thisPtr;
        }

        public const int FloorMax = 4;
        private readonly IntPtr _thisPtr;

        public IndoorFloorData GetFloor(InteriorFloor index)
        {
            return IndoorFloorData.Get(new IntPtr((byte*)_thisPtr + (0x28 + (int)index * 0x14)));
        }

        public IndoorFloorData GetFloor(int index)
        {
            return IndoorFloorData.Get(new IntPtr((byte*)_thisPtr + (0x28 + index * 0x14)));
        }

        public float LightLevel => *(float*)((byte*)_thisPtr + 0x80);
    }

    public unsafe struct IndoorFloorData
    {
        public static IndoorFloorData Get(IntPtr address)
        {
            return new(address);
        }

        private IndoorFloorData(IntPtr thisPtr)
        {
            this._thisPtr = thisPtr;
        }

        public const int PartsMax = 5;
        private readonly IntPtr _thisPtr;

        public int GetPart(InteriorPartsType index)
        {
            return *(int*)((byte*)_thisPtr + (int)index * 4);
        }

        // returns key in sheet
        public int GetPart(int index)
        {

            return *(int*)(byte*)(_thisPtr + index * 4);
        }
    }

    // [StructLayout(LayoutKind.Explicit, Size = 28336)]
    public unsafe struct HousingController
    {
        public static HousingController Get(IntPtr address)
        {
            return new(address);
        }

        private HousingController(IntPtr thisPtr)
        {
            this._thisPtr = thisPtr;
        }

        public const int HousesMax = 60;
        private readonly IntPtr _thisPtr;

        //[FieldOffset(0x8)]
        public uint AreaType => *(uint*)(byte*)_thisPtr + 0x8;

        // [FieldOffset(0x1F0)]
        // [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
        // The size of the parent type of HouseCustomize is 464 even though HouseCustomize is 352
        public HouseCustomize Houses(int index)
        {
            return HouseCustomize.Get(new IntPtr((byte*)_thisPtr + (0x1F0 + index * 464)));
        }
    }

    // [StructLayout(LayoutKind.Explicit, Size = 352)]
    public unsafe struct HouseCustomize
    {
        public static HouseCustomize Get(IntPtr address)
        {
            return new(address);
        }

        private HouseCustomize(IntPtr thisPtr)
        {
            this._thisPtr = thisPtr;
        }

        public const int PartsMax = 8;
        private readonly IntPtr _thisPtr;

        //[FieldOffset(0x10)]
        public int Size => *(int*)(byte*)_thisPtr + 0x10;

        public HousePart GetPart(int type)
        {
            return *(HousePart*)((byte*)_thisPtr + (0x20 + type * 40));
        }

        public HousePart GetPart(ExteriorPartsType type)
        {
            return *(HousePart*)((byte*)_thisPtr + (0x20 + (int)type * 40));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public unsafe struct HousePart
    {
        [FieldOffset(0x00)] public int Category;
        [FieldOffset(0x04)] private readonly int Unknown1;
        [FieldOffset(0x08)] public ushort FixtureKey;
        [FieldOffset(0x0A)] public byte Color;
        [FieldOffset(0x0B)] private readonly byte Padding;
        [FieldOffset(0x0C)] private readonly int Unknown2;
        [FieldOffset(0x10)] private readonly void* Unknown3;
    }

    // This is just MJIManager
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct MjiManagerExtended
    {
        [FieldOffset(0x160)] public IslandObjectManager* ObjectManager;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct IslandObjectManager
    {
        [FieldOffset(0x78)] public IslandFurnitureManager* FurnitureManager;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct IslandFurnitureManager
    {
        [FieldOffset(0x1698)] public IntPtr ObjectList;
        [FieldOffset(0x16A0)] public fixed ulong Objects[400];
    }

}