﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using MidiBard.Managers;
using MidiBard.Managers.Agents;
using playlibnamespace;

namespace MidiBard.Control.CharacterControl
{
	static class SwitchInstrument
	{
		public static bool SwitchingInstrument { get; set; }

		public static void SwitchToContinue(uint instrumentId, int timeOut = 3000)
		{
			Task.Run(async () =>
			{
				var isPlaying = MidiBard.IsPlaying;
				MidiBard.CurrentPlayback?.Stop();
				await SwitchTo(instrumentId);
				if (isPlaying) MidiBard.CurrentPlayback?.Start();
			});
		}

		public static async Task SwitchTo(uint instrumentId, int timeOut = 3000)
		{
			if (MidiBard.guitarGroup.Contains(MidiBard.CurrentInstrument))
			{
				if (MidiBard.guitarGroup.Contains((byte)instrumentId))
				{
					playlib.GuitarSwitchTone((int)instrumentId - MidiBard.guitarGroup[0]);
					return;
				}
			}

			if (MidiBard.CurrentInstrument == instrumentId) return;

			SwitchingInstrument = true;
			var sw = Stopwatch.StartNew();
			try
			{
				if (MidiBard.CurrentInstrument != 0)
				{
					PerformActions.DoPerformAction(0);
					await Util.Coroutine.WaitUntil(() => MidiBard.CurrentInstrument == 0, timeOut);
				}

				PerformActions.DoPerformAction(instrumentId);
				await Util.Coroutine.WaitUntil(() => MidiBard.CurrentInstrument == instrumentId, timeOut);
				await Task.Delay(200);
				PluginLog.Debug($"instrument switching succeed in {sw.Elapsed.TotalMilliseconds} ms");
				DalamudApi.api.PluginInterface.UiBuilder.AddNotification($"Switched to {MidiBard.InstrumentStrings[instrumentId]}", "MidiBard", NotificationType.Success, 5000);
			}
			catch (Exception e)
			{
				PluginLog.Error(e, $"instrument switching failed in {sw.Elapsed.TotalMilliseconds} ms");
			}
			finally
			{
				SwitchingInstrument = false;
			}
		}

		static Regex regex = new Regex(@"^#(.*?)([-|+][0-9]+)?#", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		static void ParseSongName(string inputString, out uint? instrumentId, out int? transpose)
		{
			var match = regex.Match(inputString);
			if (match.Success)
			{
				var capturedInstrumentString = match.Groups[1].Value;
				var capturedTransposeString = match.Groups[2].Value;

				PluginLog.Debug($"input: \"{inputString}\", instrumentString: {capturedInstrumentString}, transposeString: {capturedTransposeString}");
				transpose = int.TryParse(capturedTransposeString, out var t) ? t : null;
				instrumentId = TryParseInstrumentName(capturedInstrumentString, out var id) ? id : null;
				return;
			}

			instrumentId = null;
			transpose = null;
		}

		public static bool TryParseInstrumentName(string capturedInstrumentString, out uint instrumentId)
		{
			Perform equal = MidiBard.InstrumentSheet.FirstOrDefault(i =>
				i?.Instrument?.RawString.Equals(capturedInstrumentString, StringComparison.InvariantCultureIgnoreCase) == true);
			Perform contains = MidiBard.InstrumentSheet.FirstOrDefault(i =>
				i?.Instrument?.RawString?.ContainsIgnoreCase(capturedInstrumentString) == true);
			Perform gmName = MidiBard.InstrumentSheet.FirstOrDefault(i =>
				i?.Name?.RawString?.ContainsIgnoreCase(capturedInstrumentString) == true);

			var rowId = (equal ?? contains ?? gmName)?.RowId;
			PluginLog.Debug($"equal: {equal?.Instrument?.RawString}, contains: {contains?.Instrument?.RawString}, gmName: {gmName?.Name?.RawString} finalId: {rowId}");
			if (rowId is null)
			{
				instrumentId = 0;
				return false;
			}
			else
			{
				instrumentId = rowId.Value;
				return true;
			}
		}

		internal static async Task WaitSwitchInstrumentForSong(string songName)
		{
			var config = MidiBard.config;

			if (config.autoTransposeBySongName && config.EnableTransposePerTrack)
			{
				var currentTracks = MidiBard.CurrentTracks;
				foreach (var (_, trackInfo) in currentTracks)
				{
					var transposePerTrack = trackInfo.TransposeFromTrackName;
					if (transposePerTrack != 0)
					{
						PluginLog.Information($"applying transpose {transposePerTrack:+#;-#;0} for track [{trackInfo.Index + 1}]{trackInfo.GetTrackName()}");
					}
					config.TransposePerTrack[trackInfo.Index] = transposePerTrack;
				}
			}

			if (config.autoSwitchInstrumentByTrackName)
			{
				var firstEnabledTrack = MidiBard.CurrentTracks.Select(i => i.trackInfo).FirstOrDefault(i => i.IsEnabled);
				var idFromTrackName = firstEnabledTrack?.InstrumentIDFromTrackName;
				if (idFromTrackName != null)
				{
					await SwitchTo((uint)idFromTrackName);
				}

				return;
			}


			ParseSongName(songName, out var idFromSongName, out var transposeGlobal);

			if (config.autoTransposeBySongName)
			{
				if (transposeGlobal != null)
				{
					config.TransposeGlobal = (int)transposeGlobal;
				}
				else
				{
					config.TransposeGlobal = 0;
				}
			}

			if (config.autoSwitchInstrumentBySongName)
			{
				if (idFromSongName != null)
				{
					await SwitchTo((uint)idFromSongName);
				}
			}
		}
	}
}
