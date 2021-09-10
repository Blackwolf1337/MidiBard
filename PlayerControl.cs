﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Melanchall.DryWetMidi.Interaction;
using static MidiBard.Plugin;

namespace MidiBard
{
  internal static class PlayerControl
  {
    internal static void Play()
    {
      if (currentPlayback == null)
      {
				if (!PlaylistManager.Filelist.Any()) PluginLog.Information("empty playlist");
        try
        {
          var valueTuple = PlaylistManager.Filelist[PlaylistManager.CurrentPlaying];
          currentPlayback = valueTuple.GetFilePlayback();
        }
        catch (Exception e)
        {
          try
          {
            currentPlayback = PlaylistManager.Filelist[0].GetFilePlayback();
            PlaylistManager.CurrentPlaying = 0;
          }
          catch (Exception exception)
          {
            PluginLog.Error(exception, "error when getting playback.");
          }
        }
      }

      try
      {
        if (currentPlayback?.GetCurrentTime<MidiTimeSpan>() == currentPlayback?.GetDuration<MidiTimeSpan>())
        {
          currentPlayback?.MoveToStart();
          config.playDeltaTime = 0;
        }

        currentPlayback?.Start();
      }
      catch (Exception e)
      {
        PluginLog.Error(e,
          "error when try to start playing, maybe the playback has been disopsed?");
      }
    }

    internal static void Pause()
    {
      currentPlayback?.Stop();
    }

    internal static void Stop()
    {
      try
      {
        currentPlayback?.Stop();
        currentPlayback?.MoveToTime(new MidiTimeSpan(0));
        config.playDeltaTime = 0;
      }
      catch (Exception e)
      {
        PluginLog.Information("Already stopped!");
      }
      finally
      {
        currentPlayback = null;
      }
    }

    internal static void Next()
    {
      config.playDeltaTime = 0;
      if (currentPlayback != null)
      {
        try
        {
          var wasplaying = IsPlaying;
          currentPlayback?.Dispose();
          currentPlayback = null;

          switch ((PlayMode)config.PlayMode)
          {
            case PlayMode.Single:
            case PlayMode.ListOrdered:
            case PlayMode.SingleRepeat:
              currentPlayback = PlaylistManager.Filelist[PlaylistManager.CurrentPlaying + 1].GetFilePlayback();
              PlaylistManager.CurrentPlaying += 1;
              break;

            case PlayMode.ListRepeat:
              var next = PlaylistManager.CurrentPlaying + 1;
              next %= PlaylistManager.Filelist.Count;
              currentPlayback = PlaylistManager.Filelist[next].GetFilePlayback();
              PlaylistManager.CurrentPlaying = next;
              break;

            case PlayMode.Random:
              var r = new Random();
              int nexttrack;
              do
              {
                nexttrack = r.Next(0, PlaylistManager.Filelist.Count);
              } while (nexttrack == PlaylistManager.CurrentPlaying);

              currentPlayback = PlaylistManager.Filelist[nexttrack].GetFilePlayback();
              PlaylistManager.CurrentPlaying = nexttrack;
              break;
          }

          if (wasplaying)
          {
            try
            {
              // ReSharper disable once PossibleNullReferenceException
              currentPlayback.Start();
            }
            catch (Exception e)
            {
              PluginLog.Error(e, "error when try playing next song.");
            }
          }
          Task.Run(SwitchInstrument.WaitSwitchInstrument);
        }
        catch (Exception e)
        {
          currentPlayback = null;
          PlaylistManager.CurrentPlaying = -1;
        }
      }
      else
      {
        PlaylistManager.CurrentPlaying += 1;
      }
    }

    internal static void Last()
    {
      config.playDeltaTime = 0;
      if (currentPlayback != null)
      {
        try
        {
          var wasplaying = IsPlaying;
          currentPlayback?.Dispose();
          currentPlayback = null;

          switch ((PlayMode)config.PlayMode)
          {
            case PlayMode.Single:
            case PlayMode.ListOrdered:
            case PlayMode.SingleRepeat:
              currentPlayback = PlaylistManager.Filelist[PlaylistManager.CurrentPlaying - 1].GetFilePlayback();
              PlaylistManager.CurrentPlaying -= 1;
              break;
            case PlayMode.Random:
            case PlayMode.ListRepeat:
              var next = PlaylistManager.CurrentPlaying - 1;
							if (next < 0) next = PlaylistManager.Filelist.Count - 1;
              currentPlayback = PlaylistManager.Filelist[next].GetFilePlayback();
              PlaylistManager.CurrentPlaying = next;
              break;
          }

          if (wasplaying)
          {
            try
            {
              currentPlayback.Start();
            }
            catch (Exception e)
            {
              PluginLog.Error(e, "error when try playing next song.");
            }
          }
          Task.Run(SwitchInstrument.WaitSwitchInstrument);
        }
        catch (Exception e)
        {
          currentPlayback = null;
          PlaylistManager.CurrentPlaying = -1;
        }
      }
      else
      {
        PlaylistManager.CurrentPlaying -= 1;
      }
    }
  }
}