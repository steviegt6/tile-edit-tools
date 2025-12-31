using System;
using System.IO;
using Daybreak.Common.Features.Models;
using Terraria;
using Terraria.ID;

namespace TileEditTools;

partial class ModImpl : INameProvider
{
    string INameProvider.GetName(Type type)
    {
        return NameProvider.GetNestedName(type);
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        base.HandlePacket(reader, whoAmI);

        var packetKind = (Networking.PacketKind)reader.ReadByte();
        switch (packetKind)
        {
            case Networking.PacketKind.CustomTileManipulation:
            {
                var tileManipulationKind = (Networking.TileManipulationKind)reader.ReadByte();
                var tileX = reader.ReadUInt16();
                var tileY = reader.ReadUInt16();

                if (!WorldGen.InWorld(tileX, tileY, fluff: 3))
                {
                    break;
                }

                switch (tileManipulationKind)
                {
                    case Networking.TileManipulationKind.ToggleTileStasis:
                        StasisRod.ToggleStasis(tileX, tileY);
                        break;

                    case Networking.TileManipulationKind.ForceToggleActuation:
                        Wiring.SetCurrentUser(whoAmI);
                        MetaActuationRod.ToggleActuate(tileX, tileY);
                        Wiring.SetCurrentUser();
                        break;
                }

                if (Main.netMode == NetmodeID.Server)
                {
                    var p = GetPacket();
                    {
                        p.Write((byte)Networking.PacketKind.CustomTileManipulation);
                        p.Write((byte)tileManipulationKind);
                        p.Write(tileX);
                        p.Write(tileY);
                    }
                    p.Send(toClient: -1, ignoreClient: whoAmI);
                }

                StasisRod.SetStasisOn(tileX, tileY);
                break;
            }

            case Networking.PacketKind.TileSectionExtras:
            {
                var tileX = reader.ReadInt32();
                var tileY = reader.ReadInt32();
                var width = reader.ReadUInt16();
                var height = reader.ReadUInt16();

                var framingPrevented = CompactBitArray.Deserialize(reader).ToBitArray();

                var i = 0;
                for (var x = tileX; x < tileX + width; x++)
                for (var y = tileY; y < tileY + height; y++)
                {
                    var tile = Main.tile[x, y];
                    {
                        tile.Get<StasisRod.TileData>().FramingPrevented = framingPrevented[i];
                    }
                    i++;
                }

                break;
            }

            case Networking.PacketKind.TileSquareExtras:
            {
                var tileX = reader.ReadUInt16();
                var tileY = reader.ReadUInt16();
                var width = reader.ReadByte();
                var height = reader.ReadByte();

                var framingPrevented = CompactBitArray.Deserialize(reader).ToBitArray();

                var i = 0;
                for (var x = tileX; x < tileX + width; x++)
                for (var y = tileY; y < tileY + height; y++)
                {
                    var tile = Main.tile[x, y];
                    {
                        tile.Get<StasisRod.TileData>().FramingPrevented = framingPrevented[i];
                    }
                    i++;
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
