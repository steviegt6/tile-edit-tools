using Daybreak.Common.Features.Hooks;
using Daybreak.Common.Features.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TileEditTools;

internal sealed class MetaActuationRodItem : ModItem, IPreRenderedItem
{
    public override string Texture => Assets.Images.MetaActuationRod_Item.KEY;

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        ItemID.Sets.AlsoABuildingItem[Type] = true;
    }

    [OnLoad]
    private static void ApplyHooks()
    {
        On_Wiring.Actuate += (orig, x, y) =>
        {
            if (Main.player[Wiring.CurrentUser].HeldItem.type != ModContent.ItemType<MetaActuationRodItem>())
            {
                return orig(x, y);
            }

            Wiring.ActuateForced(x, y);
            return true;
        };

        On_Wiring.DeActive += (orig, i, j) =>
        {
            if (Main.player[Wiring.CurrentUser].HeldItem.type != ModContent.ItemType<MetaActuationRodItem>())
            {
                orig(i, j);
                return;
            }

            var tile = Main.tile[i, j];
            if (!tile.HasTile)
            {
                return;
            }

            tile.IsActuated = true;
            WorldGen.SquareTileFrame(i, j, resetFrame: false);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                NetMessage.SendTileSquare(-1, i, j);
            }
        };
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.CloneDefaults(ItemID.ActuationRod);
        Item.useTime = Item.useAnimation = 15;
        Item.value = 0;
        Item.mech = false;
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

        Wiring.ActuateForced(tileX, tileY);
        NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 19, tileX, tileY);

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
