using System;
using System.IO;
using Daybreak.Common.Features.Models;
using Terraria;
using Terraria.ID;

namespace TileEditTools;

internal enum PacketKind : byte
{
    CustomTileManipulation,
}

internal enum TileManipulationKind : byte
{
    ToggleTileStasis,
    ForceToggleActuation,
}

partial class ModImpl : INameProvider
{
    string INameProvider.GetName(Type type)
    {
        return NameProvider.GetNestedName(type);
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        base.HandlePacket(reader, whoAmI);
        
        var packetKind = (PacketKind)reader.ReadByte();
        switch (packetKind)
        {
            case PacketKind.CustomTileManipulation:
            {
                var tileManipulationKind = (TileManipulationKind)reader.ReadByte();
                var tileX = reader.ReadUInt16();
                var tileY = reader.ReadUInt16();

                if (!WorldGen.InWorld(tileX, tileY, fluff: 3))
                {
                    break;
                }

                switch (tileManipulationKind)
                {
                    case TileManipulationKind.ToggleTileStasis:
                        StasisRod.ToggleStasis(tileX, tileY);
                        break;

                    case TileManipulationKind.ForceToggleActuation:
                        Wiring.SetCurrentUser(whoAmI);
                        MetaActuationRod.ToggleActuate(tileX, tileY);
                        Wiring.SetCurrentUser();
                        break;
                }

                if (Main.netMode == NetmodeID.Server)
                {
                    var p = GetPacket();
                    {
                        p.Write((byte)PacketKind.CustomTileManipulation);
                        p.Write((byte)tileManipulationKind);
                        p.Write(tileX);
                        p.Write(tileY);
                    }
                    p.Send(toClient: -1, ignoreClient: whoAmI);
                }
                
                StasisRod.SetStasisOn(tileX, tileY);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
