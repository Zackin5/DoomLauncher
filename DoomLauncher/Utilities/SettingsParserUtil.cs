using DoomLauncher.Enums;
using DoomLauncher.Models;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace DoomLauncher.Utilities
{
    class SettingsParserUtil
    {
        private static string LatestVersionStr = "v2";

        /// <summary>
        /// Parse a settings object
        /// </summary>
        /// <returns>Tuple item 1 is a success flag. Item 2 is the LauncherConfig if parsing was succesful</returns>
        public static (SettingParserResult resultCode, LauncherConfig config) ParseSettings(string settingsPath)
        {
            if (!File.Exists(settingsPath))
                return (SettingParserResult.FailFileNotFound, null);

            LauncherConfig parsedConfig = null;
            // Check file version header line
            using(var reader = new StreamReader(settingsPath))
            {
                var firstLine = reader.ReadLine();

                var versionRegex = new Regex(@"v(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var vregexMatch = versionRegex.Match(firstLine);

                // Match failed, attempt v1 parse
                if (!vregexMatch.Success)
                {
                    var config = JsonConvert.DeserializeObject<LauncherConfigV1>(firstLine + reader.ReadToEnd());

                    var updatedConfig = new LauncherConfig
                    {
                        Executables = config.Executables,
                        Mods = config.Mods.GroupBy(f => f.Category).ToDictionary(f => f.Key ?? "Unknown", f => f.ToList()),
                        Mutators = config.Mutators.GroupBy(f => f.Category).ToDictionary(f => f.Key ?? "Unknown", f => f.ToList()),
                        Levels = config.Levels.GroupBy(f => f.Category).ToDictionary(f => f.Key ?? "Unknown", f => f.ToList())
                    };

                    reader.Close();

                    WriteJsonSettings(settingsPath, updatedConfig);
                    Console.WriteLine($"Migrated v1 config to {LatestVersionStr}.");

                    parsedConfig = updatedConfig;
                }
                else
                {
                    // TODO: handle other version migrations
                    parsedConfig = JsonConvert.DeserializeObject<LauncherConfig>(reader.ReadToEnd());
                }

            }

            Debug.Assert(parsedConfig != null);
            return (SettingParserResult.Success, parsedConfig);
        }

        public static void CreateDefaultSettings(string settingsPath)
        {
            WriteJsonSettings(settingsPath, new LauncherConfig
            {
                Executables = new List<DoomExecutable> { new DoomExecutable() },
                Mods = new Dictionary<string, List<Mod>> { { "CategoryName", new List<Mod> { new Mod() } } },
                Mutators = new Dictionary<string, List<Mod>> { { "CategoryName", new List<Mod> { new Mod() } } },
                Levels = new Dictionary<string, List<Mod>> { { "CategoryName", new List<Mod> { new Mod() } } }
            }, NullValueHandling.Include);
        }

        private static void WriteJsonSettings(string settingsPath, LauncherConfig launcherConfig, NullValueHandling nullValueHandling = NullValueHandling.Ignore)
        {
            var jsonStr = JsonConvert.SerializeObject(launcherConfig, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = nullValueHandling
            });

            File.WriteAllText(settingsPath, $"{LatestVersionStr}\n{jsonStr}");
        }
    }
}
