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
        
        void IPreRenderedItem.PreRender(Texture2D sourceTexture)
        {
            Assets.Shaders.MetaActuationRodShader.Asset.Wait();

            var shader = Assets.Shaders.StasisRodShader.CreateHueShader();
            shader.Apply();

            Main.spriteBatch.Draw(sourceTexture, Vector2.Zero, Color.White);
        }
    }
}
