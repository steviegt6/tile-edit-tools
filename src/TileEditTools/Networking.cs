using System.Collections;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TileEditTools;

public static class Networking
{
    public enum PacketKind : byte
    {
        CustomTileManipulation,
        TileSectionExtras,
        TileSquareExtras,
    }

    public enum TileManipulationKind : byte
    {
        ToggleTileStasis,
        ForceToggleActuation,
    }

    public static void SyncTileSquare(int tileX, int tileY)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        NetMessage.SendTileSquare(-1, tileX, tileY);
    }

    private static void HijackNetMessages()
    {
        On_NetMessage.SendData += SendData_SendFollowUpPackets;
    }

    private static void SendData_SendFollowUpPackets(
        On_NetMessage.orig_SendData orig,
        int msgType,
        int remoteClient,
        int ignoreClient,
        NetworkText text,
        int number,
        float number2,
        float number3,
        float number4,
        int number5,
        int number6,
        int number7
    )
    {
        orig(
            msgType,
            remoteClient,
            ignoreClient,
            text,
            number,
            number2,
            number3,
            number4,
            number5,
            number6,
            number7
        );

        switch (msgType)
        {
            case MessageID.TileSection:
                SendTileSectionExtras(
                    number,
                    (int)number2,
                    (ushort)number3,
                    (ushort)number4
                );
                break;

            case MessageID.TileSquare:
                SendTileSquareExtras(
                    number,
                    (int)number2,
                    (int)number3,
                    (int)number4
                );
                break;
        }
    }

    private static void SendTileSectionExtras(
        int tileX,
        int tileY,
        ushort width,
        ushort height
    )
    {
        var p = ModContent.GetInstance<ModImpl>().GetPacket();
        {
            p.Write((byte)PacketKind.TileSectionExtras);
        }

        p.Write(tileX);
        p.Write(tileY);
        p.Write(width);
        p.Write(height);

        var framingPrevented = new BitArray(width * height);

        var i = 0;
        for (var x = tileX; x < tileX + width; x++)
        for (var y = tileY; y < tileY + height; y++)
        {
            var tile = Main.tile[x, y];
            {
                framingPrevented[i] = tile.Get<StasisRod.TileData>().FramingPrevented;
            }
            i++;
        }

        var framingPreventedBits = CompactBitArray.FromBitArray(framingPrevented);
        {
            framingPreventedBits.Serialize(p);
        }
    }

    private static void SendTileSquareExtras(
        int tileX,
        int tileY,
        int width,
        int height
    )
    {
        if (width < 0)
        {
            width = 0;
        }

        if (height < 0)
        {
            height = 0;
        }

        if (tileX < width)
        {
            tileX = width;
        }

        if (tileX >= Main.maxTilesX + width)
        {
            tileX = Main.maxTilesX - width - 1;
        }

        if (tileY < height)
        {
            tileY = height;
        }

        if (tileY >= Main.maxTilesY + height)
        {
            tileY = Main.maxTilesY - height - 1;
        }

        var p = ModContent.GetInstance<ModImpl>().GetPacket();
        {
            p.Write((byte)PacketKind.TileSquareExtras);
        }

        p.Write((ushort)tileX);
        p.Write((ushort)tileY);
        p.Write((byte)width);
        p.Write((byte)height);

        var framingPrevented = new BitArray(width * height);

        var i = 0;
        for (var x = tileX; x < tileX + width; x++)
        for (var y = tileY; y < tileY + height; y++)
        {
            var tile = Main.tile[x, y];
            {
                framingPrevented[i] = tile.Get<StasisRod.TileData>().FramingPrevented;
            }
            i++;
        }

        var framingPreventedBits = CompactBitArray.FromBitArray(framingPrevented);
        {
            framingPreventedBits.Serialize(p);
        }
    }
}
