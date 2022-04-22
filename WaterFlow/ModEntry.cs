using HarmonyLib; // el diavolo nuevo
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Linq;
using xTile;
using xTile.ObjectModel;
using Colour = Microsoft.Xna.Framework.Color;

namespace WaterFlow
{
	public class Config
	{
		public string[] UpwardsLocations { get; set; } = new[]
		{
			"Beach",
			"Forest"
		};
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
		public const string MapProperty = "blueberry.water.flow";
		public const WaterFlow DefaultWaterFlow = WaterFlow.Down;

		private class ModState
		{
			public WaterFlow WaterFlow = WaterFlow.Up;
		}
		private static readonly PerScreen<ModState> State = new PerScreen<ModState>(() => new ModState());


		public static bool GameLocation_DrawWaterTile_Prefix(GameLocation __instance,
			SpriteBatch b, int x, int y, Colour color)
		{
			if (ModEntry.State.Value.WaterFlow is WaterFlow.Up)
				return true;

			if (ModEntry.State.Value.WaterFlow is WaterFlow.None)
				__instance.waterPosition = 0;
			const int sourceX = 0;
			const int sourceY = 2064;
			const int rotation = 0;
			const int origin = 0;
			const int scale = 1;
			const SpriteEffects effects = SpriteEffects.None;
			const float layerDepth = 0.56f;

			bool isTopTile = y == 0 || !__instance.waterTiles[x, y - 1];
			bool isBottomTile = y == __instance.Map.Layers[0].LayerHeight - 1 || !__instance.waterTiles[x, y + 1];

			Vector2 position = new Vector2(
					x: x * Game1.tileSize,
					y: (y + 1) * Game1.tileSize - (int)(Game1.tileSize - __instance.waterPosition) - 1);
			Rectangle sourceRectangle = new Rectangle(
					x: sourceX + __instance.waterAnimationIndex * Game1.tileSize,
					y: sourceY + (((x + y) % 2 != 0) ? ((!__instance.waterTileFlip) ? Game1.tileSize * 2 : 0) : (__instance.waterTileFlip ? Game1.tileSize * 2 : 0)) + (isBottomTile ? (int)__instance.waterPosition : 0),
					width: Game1.tileSize,
					height: Game1.tileSize + (isBottomTile ? ((int)(0 - __instance.waterPosition)) : 0));
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
						y: sourceY + (((x + (y + 1)) % 2 != 0) ? ((!__instance.waterTileFlip) ? Game1.tileSize * 2 : 0) : (__instance.waterTileFlip ? Game1.tileSize * 2 : 0)),
						width: Game1.tileSize,
						height: Game1.tileSize - (int)(Game1.tileSize - __instance.waterPosition) - 1),
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
					return;
				}
				object result = WaterFlow.Up;
				bool hasWater = e.NewLocation.waterTiles.waterTiles.Cast<WaterTiles.WaterTileData>().Any();
				bool isDisabledInConfig = config.UpwardsLocations.Any(s => s.Equals(e.NewLocation.Name));
				bool isEnabledInMap = e.NewLocation.Map.Properties.TryGetValue(key: ModEntry.MapProperty, out PropertyValue value)
					&& Enum.TryParse(enumType: typeof(WaterFlow), value: value, ignoreCase: true, out result)
					&& result is WaterFlow and not WaterFlow.Up;
				if ((!isDisabledInConfig && hasWater) || isEnabledInMap)
				{
					this.Monitor.LogOnce(message: $"{e.NewLocation.Name} will flow {result.ToString().ToLower()}.", level: LogLevel.Trace);
					ModEntry.State.Value.WaterFlow = isEnabledInMap ? (WaterFlow)result : ModEntry.DefaultWaterFlow;
				}
			};
		}
	}
}
