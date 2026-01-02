using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Rendering;
using Daybreak.Common.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
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
                    p.Write((byte)Networking.PacketKind.CustomTileManipulation);
                    p.Write((byte)Networking.TileManipulationKind.ToggleTileStasis);
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
        if (!tile.HasTile)
        {
            SetStasisOff(tileX, tileY);
            return false;
        }

        if (tile.Get<TileData>().FramingPrevented)
        {
            SetStasisOff(tileX, tileY);
            return false;
        }
        else
        {
            SetStasisOn(tileX, tileY);
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
        {
            tile.Get<TileData>().FramingPrevented = true;
        }

        Networking.SyncTileSquare(tileX, tileY);
    }

    public static void SetStasisOff(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY))
        {
            return;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        {
            tile.Get<TileData>().FramingPrevented = false;
        }

        Networking.SyncTileSquare(tileX, tileY);
    }

    [OnLoad]
    private static void ApplyHooks()
    {
        MonoModHooks.Add(
            typeof(TileLoader).GetMethod(nameof(TileLoader.TileFrame), BindingFlags.Public | BindingFlags.Static)!,
            TileFrame_CancelStaticTileFraming
        );

        /*
        MonoModHooks.Add(
            typeof(WallLoader).GetMethod(nameof(WallLoader.WallFrame), BindingFlags.Public | BindingFlags.Static)!,
            WallFrame_CancelStaticWallFraming
        );
        */

        On_Main.DrawWires += DrawWires_DrawStasisIcons;
        IL_WorldGen.KillTile += KillTile_RemoveStasis;
        On_WorldGen.ReplaceTIle_DoActualReplacement += DoActualReplacement_RemoveStasis;
    }

    private delegate bool Orig_TileFrame(
        int i,
        int j,
        int type,
        ref bool resetFrame,
        ref bool noBreak
    );

    private static bool TileFrame_CancelStaticTileFraming(
        Orig_TileFrame orig,
        int i,
        int j,
        int type,
        ref bool resetFrame,
        ref bool noBreak
    )
    {
        if (WorldGen.InWorld(i, j))
        {
            var tile = Framing.GetTileSafely(i, j);

            if (tile.Get<TileData>().FramingPrevented)
            {
                return false;
            }
        }

        return orig(
            i,
            j,
            type,
            ref resetFrame,
            ref noBreak
        );
    }

    // TODO: Can be turned into an IL edit to DoDraw which allows these to exist
    //       more in parallel.
    private static void DrawWires_DrawStasisIcons(
        On_Main.orig_DrawWires orig,
        Main self
    )
    {
        if (Main.LocalPlayer.HeldItem.type != ModContent.ItemType<FunctionalItem>())
        {
            orig(self);
            return;
        }

        var tileStartX = (int)(Main.screenPosition.X / 16) - 1;
        var tileEndX = (int)((Main.screenPosition.X + Main.screenWidth) / 16) + 2;
        var tileStartY = (int)(Main.screenPosition.Y / 16) - 1;
        var tileEndY = (int)((Main.screenPosition.Y + Main.screenHeight) / 16) + 5;

        if (tileStartX < 0)
        {
            tileStartX = 0;
        }

        if (tileEndX > Main.maxTilesX)
        {
            tileEndX = Main.maxTilesX;
        }

        if (tileStartY < 0)
        {
            tileStartY = 0;
        }

        if (tileEndY > Main.maxTilesY)
        {
            tileEndY = Main.maxTilesY;
        }

        var tileOffset = Main.GetScreenOverdrawOffset();
        for (var y = tileStartY + tileOffset.Y; y < tileEndY - tileOffset.Y; y++)
        for (var x = tileStartX + tileOffset.X; x < tileEndX - tileOffset.X; x++)
        {
            var tile = Framing.GetTileSafely(x, y);

            // Vanilla actuator logic for lighting...
            /*
            if (tile.Get<TileData>().FramingPrevented && (Lighting.Brightness(x, y) > 0f))
            {
                var lightColor = Lighting.GetColor(x, y);

                switch (num6) {
                    case 0:
                        color5 = Microsoft.Xna.Framework.Color.White;
                        break;
                    case 2:
                        color5 *= 0.5f;
                        break;
                    case 3:
                        color5 = Microsoft.Xna.Framework.Color.Transparent;
                        break;
                }
            }
            */

            if (tile.Get<TileData>().FramingPrevented)
            {
                Main.spriteBatch.Draw(
                    new DrawParameters(Assets.Images.StaticCross.Asset)
                    {
                        Position = new Vector2(x * 16 - (int)Main.screenPosition.X, y * 16 - (int)Main.screenPosition.Y),
                        Color = /*color5 * num*/ Color.White * 0.75f,
                    }
                );
            }
        }
    }

    private static void KillTile_RemoveStasis(ILContext il)
    {
        var c = new ILCursor(il)
        {
            Next = null,
        };

        var tileIdx = -1;
        c.GotoPrev(x => x.MatchCall<Tile>("get_" + nameof(Tile.type)));
        c.GotoPrev(MoveType.Before, x => x.MatchLdloca(out tileIdx));
        {
            Debug.Assert(tileIdx != -1);
        }

        c.MoveAfterLabels();

        c.EmitLdloca(tileIdx);
        c.EmitDelegate(
            (ref Tile t) =>
            {
                t.Get<TileData>().FramingPrevented = false;
            }
        );
    }

    private static void DoActualReplacement_RemoveStasis(
        On_WorldGen.orig_ReplaceTIle_DoActualReplacement orig,
        ushort targetType,
        int targetStyle,
        int topLeftX,
        int topLeftY,
        Tile t
    )
    {
        t.Get<TileData>().FramingPrevented = false;

        orig(
            targetType,
            targetStyle,
            topLeftX,
            topLeftY,
            t
        );
    }
}
