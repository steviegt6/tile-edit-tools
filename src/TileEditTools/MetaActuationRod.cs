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
            
            ToggleActuate(tileX, tileY);

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 19, tileX, tileY);
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
            SetActuateOff(tile);
            return false;
        }
        else
        {
            SetActuateOn(tile);
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
        SetActuateOn(tile);
    }

    public static void SetActuateOn(Tile tile)
    {
        tile.IsActuated = true;
    }

    public static void SetActuateOff(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY))
        {
            return;
        }

        var tile = Framing.GetTileSafely(tileX, tileY);
        SetActuateOff(tile);
    }

    public static void SetActuateOff(Tile tile)
    {
        tile.IsActuated = false;
    }
}
