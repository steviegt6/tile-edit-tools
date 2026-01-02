using System;
using System.Reflection;
using Daybreak.Common.Features.Hooks;
using Daybreak.Common.UI;
using DragonLens.Content.GUI;
using DragonLens.Content.Tools.Gameplay;
using GoldMeridian.CodeAnalysis;
using Microsoft.Xna.Framework;
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
    [ExtensionDataFor<PaintWindow>]
    public sealed class PaintWindowButtons
    {
        public required ToggleButton IgnoreWallsButton { get; set; }

        public bool IgnoringWalls { get; set; }
    }

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

        MonoModHooks.Add(
            typeof(PaintWindow).GetMethod(nameof(PaintWindow.SafeOnInitialize), BindingFlags.Public | BindingFlags.Instance)!,
            SafeOnInitialize_AddNewButtons
        );

        MonoModHooks.Add(
            typeof(PaintWindow).GetMethod(nameof(PaintWindow.AdjustPositions), BindingFlags.Public | BindingFlags.Instance)!,
            AdjustPositions_AdjustNewButtons
        );
    }

    private static void PaintWindow_SafeClick_AddSpecialTileData(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(MoveType.Before, x => x.MatchCall(typeof(Saver), nameof(Saver.SaveToStructureData)));
        c.Remove();
        c.EmitLdarg0();
        c.EmitDelegate(
            (
                int x,
                int y,
                int width,
                int height,
                PaintWindow self
            ) =>
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

                if (self.Buttons is not { IgnoringWalls: true }
                 || data.dataEntries["Terraria/WallTypeData"] is not TileDataEntry<WallTypeData> wallData)
                {
                    return data;
                }

                for (var i = 0; i < x; i++)
                {
                    data.slowColumns[i] = true;
                }

                data.moddedWallTable[ushort.MaxValue] = ushort.MaxValue;

                for (var i = 0; i < wallData.rawData.Length; i++)
                {
                    wallData.rawData[i].Type = ushort.MaxValue;
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

    private static void SafeOnInitialize_AddNewButtons(
        Action<PaintWindow> orig,
        PaintWindow self
    )
    {
        orig(self);

        var ignoreWallsButton = new ToggleButton(
            Assets.Images.IgnoresWallsToggle.KEY,
            () => self.Buttons?.IgnoringWalls ?? false,
            Mods.TileEditTools.UI.IgnoresWallsToggle.GetTextValue()
        );
        {
            ignoreWallsButton.OnLeftClick += (_, _) =>
            {
                self.Buttons?.IgnoringWalls = !(self.Buttons?.IgnoringWalls ?? false);
            };
        }
        self.Append(ignoreWallsButton);

        self.Buttons = new PaintWindowButtons
        {
            IgnoreWallsButton = ignoreWallsButton,
        };
    }

    private static void AdjustPositions_AdjustNewButtons(
        Action<PaintWindow, Vector2> orig,
        PaintWindow self,
        Vector2 newPos
    )
    {
        orig(self, newPos);

        if (self.Buttons is not { } buttons)
        {
            return;
        }

        var ignoreWallsButton = buttons.IgnoreWallsButton;
        {
            /*
            ignoreWallsButton.Left.Set(newPos.X + 344, 0);
            ignoreWallsButton.Top.Set(newPos.X + 80, 0);
            */

            ignoreWallsButton.Left = self.sampleButton.Left;
            ignoreWallsButton.Top = self.sampleButton.Top;
            ignoreWallsButton.Top.Pixels += ignoreWallsButton.Dimensions.Height + 10f;
        }
    }
}

[ExtendsFromMod("DragonLens")]
partial class PaintWindow_Buttons_CwtExtensions;
