using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Terraria;
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
}
