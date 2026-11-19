using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.Interop;

namespace ReMakePlacePlugin.Extensions;

[Flags]
public enum MaterialFlag : ushort
{
    None = 0,
    Unk4 = 1 << 4,
    Unk5 = 1 << 5,
    Unk6 = 1 << 6,
    Unk7 = 1 << 7,
    Unk8 = 1 << 8,
    Unk9 = 1 << 9,
    Unk10 = 1 << 10,
    Unk11 = 1 << 11,
    CameraCollision = 1 << 12,
    PlayerCollision = 1 << 13,
    CursorCollision = 1 << 14,
    Unk15 = 1 << 15,
}

public enum MaterialType : sbyte
{
    None = 0,
    Dirt = 0x01, // "dart" in the scd path
    Grass = 0x02,
    Sand = 0x03,
    Stone = 0x04,
    Wood = 0x05,
    Metal = 0x06,
    Gravel = 0x07,
    Leaf = 0x08,
    Powder = 0x09, // could be wrong
    Carpet = 0x0A,
    Snow = 0x0B,
    Water1 = 0x0C, // only used on a few water related items... for some reason
    Water2 = 0x0D,
    Mesh = 0x0E,
    Sticky = 0x0F
}

public static unsafe class ColliderExtensions
{
    extension (Pointer<Collider> collider)
    {
        private ulong GetAllMaterialValue()
        {
            if (collider.IsNull) return 0;
            
            ulong value = collider.Value->ObjectMaterialValue;

            if (collider.Value->GetColliderType() == ColliderType.Mesh)
            {
                var cast = (ColliderMesh*)collider.Value;
                if (!cast->MeshIsSimple && cast->Mesh != null)
                {
                    var mesh = (MeshPCB*)cast->Mesh;
                    RunForTree(mesh->RootNode, prim =>
                    {
                        value |= prim.Value->Material;
                    });
                }
            }
        
            return value;
        }
        
        public MaterialFlag GetMaterialFlag()
        {
            if (collider.IsNull) return MaterialFlag.None;

            ulong value = collider.GetAllMaterialValue();
            return (MaterialFlag)((value & 0x0000FFF0));
        }

        public MaterialFlag GetMaterialMask()
        {
            if (collider.IsNull) return MaterialFlag.None;
        
            return (MaterialFlag)((collider.Value->ObjectMaterialMask & 0x0000FFFF));
        }

        public void SetMaterialMask(MaterialFlag mask, bool enabled = true)
        {
            if (collider.IsNull) return;

            if (enabled)
            {
                collider.Value->ObjectMaterialMask |= (ulong)mask;
            }
            else
            {
                collider.Value->ObjectMaterialMask &= ~(ulong)mask;
            }
        }
    
        public HashSet<MaterialType> GetMaterialTypes()
        {
            if (collider.IsNull) return [];

            HashSet<MaterialType> types = [];

            if (collider.Value->GetColliderType() == ColliderType.Mesh)
            {
                var cast = (ColliderMesh*)collider.Value;
                if (!cast->MeshIsSimple && cast->Mesh != null)
                {
                    var mesh = (MeshPCB*)cast->Mesh;
                    RunForTree(mesh->RootNode, prim =>
                    {
                        types.Add((MaterialType)(prim.Value->Material & 0x7F));
                    });
                }
            }
            else
            {
                types = [(MaterialType)(collider.Value->ObjectMaterialValue & 0x7F)];
            }
        
            return types;
        }

        public void SetMaterialType(MaterialType materialType)
        {
            if (collider.IsNull) return;
            
            if (collider.Value->GetColliderType() == ColliderType.Mesh)
            {
                var cast = (ColliderMesh*)collider.Value;
                if (!cast->MeshIsSimple && cast->Mesh != null)
                {
                    var mesh = (MeshPCB*)cast->Mesh;
                    RunForTree(mesh->RootNode, prim =>
                    {
                        prim.Value->Material = prim.Value->Material & ~0xFFU | (byte)materialType;
                    });
                }
            }
            else
            {
                collider.Value->ObjectMaterialValue = collider.Value->ObjectMaterialValue & ~0xFFU | (byte)materialType;
            }
        }
    }

    private static void RunForTree(MeshPCB.FileNode* mesh, Action<Pointer<Mesh.Primitive>> action)
    {
        while (mesh != null)
        {
            Mesh.Primitive* ptr = mesh->PrimitivesPtr;
            for (int i = 0; i < mesh->NumPrims; i++)
            {
                Mesh.Primitive* prim = ptr + i;
                action(prim);
            }

            RunForTree(mesh->Child1, action);
            mesh = mesh->Child2;
        }
    }
}