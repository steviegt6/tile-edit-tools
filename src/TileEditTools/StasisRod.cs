using System;
using System.Runtime.InteropServices;
using Daybreak.Common.Features.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TileEditTools;

public static class StasisRod
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct TileData : ITileData
    {
        public bool FramingPrevented { get; set; }

        public void Clear()
        {
            FramingPrevented = false;
        }
    }

    private sealed class TileDataHandler : ModSystem
    {
        public override void SaveWorldData(TagCompound tag)
        {
            base.SaveWorldData(tag);

            var tileData = Main.tile.GetData<TileData>().AsSpan();
            var byteData = MemoryMarshal.Cast<TileData, byte>(tileData);
            tag[nameof(TileData)] = byteData.ToArray();
        }

        public override void LoadWorldData(TagCompound tag)
        {
            base.LoadWorldData(tag);

            var byteData = tag.GetByteArray(nameof(TileData));
            var loadedTileData = MemoryMarshal.Cast<byte, TileData>(byteData);
            var tileData = Main.tile.GetData<TileData>().AsSpan();

            if (loadedTileData.Length <= tileData.Length)
            {
                loadedTileData.CopyTo(tileData);
            }
        }
    }

    public sealed class FunctionalItem : ModItem, IPreRenderedItem
    {
        public override string Texture => Assets.Images.Items.StasisRod_Item.KEY;

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            ItemID.Sets.AlsoABuildingItem[Type] = true;
        }

        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.CloneDefaults(ItemID.ActuationRod);
            Item.useTime = Item.useAnimation = 15;
            Item.value = 0;
            // Item.mech = false;
        }

        public override bool? UseItem(Player player)
        {
            if (player.itemAnimation <= 0 || !player.ItemTimeIsZero || !player.controlUseItem)
            {
                return null;
            }

            var tileX = Player.tileTargetX;
            var tileY = Player.tileTargetY;

            if (!WorldGen.InWorld(tileX, tileY))
            {
                return false;
            }

            var toggled = ToggleStasis(tileX, tileY);
            if (!toggled.HasValue)
            {
                return false; // ?!
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                var p = ModContent.GetInstance<ModImpl>().GetPacket();
                {
                    p.Write((byte)PacketKind.CustomTileManipulation);
                    p.Write((byte)TileManipulationKind.ToggleTileStasis);
                    p.Write((ushort)tileX);
                    p.Write((ushort)tileY);
                }
                p.Send(toClient: -1, ignoreClient: -1);
            }

            return true;
        }

        void IPreRenderedItem.PreRender(Texture2D sourceTexture)
        {
            Assets.Shaders.MetaActuationRodShader.Asset.Wait();

            var shader = Assets.Shaders.StasisRodShader.CreateHueShader();
            shader.Apply();

            Main.spriteBatch.Draw(sourceTexture, Vector2.Zero, Color.White);
        }
    }

    public static bool? ToggleStasis(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY, fluff: 3))
        {
            return null;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        if (tile.Get<TileData>().FramingPrevented)
        {
            SetStasisOff(tile);
            return false;
        }
        else
        {
            SetStasisOn(tile);
            return true;
        }
    }

    public static void SetStasisOn(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY))
        {
            return;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        SetStasisOn(tile);
    }

    public static void SetStasisOn(Tile tile)
    {
        tile.Get<TileData>().FramingPrevented = true;
    }

    public static void SetStasisOff(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY))
        {
            return;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        SetStasisOff(tile);
    }

    public static void SetStasisOff(Tile tile)
    {
        tile.Get<TileData>().FramingPrevented = false;
    }
}
