using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Numerics;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Node;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.Interop;
using ReMakePlacePlugin.Extensions;

namespace ReMakePlacePlugin.Preview;

public class SharedGroup : IDisposable
{
    public Pointer<SharedGroupLayoutInstance> Data;
    private CancellationToken Token { get; init; }

    // Easy way to have unique IDs for every spawned object
    private static uint _idCounter;
    public uint Id { get; } = Interlocked.Increment(ref _idCounter);

    public Vector3 Position { get; set; } = Vector3.Zero;
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;

    public Vector4? Color
    {
        get;
        set
        {
            field = value;
            SetColor(value);
        }
    }

    public string Path { get; init; } = string.Empty;
    public ushort MaterialKey { get; set; } = 0;

    public SharedGroup(string path, CancellationToken token = new())
    {
        if (!Svc.Data.FileExists(path))
        {
            Svc.Log.Error($"SGB {path} not found!");
            return;
        }

        Path = path;
        Token = token;

        if (!TryCreateSharedGroup())
        {
            return;
        }
        
        Task.Run(Init, Token);
    }

    private unsafe bool TryCreateSharedGroup()
    {
        Data = IMemorySpace.GetDefaultSpace()->Malloc<SharedGroupLayoutInstance>();

        Svc.Log.Verbose($"Attempting to create shared group {Path}");

        try
        {
            Memory.SharedGroupLayoutFunctions.Ctor(Data);
            Memory.SharedGroupLayoutFunctions.Init(Data, Path); 
            UpdateTransform();
            return true;
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to create shared group {Path}: {e}");
        }
        
        return false;
    }

    private async Task Init()
    {
        if (Token.IsCancellationRequested) return;
        if (Data.IsNull) throw new InvalidOperationException("Data is null");
    
        bool loaded = IsLoaded();
    
        while (!loaded)
        {
            if (Token.IsCancellationRequested) return;
            loaded = IsLoaded();
            await Task.Delay(16, Token).ConfigureAwait(false);
        }
    
        await Svc.Framework.Run(OnLoad, Token);
        // abyeon: my wip local lib usually has an update task initiated here, but preview objects don't really need to be updated.
        // If in the future there is an effort to make a local house editor, that will need to change.
    }

    private void OnLoad()
    {
        UpdateTransform();
        
        if (MaterialKey != 0) LoadMaterial(0, MaterialKey);
        
        Svc.Framework.RunOnTick(() => ApplyStainTask(null!), delayTicks: 1, cancellationToken: Token);
    }

    private unsafe bool IsLoaded()
    {
        return !Data.IsNull && Data.Value->IsPrimaryReady();
    }

    public unsafe void UpdateTransform()
    {
        if (Data.IsNull) return;
        FFXIVClientStructs.FFXIV.Client.LayoutEngine.Transform* transform = Data.Value->GetTransformImpl();

        if (transform == null) return;
        transform->Translation = Position;
        transform->Rotation = Rotation;
        transform->Scale = Scale;
        
        Data.Value->SetTransformImpl(transform);
        Data.Value->SetColliderActive(false);
        
        // Some notes on this masking approach:
        // - Preview furniture will cause pumps / snapping to occur with real furniture
        // - Some furniture just... still interacts with the player.
        // SetCollisionMask(MaterialFlag.PlayerCollision | MaterialFlag.CameraCollision); // Very risky, needs extensive testing to make sure this is safe!

        foreach (Pointer<ChildNodeInstance> ptr in Data.Value->Instances.Instances)
        {
            if (ptr.IsNull) continue;
            ILayoutInstance* instance = ptr.Value->Instance;

            // Kill the chairs (and beds)! Just in case. For some reason you cant sit in them without this anyway- but I remember that being different.
            if (instance->Id.Type == InstanceType.ChairMarker)
            {
                instance->Deinit();
                instance->Dtor(0);
            }
            
            var graphics = (DrawObject*)instance->GetGraphics();
            if (graphics == null) continue;
            graphics->UpdateCulling();
            graphics->UpdateRender();
        }
    }

