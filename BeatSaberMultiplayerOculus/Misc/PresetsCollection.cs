﻿using BeatSaberMultiplayer.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMultiplayer.Misc
{
    public static class PresetsCollection
    {
        public static List<RoomPreset> loadedPresets = new List<RoomPreset>();

        public static void ReloadPresets()
        {
            try
            {
                loadedPresets.Clear();

                if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "UserData")))
                {
                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "UserData"));
                }

                if (!Directory.Exists(Path.Combine(Path.Combine(Environment.CurrentDirectory, "UserData"), "RoomPresets")))
                {
                    Directory.CreateDirectory(Path.Combine(Path.Combine(Environment.CurrentDirectory, "UserData"), "RoomPresets"));
                }

                List<string> presetFiles = Directory.GetFiles(Path.Combine(Path.Combine(Environment.CurrentDirectory, "UserData"), "RoomPresets"), "*.json").ToList();

                Logger.Info($"Found {presetFiles.Count} presets in RoomPresets folder");

                foreach (string path in presetFiles)
                {
                    try
                    {
                        RoomPreset preset = RoomPreset.LoadPreset(path);
                        loadedPresets.Add(preset);
                        Logger.Info($"Found preset \"{preset.GetName()}\"");
                    }
                    catch (Exception e)
                    {
                        Logger.Info($"Unable to parse preset @ {path}! Exception: {e}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Exception("Unable to load presets! Exception: " + e);
            }
        }
    }
}
