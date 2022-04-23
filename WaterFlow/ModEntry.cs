﻿using HarmonyLib; // el diavolo nuevo
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile;
using xTile.Dimensions;
using xTile.ObjectModel;
using Colour = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace WaterFlow
{
	public class Config
	{
		public string[] UpwardsLocations { get; set; } = new[]
		{
			"Beach",
			"Forest"
		};
		public bool VerboseLogging { get; set; } = true;
	}

	public class ModEntry : Mod
	{
		public enum WaterFlow
		{
			None = -1,
			Up = 0,
			Right = 1,
			Down = 2,
			Left = 3
		}
		public const string MapPropertyGlobal = "blueberry.water.flow.global";
		public const string MapPropertyLocal = "blueberry.water.flow.local";
		public const WaterFlow DefaultWaterFlow = WaterFlow.Down;

		private class ModState
		{
			public WaterFlow WaterFlow = WaterFlow.Up;
			public List<(WaterFlow flow, Rectangle area)> Areas = new List<(WaterFlow flow, Rectangle area)>();
			public Dictionary<string, bool> VisitedLocations = new Dictionary<string, bool>();
		}
		private static readonly PerScreen<ModState> State = new PerScreen<ModState>(() => new ModState());


		public static bool GameLocation_DrawWaterTile_Prefix(GameLocation __instance,
			SpriteBatch b, int x, int y, Colour color)
		{
			WaterFlow waterFlow = ModEntry.State.Value.WaterFlow;

			if (ModEntry.State.Value.Areas.FirstOrDefault(pair => pair.area.Contains(x, y)) is var a && a != default)
				waterFlow = a.flow;
			
			if (waterFlow is WaterFlow.Up)
				return true;

			if (waterFlow is WaterFlow.None)
				__instance.waterPosition = 0;

			const int sourceX = 0;
			const int sourceY = 2064;
			const int rotation = 0;
			const int origin = 0;
			const int scale = 1;
			const SpriteEffects effects = SpriteEffects.None;
			const float layerDepth = 0.56f;

			bool isLeftOrRight = waterFlow is WaterFlow.Left or WaterFlow.Right;
			bool isUpOrLeft = waterFlow is WaterFlow.Up or WaterFlow.Left;

			int forLR = isLeftOrRight ? 1 : 0;
			int forUD = 1 - forLR;
			int forUL = isUpOrLeft ? 1 : 0;
			int forDR = 1 - forUL;

			const int n = 1;
			int start = isLeftOrRight ? x : y;
			int span = isLeftOrRight ? __instance.Map.Layers[0].LayerWidth : __instance.Map.Layers[0].LayerHeight;

			bool isTopTile = start == 0 || !__instance.waterTiles[x - (n * forLR), y - (n * forUD)];
			bool isBottomTile = start == span - 1 || !__instance.waterTiles[x + (n * forLR), y + (n * forUD)];

			int tileCrop = (int)(Game1.tileSize - __instance.waterPosition) + 1;
			int tileSize = isBottomTile ? ((int)(0 - __instance.waterPosition)) : 0;

			Vector2 position = new Vector2(
					x: (x + (n * forLR)) * Game1.tileSize - (tileCrop * forLR),
					y: (y + (n * forUD)) * Game1.tileSize - (tileCrop * forUD));
			Rectangle sourceRectangle = new Rectangle(
					x: sourceX + __instance.waterAnimationIndex * Game1.tileSize,
					y: sourceY + (((x + y) % 2 != 0) ? ((!__instance.waterTileFlip) ? Game1.tileSize * 2 : 0) : (__instance.waterTileFlip ? Game1.tileSize * 2 : 0)) + (isBottomTile ? (int)__instance.waterPosition : 0),
					width: Game1.tileSize + (tileSize * forLR),
					height: Game1.tileSize + (tileSize * forUD));
			b.Draw(
				texture: Game1.mouseCursors,
				position: Game1.GlobalToLocal(
					viewport: Game1.viewport,
					globalPosition: position),
				sourceRectangle: sourceRectangle,
				color: color,
				rotation: rotation,
				origin: new Vector2(origin),
				scale: scale,
				effects: effects,
				layerDepth: layerDepth);

			if (isTopTile)
			{
				b.Draw(
					texture: Game1.mouseCursors,
					position: Game1.GlobalToLocal(
						viewport: Game1.viewport,
						globalPosition: new Vector2(
							x: x * Game1.tileSize,
							y: y * Game1.tileSize)),
					sourceRectangle: new Rectangle(
						x: sourceX + __instance.waterAnimationIndex * Game1.tileSize,
						y: sourceY + (((x + y + 1) % 2 != 0) ? ((!__instance.waterTileFlip) ? Game1.tileSize * 2 : 0) : (__instance.waterTileFlip ? Game1.tileSize * 2 : 0)),
						width: Game1.tileSize - (isLeftOrRight ? (int)(Game1.tileSize - __instance.waterPosition) + 1 : 0),
						height: Game1.tileSize - (isLeftOrRight ? 0 : (int)(Game1.tileSize - __instance.waterPosition) + 1)),
					color: color,
					rotation: rotation,
					origin: new Vector2(origin),
					scale: scale,
					effects: effects,
					layerDepth: layerDepth);
			}

			return false;
		}

		public override void Entry(IModHelper helper)
		{
			Config config = helper.ReadConfig<Config>();

			bool parseLocalAreaValues(PropertyValue localValue, Size mapSize)
			{
				int i = 0;
				string[] fields = null;
				try
				{
					ModEntry.State.Value.Areas.Clear();
					fields = localValue.ToString().Trim().Split();
					for (; i < fields.Length; i += 5)
					{
						if (Enum.TryParse(enumType: typeof(WaterFlow), value: fields[i], ignoreCase: true, out object result)
							&& result is WaterFlow flow)
						{
							int[] values = fields.Skip(i + 1).Take(4).ToList().ConvertAll(int.Parse).ToArray();
							if (values[2] < 1)
								values[2] = mapSize.Width;
							if (values[3] < 1)
								values[3] = mapSize.Height;
							values[0] = Math.Min(mapSize.Width, Math.Max(0, values[0]));
							values[1] = Math.Min(mapSize.Height, Math.Max(0, values[1]));
							values[2] = Math.Min(mapSize.Width - values[0], values[2]);
							values[3] = Math.Min(mapSize.Height - values[1], values[3]);
							if (values[2] > 0 && values[3] > 0)
							{
								Rectangle area = new Rectangle(values[0], values[1], values[2], values[3]);
								ModEntry.State.Value.Areas.Add((flow: flow, area: area));
								if (config.VerboseLogging)
								{
									this.Monitor.Log(
										message: $"Parsed '{localValue}' to {nameof(WaterFlow)} {flow}, {nameof(Rectangle)} {area}.",
										level: LogLevel.Debug);
								}
								return true;
							}
							else
							{
								this.Monitor.Log(
									message: $"Invalid parsed area ({nameof(Rectangle)}): {string.Join(' ', values)}",
									level: LogLevel.Error);
							}
						}
						else
						{
							this.Monitor.Log(
								message: $"Invalid parsed flow ({nameof(WaterFlow)}): {fields[i + 0]}",
								level: LogLevel.Error);
						}
					}
				}
				catch (Exception e)
				{
					this.Monitor.Log(
						message: $"Error while reading {nameof(WaterFlow)} entry {i} of {fields?.Length / 5 ?? 0}:{Environment.NewLine}{e}",
						level: LogLevel.Error);
				}
				this.Monitor.Log(
					message: $"Failed to read local {nameof(WaterFlow)} entry:{Environment.NewLine}'{localValue}'",
					level: LogLevel.Error);
				return false;
			}

			Harmony harmony = new Harmony(id: this.ModManifest.UniqueID);
			harmony.Patch(
				original: AccessTools.Method(type: typeof(GameLocation), name: nameof(GameLocation.drawWaterTile),
				parameters: new Type[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(Colour) }),
				prefix: new HarmonyMethod(methodType: typeof(ModEntry), methodName: nameof(ModEntry.GameLocation_DrawWaterTile_Prefix)));
			helper.Events.Player.Warped += (object sender, WarpedEventArgs e) =>
			{
				if (e.NewLocation is null)
				{
					ModEntry.State.Value.WaterFlow = WaterFlow.Up;
					ModEntry.State.Value.Areas.Clear();
					return;
				}
				object result = WaterFlow.Up;
				bool hasWater = e.NewLocation.waterTiles.waterTiles.Cast<WaterTiles.WaterTileData>().Any();
				bool isDisabledInConfig = config.UpwardsLocations.Any(s => s.Equals(e.NewLocation.Name));
				bool isEnabledLocalInMap = e.NewLocation.Map.Properties.TryGetValue(key: ModEntry.MapPropertyLocal, out PropertyValue localValue)
					&& parseLocalAreaValues(localValue: localValue, mapSize: e.NewLocation.Map.Layers[0].LayerSize);
				bool isEnabledGlobalInMap = e.NewLocation.Map.Properties.TryGetValue(key: ModEntry.MapPropertyGlobal, out PropertyValue value)
					&& Enum.TryParse(enumType: typeof(WaterFlow), value: value, ignoreCase: true, out result)
					&& result is WaterFlow and not WaterFlow.Up;
				if ((!isDisabledInConfig && hasWater) || isEnabledLocalInMap || isEnabledGlobalInMap)
				{
					if (config.VerboseLogging && !ModEntry.State.Value.VisitedLocations.ContainsKey(key: e.NewLocation.Name))
					{
						this.Monitor.Log(
							message: $"{e.NewLocation.Name} will flow {result.ToString().ToLower()}.",
							level: LogLevel.Debug);
						foreach ((WaterFlow flow, Rectangle area) in ModEntry.State.Value.Areas)
						{
							this.Monitor.Log(
								message: $"({flow}: {area})",
								level: LogLevel.Debug);
						}
						ModEntry.State.Value.VisitedLocations[e.NewLocation.Name] = true;
					}
					ModEntry.State.Value.WaterFlow = isEnabledGlobalInMap ? (WaterFlow)result : ModEntry.DefaultWaterFlow;
				}
			};
		}
	}
}
