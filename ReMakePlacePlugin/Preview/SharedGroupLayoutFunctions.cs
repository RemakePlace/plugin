using System;
using System.Text;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;

namespace ReMakePlacePlugin.Preview;

public unsafe class SharedGroupLayoutFunctions
{
    internal delegate sbyte InitSgbDelegate(SharedGroupLayoutInstance* self, nint* initArgs, byte* pathBytes, byte a4);
    internal delegate bool ReadyToStainDelegate(SharedGroupLayoutInstance* self);
    internal delegate SharedGroupLayoutInstance* CtorDelegate(SharedGroupLayoutInstance* self);
    internal delegate byte LoadSgbDelegate(SharedGroupLayoutInstance* self, byte* pathBytes);
    internal delegate LayerManager* LayerCtorDelegate(LayerManager* self, FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutManager* owner);
    internal delegate bool TryLoadMaterialDelegate(SharedGroupLayoutInstance* self, nint a2, ushort Key);
    
    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 33 F6 C7 41 ?? ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 48 89 71 ?? ?? ?? ?? 48 8B F9 48 89 71 ?? 89 71 ?? 48 89 71 ?? C7 41 ?? ?? ?? ?? ?? 48 83 C1 ?? E8 ?? ?? ?? ?? 48 89 77")]
    internal CtorDelegate? CtorInternal = null;
    
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B 81 ?? ?? ?? ?? 48 8D 79")]
    internal LoadSgbDelegate? LoadSgbInternal = null;
    
    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC ?? 48 8B B1 ?? ?? ?? ?? 45 0F B6 F9")]
    internal InitSgbDelegate? InitSgbInternal = null;

    [Signature("40 56 48 83 EC ?? 8B 91 ?? ?? ?? ?? 48 8B F1 C1 E2 ?? C1 FA ?? 85 D2 74 ?? 83 EA ?? 74 ?? 83 EA ?? 74 ?? B0")]
    internal ReadyToStainDelegate? ReadyToStainInternal = null;
    
    [Signature("40 53 48 83 EC ?? 48 8B D9 48 89 51 ?? C7 41 ?? ?? ?? ?? ?? 48 8D 05")]
    internal LayerCtorDelegate? LayerCtorInternal = null;
    
    [Signature("E8 ?? ?? ?? ?? 8B D7 45 33 C9")]
    internal TryLoadMaterialDelegate? TryLoadMaterialInternal = null; // Hard to name, but uses the keys provided by VaseFlower sheet
    
    public SharedGroupLayoutFunctions()
    {
        Svc.Hook.InitializeFromAttributes(this);
    }

    public SharedGroupLayoutInstance* Ctor(SharedGroupLayoutInstance* self)
    {
        if (CtorInternal == null)
            throw new InvalidOperationException("Ctor sig was not found!");
        
        return CtorInternal(self);
    }

    public sbyte Init(SharedGroupLayoutInstance* self, string path)
    {
        if (InitSgbInternal == null)
            throw new InvalidOperationException("CreateSgb sig was not found!");

        var bytes = Encoding.UTF8.GetBytes(path + "\0");

        LayerManager* layer;
        
        if (!FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutWorld.Instance()->GlobalLayout->Layers.ContainsKey(6969))
        {
            var manager = IMemorySpace.GetDefaultSpace()->Malloc<LayerManager>();
            layer = LayerCtor(manager, FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutWorld.Instance()->GlobalLayout);
            layer->Id = 6969;
            layer->Initialize();
            layer = manager;
        }
        else
        {
            layer = FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutWorld.Instance()->GlobalLayout->Layers[6969];
        }
        
        fixed (byte* pathPtr = bytes)
        {
            var initArgs = stackalloc nint[2];
            initArgs[0] = (nint)layer;
            initArgs[1] = 0;
            
            var result = InitSgbInternal(self, initArgs, pathPtr, 1);
            return result;
        }
    }

    public bool ReadyToStain(SharedGroupLayoutInstance* self)
    {
        if (ReadyToStainInternal == null)
            throw new InvalidOperationException("ReadyToStain sig was not found!");
        
        return ReadyToStainInternal(self);
    }
    
    public LayerManager* LayerCtor(LayerManager* self, FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutManager* owner)
    {
        if (LayerCtorInternal == null)
            throw new InvalidOperationException("LayerCtorInternal sig was not found!");
        
        return LayerCtorInternal(self , owner);
    }

    /// <summary>
    /// Loads material's onto an SGB. Namely; wallpapers, paintings, flowers, and fish prints.
    /// </summary>
    /// <param name="self">The group to load onto</param>
    /// <param name="key">Key for the material, found via the VaseFlower sheet</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public bool TryLoadMaterial(SharedGroupLayoutInstance* self, ushort key)
    {
        if (TryLoadMaterialInternal == null)
            throw new InvalidOperationException("LoadVaseFlower sig was not found!");
        
        return TryLoadMaterialInternal(self, 0, key);
    }
}
