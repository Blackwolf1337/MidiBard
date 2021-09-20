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
				DalamudApi.DalamudApi.PluginInterface.UiBuilder.AddNotification($"Switched to {MidiBard.InstrumentStrings[instrumentId]}", "MidiBard", NotificationType.Success, 5000);
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
		static void ParseTrackName(string inputString, out uint? instrumentId, out int? transpose)
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

		internal static async Task WaitSwitchInstrumentForSong(string trackName)
		{
			ParseTrackName(trackName, out var instrumentId, out var transpose);

			if (MidiBard.config.autoSwitchInstrumentByFileName && instrumentId != null)
			{
				await SwitchTo((uint)instrumentId);
			}

			if (MidiBard.config.autoTransposeByFileName)
			{
				if (transpose != null)
				{
					MidiBard.config.TransposeGlobal = (int)transpose;
				}
				else
				{
					MidiBard.config.TransposeGlobal = 0;
				}
			}
		}
	}
}
