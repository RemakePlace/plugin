using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;

namespace ReMakePlacePlugin;

public class HousingData
{
    public static HousingData Instance { get; private set; }

    private readonly Dictionary<uint, HousingFurniture> _furnitureDict;
    private readonly Dictionary<uint, HousingFurniture> _itemFurnitureDict;
    private readonly Dictionary<uint, Item> _itemDict;
    private readonly Dictionary<uint, Stain> _stainDict;

    private readonly Dictionary<uint, uint> _unitedDict;
    private readonly Dictionary<uint, HousingYardObject> _yardObjectDict;
    private readonly Dictionary<uint, HousingYardObject> _itemYardObjectDict;

    private readonly Dictionary<ushort, uint> _wallpaper;
    private readonly Dictionary<ushort, uint> _smallFishprint;
    private readonly Dictionary<ushort, uint> _mediumFishprint;
    private readonly Dictionary<ushort, uint> _largeFishprint;
    private readonly Dictionary<ushort, uint> _extraLargeFishprint;
    private readonly Dictionary<ushort, uint> _painting;

    private static ReMakePlacePlugin Plugin;

    private HousingData()
    {
        var sheet = Svc.Data.GetExcelSheet<HousingLandSet>();
        uint[] terriKeys = { 339, 340, 341, 641 };

        var unitedExteriorSheet = Svc.Data.GetExcelSheet<HousingUnitedExterior>();

        _unitedDict = new Dictionary<uint, uint>();
        foreach (var row in unitedExteriorSheet)
        {
            // Ugly but cba to do better now
            if (row.Roof.RowId != 0)
                _unitedDict[row.Roof.RowId] = row.RowId;

            if (row.Walls.RowId != 0)
                _unitedDict[row.Walls.RowId] = row.RowId;

            if (row.Windows.RowId != 0)
                _unitedDict[row.Windows.RowId] = row.RowId;

            if (row.Door.RowId != 0)
                _unitedDict[row.Door.RowId] = row.RowId;

            if (row.OptionalRoof.RowId != 0)
                _unitedDict[row.OptionalRoof.RowId] = row.RowId;

            if (row.OptionalWall.RowId != 0)
                _unitedDict[row.OptionalWall.RowId] = row.RowId;

            if (row.OptionalSignboard.RowId != 0)
                _unitedDict[row.OptionalSignboard.RowId] = row.RowId;

            if (row.Fence.RowId != 0)
                _unitedDict[row.Fence.RowId] = row.RowId;
        }

        _itemDict = Svc.Data.GetExcelSheet<Item>()
            .Where(item => item.AdditionalData.RowId != 0 && (item.ItemSearchCategory.RowId == 65 || item.ItemSearchCategory.RowId == 66))
            .ToDictionary(row => row.AdditionalData.RowId, row => row);

        _stainDict = Svc.Data.GetExcelSheet<Stain>().ToDictionary(row => row.RowId, row => row);
        _furnitureDict = Svc.Data.GetExcelSheet<HousingFurniture>().ToDictionary(row => row.RowId, row => row);
        _itemFurnitureDict = Svc.Data.GetExcelSheet<HousingFurniture>().Where(furniture => furniture.Item.IsValid && furniture.Item.RowId != 0).ToDictionary(furniture => furniture.Item.RowId);
        _yardObjectDict = Svc.Data.GetExcelSheet<HousingYardObject>().ToDictionary(row => row.RowId, row => row);
        _itemYardObjectDict = Svc.Data.GetExcelSheet<HousingYardObject>().Where(furniture => furniture.Item.IsValid && furniture.Item.RowId != 0).ToDictionary(furniture => furniture.Item.RowId);

        Svc.Log.Info($"Loaded {_furnitureDict.Keys.Count} furniture");
        Svc.Log.Info($"Loaded {_yardObjectDict.Keys.Count} yard objects");
        Svc.Log.Info($"Loaded {_unitedDict.Keys.Count} united parts");
        Svc.Log.Info($"Loaded {_stainDict.Keys.Count} dyes");
        Svc.Log.Info($"Loaded {_itemDict.Keys.Count} items with AdditionalData");

        _wallpaper = new Dictionary<ushort, uint>();
        _smallFishprint = new Dictionary<ushort, uint>();
        _mediumFishprint = new Dictionary<ushort, uint>();
        _largeFishprint = new Dictionary<ushort, uint>();
        _extraLargeFishprint = new Dictionary<ushort, uint>();
        _painting = new Dictionary<ushort, uint>(); //could make a reverse for painting lookups?

        var materialSheet = Svc.Data.GetExcelSheet<VaseFlower>();

        foreach (var row in materialSheet)
        {
            var id = row.RowId;

            if (id < 1000) continue;
            else if (id > 1000 && id < 2000) _painting.TryAdd(row.Unknown0, row.Item.RowId);
            else if (id > 2000 && id < 3000) _wallpaper.TryAdd(row.Unknown0, row.Item.RowId);
            else if (id > 3000 && id < 4000) _smallFishprint.TryAdd(row.Unknown0, row.Item.RowId);
            else if (id > 4000 && id < 5000) _mediumFishprint.TryAdd(row.Unknown0, row.Item.RowId);
            else if (id > 5000 && id < 6000) _largeFishprint.TryAdd(row.Unknown0, row.Item.RowId);
            else if (id > 6000 && id < 7000) _extraLargeFishprint.TryAdd(row.Unknown0, row.Item.RowId);

        }
    }

    public static void Init(ReMakePlacePlugin plugin)
    {
        Plugin = plugin;
        Instance = new HousingData();
    }

    public bool TryGetYardObject(uint id, out HousingYardObject yardObject)
    {
        return _yardObjectDict.TryGetValue(id, out yardObject);
    }

    public bool TryGetFurniture(uint id, out HousingFurniture furniture)
    {
        return _furnitureDict.TryGetValue(id, out furniture);
    }
    
    public bool TryGetFurnitureByItemId(uint id, out HousingFurniture furniture)
    {
        return _itemFurnitureDict.TryGetValue(id, out furniture);
    }

    public bool TryGetYardObjectByItemId(uint id, out HousingYardObject yardObject)
    {
        return _itemYardObjectDict.TryGetValue(id, out yardObject);
    }

    public bool IsUnitedExteriorPart(uint id, out Item item)
    {
        item = new Item();

        if (!_unitedDict.TryGetValue(id, out var unitedId))
            return false;

        if (!_itemDict.TryGetValue(unitedId, out item))
            return false;

        return true;
    }

    public bool TryGetItem(uint id, out Item item)
    {
        return _itemDict.TryGetValue(id, out item);
    }

    public bool TryGetStain(uint id, out Stain stain)
    {
        return _stainDict.TryGetValue(id, out stain);
    }

    public uint GetMaterialItemKey(uint itemId, ushort material)
    {
        if (material == 0) return 0;

        // Angler Canvas
        if (itemId == 28931) return _smallFishprint.GetValueOrDefault(material);
        else if (itemId == 28932) return _mediumFishprint.GetValueOrDefault(material);
        else if (itemId == 28933) return _largeFishprint.GetValueOrDefault(material);
        else if (itemId == 28934) return _extraLargeFishprint.GetValueOrDefault(material);

        if (itemId >= 16935 && itemId <= 16937)
        {
            // Picture Frame
            return _painting.GetValueOrDefault(material);
        }

        return _wallpaper.GetValueOrDefault(material);

    }
}