using Daybreak.Common.Features.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TileEditTools;

public static class MetaActuationRod
{
    [LegacyName("MetaActuationRodItem")]
    public sealed class FunctionalItem : ModItem, IPreRenderedItem
    {
        public override string Texture => Assets.Images.Items.MetaActuationRod_Item.KEY;

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

            var toggled = ToggleActuate(tileX, tileY);
            if (!toggled.HasValue)
            {
                return false; // ?!
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                var p = ModContent.GetInstance<ModImpl>().GetPacket();
                {
                    p.Write((byte)Networking.PacketKind.CustomTileManipulation);
                    p.Write((byte)Networking.TileManipulationKind.ForceToggleActuation);
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

            var shader = Assets.Shaders.MetaActuationRodShader.CreateHueShader();
            shader.Apply();

            Main.spriteBatch.Draw(sourceTexture, Vector2.Zero, Color.White);
        }
    }

    public static bool? ToggleActuate(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY, fluff: 3))
        {
            return null;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        if (tile.IsActuated)
        {
            SetActuateOff(tileX, tileY);
            return false;
        }
        else
        {
            SetActuateOn(tileX, tileY);
            return true;
        }
    }

    public static void SetActuateOn(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY))
        {
            return;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        {
            tile.IsActuated = true;
        }

        Networking.SyncTileSquare(tileX, tileY);
    }

    public static void SetActuateOff(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY))
        {
            return;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        {
            tile.IsActuated = false;
        }

        Networking.SyncTileSquare(tileX, tileY);
    }
}