    public unsafe void SetCollisionMask(MaterialFlag flag, bool enabled = true)
    {
        if (Data.IsNull) return;
        foreach (Pointer<ChildNodeInstance> instance in Data.Value->Instances.Instances)
        {
            if (instance.IsNull) continue;

            if (instance.Value->Instance->GetCollider() == null) continue;

            Pointer<Collider> coll = instance.Value->Instance->GetCollider();

            if (coll.IsNull) continue;
            coll.SetMaterialMask(flag, enabled);
        }
    }
    
    public void SetColor(Vector4? color)
    {
        if (Data == null || TrySetColor(color)) return;
        ApplyStainTask(null!);
    }

    private unsafe bool TrySetColor(Vector4? color)
    { 
        var layout = (LayoutManager*)Data.Value->Layout;
        if (!Memory.SharedGroupLayoutFunctions.ReadyToStain(Data)) return false;
        
        ByteColor byteColor;
        if (!color.HasValue)
        {
            byteColor = *SharedGroupLayoutInstance.GetObjectStainColorByIndex(Data.Value->StainInfo->DefaultStainIndex);
        }
        else
        {
            byteColor = color.Value.ToByteColor();
        }
        
        Data.Value->ApplyStain(&byteColor);
        Data.Value->ReapplyStain();

        return layout->StainNeedsUpdating != 1;
    }

    private void ApplyStainTask(IFramework framework)
    {
        Svc.Log.Verbose($"Applying stain {Color} to {Path}");
        if (Data == null || TrySetColor(Color))
        {
            Svc.Log.Verbose($"Successfully applied stain!");
            Svc.Framework.Update -= ApplyStainTask;
            return;
        }
        
        Svc.Log.Verbose($"Failed to apply stain, attempting again..");
        Svc.Framework.Update += ApplyStainTask;
    }

    public unsafe void SetTransparency(float transparency)
    {
        foreach (Pointer<ChildNodeInstance> ptr in Data.Value->Instances.Instances)
        {
            if (ptr.Value == null) continue;
            ILayoutInstance* instance = ptr.Value->Instance;
            var graphics = (BgObject*)instance->GetGraphics();
            if (graphics == null) continue;
            graphics->SetTransparency(transparency);
        }
    }

    // for some reason necessary, using ->GetGraphics on SGLs just doesnt work.
    private unsafe Pointer<BgObject> GetGraphics()
    {
        foreach (Pointer<ChildNodeInstance> ptr in Data.Value->Instances.Instances)
        {
            if (ptr.Value == null) continue;
            ILayoutInstance* instance = ptr.Value->Instance;
            var graphics = (BgObject*)instance->GetGraphics();
            if (graphics == null) continue;
            return graphics;
        }

        return null;
    }
    
    public unsafe void SetCustomMaterial(string path)
    {
        Svc.Log.Verbose($"Attempting to set custom material {path}");
        
        if (Data.IsNull) return;
        Pointer<BgObject> graphics = GetGraphics();
        
        if (graphics.IsNull) return;
        
        BgObjectStainBuffer* stain = graphics.Value->StainBuffer;
        if (stain is null) return;
        
        Svc.Log.Verbose($"Setting custom material {path}");
        stain->SetCustomMaterialResource(path);
    }
    
    public unsafe void LoadMaterial(long slot, ushort key)
    {
        Svc.Log.Verbose($"Attempting to load material {key}");

        if (Data.IsNull) return;

        try
        {
            Memory.SharedGroupLayoutFunctions.TryLoadMaterial(Data.Value, slot, key);
            Svc.Log.Verbose($"Successfully loaded material {key}");
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to load material {key}", e);
        }
    }

    public unsafe void Dispose()
    {
        Svc.Log.Verbose($"Disposing {Path}");
        
        Svc.Framework.Update -= ApplyStainTask;
        ThreadSafety.AssertMainThread();

        if (Data == null) return;
        
        Data.Value->Deinit();
        Data.Value->Dtor(0);

        IMemorySpace.Free(Data.Value);
        Data = null;
    }
}