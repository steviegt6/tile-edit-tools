using System;
using System.Collections.Generic;
using System.Reflection;
using Daybreak.Common.Features.Hooks;
using DragonLens.Content.Tools.Gameplay;
using MonoMod.Cil;
using StructureHelper;
using StructureHelper.API;
using StructureHelper.Helpers;
using StructureHelper.Models;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Generator = StructureHelper.API.Generator;

namespace TileEditTools;

[ExtendsFromMod("DragonLens")]
internal static class DragonLensCompat
{
    [OnLoad]
    private static void ApplyHooks()
    {
        MonoModHooks.Modify(
            typeof(PaintWindow).GetMethod(nameof(PaintWindow.SafeClick), BindingFlags.Public | BindingFlags.Instance)!,
            PaintWindow_SafeClick_AddSpecialTileData
        );

        MonoModHooks.Modify(
            typeof(PaintWindow).GetMethod(nameof(PaintWindow.DraggableUdpate), BindingFlags.Public | BindingFlags.Instance)!,
            PaintWindow_DraggableUpdate_UseSpecialTileData
        );
    }

    private static void PaintWindow_SafeClick_AddSpecialTileData(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(MoveType.Before, x => x.MatchCall(typeof(Saver), nameof(Saver.SaveToStructureData)));
        c.Remove();
        c.EmitDelegate(
            (int x, int y, int width, int height) =>
            {
                var data = Saver.SaveToStructureData(x, y, width, height);
                {
                    AddCustomDataFromWorld<StasisRod.TileData>(
                        data,
                        x,
                        y,
                        width,
                        height
                    );
                }

                return data;
            }
        );
    }

    private static void PaintWindow_DraggableUpdate_UseSpecialTileData(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(MoveType.Before, x => x.MatchCall(typeof(Generator), nameof(Generator.GenerateFromData)));
        c.Remove();
        c.EmitDelegate(
            (
                StructureData data,
                Point16 placeTarget,
                bool ignoreNull,
                GenFlags flags
            ) =>
            {
                GenerateFromData(
                    data,
                    placeTarget,
                    ignoreNull,
                    flags
                );
            }
        );
    }

    private static void AddCustomDataFromWorld<TTileData>(
        StructureData data,
        int x,
        int y,
        int w,
        int h
    ) where TTileData : unmanaged, ITileData
    {
        for (var scanX = 0; scanX < w; scanX++)
        {
            data.ImportDataColumn<TTileData>(x + scanX, y, scanX, ModContent.GetInstance<ModImpl>());
        }
    }

    private static void GenerateFromData(
        StructureData data,
        Point16 pos,
        bool ignoreNull = false,
        GenFlags flags = GenFlags.None
    )
    {
        if (!Generator.IsInBounds(data, pos))
        {
            throw new ArgumentException(ErrorHelper.GenerateErrorMessage($"Attempted to generate a structure out of bounds! {pos} is not a valid position for the structure. Mods are responsible for bounds-checking their own structures. You can fetch dimension data using GetDimensions or GetMultistructureDimensions.", null));
        }

        for (var column = 0; column < data.width; ++column)
        {
            if (!data.slowColumns[column] | ignoreNull)
            {
                data.ExportDataColumn<TileTypeData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumn<WallTypeData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumn<LiquidData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumn<TileWallBrightnessInvisibilityData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumn<TileWallWireStateData>(pos.X + column, pos.Y, column, null);

                data.ExportDataColumn<StasisRod.TileData>(pos.X + column, pos.Y, column, ModContent.GetInstance<ModImpl>());
            }
            else
            {
                data.ExportDataColumnSlow<TileTypeData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumnSlow<WallTypeData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumnSlow<LiquidData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumnSlow<TileWallBrightnessInvisibilityData>(pos.X + column, pos.Y, column, null);
                data.ExportDataColumnSlow<TileWallWireStateData>(pos.X + column, pos.Y, column, null);

                data.ExportDataColumnSlow<StasisRod.TileData>(pos.X + column, pos.Y, column, ModContent.GetInstance<ModImpl>());
            }
        }

        if (ignoreNull)
        {
            for (var x = 0; x < data.width; ++x)
            for (var y = 0; y < data.height; ++y)
            {
                var tile = Main.tile[pos.X + x, pos.Y + y];
                if (tile.TileType == ushort.MaxValue)
                {
                    tile.TileType = StructureHelper.StructureHelper.NullTileID;
                }

                if (tile.WallType == ushort.MaxValue)
                {
                    tile.WallType = StructureHelper.StructureHelper.NullWallID;
                }
            }
        }

        if (data.containsNbt)
        {
            foreach (var structureNbtEntry in data.nbtData)
            {
                structureNbtEntry.OnGenerate(pos + new Point16(structureNbtEntry.x, structureNbtEntry.y), ignoreNull, flags);
            }
        }

        if (WorldGen.generatingWorld)
        {
            return;
        }

        for (var i = 0; i < data.width; ++i)
        {
            WorldGen.TileFrame(pos.X + i, pos.Y);
            WorldGen.TileFrame(pos.X + i, pos.Y + data.height);
            WorldGen.SquareWallFrame(pos.X + i, pos.Y);
            WorldGen.SquareWallFrame(pos.X + i, pos.Y + data.height);
        }

        for (var i = 0; i < data.height; ++i)
        {
            WorldGen.TileFrame(pos.X, pos.Y + i);
            WorldGen.TileFrame(pos.X + data.width, pos.Y + i);
            WorldGen.SquareWallFrame(pos.X, pos.Y + i);
            WorldGen.SquareWallFrame(pos.X + data.width, pos.Y + i);
        }

        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            return;
        }

        NetMessage.SendTileSquare(-1, pos.X, pos.Y, data.width, data.height);
    }
}
