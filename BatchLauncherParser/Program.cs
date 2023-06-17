using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DoomLauncher.Models;
using Newtonsoft.Json;

namespace BatchLauncherParser
{
    class Program
    {
        /// <summary>
        /// Hardcoded program for parsing old doomlauncher.bat
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var filePath = args[0];

            var fileLines = File.ReadAllLines(filePath);

            var parsedConfig = new LauncherConfigV1
            {
                Mods = new List<Mod>(),
                Levels = new List<Mod>(),
                Mutators = new List<Mod>()
            };

            // Parse mods
            parsedConfig.Mods.AddRange(ParseModRange(511, 866, fileLines));

            // Add mod attributes
            var modTitles = ParseModTitleRange(19, 24, fileLines, "Mostly Vanilla");
            modTitles.AddRange(ParseModTitleRange(27, 97, fileLines, "Gameplay Mods"));
            modTitles.AddRange(ParseModTitleRange(100, 106, fileLines, "Total Conversions"));
            modTitles.AddRange(ParseModTitleRange(109, 113, fileLines, "BRUTAL DOOM"));

            parsedConfig.Mods = MapModTitles(parsedConfig.Mods, modTitles);

            // Parse levels
            parsedConfig.Levels.AddRange(ParseLevelRange(331, 445, fileLines));

            var levelTitles = ParseModTitleRange(173, 183, fileLines, "1990's");
            levelTitles.AddRange(ParseModTitleRange(194, 224, fileLines, "2000's"));
            levelTitles.AddRange(ParseModTitleRange(235, 300, fileLines, "2010's"));
            levelTitles.AddRange(ParseModTitleRange(303, 303, fileLines, "Cacowards 25"));
            levelTitles.AddRange(ParseModTitleRange(306, 311, fileLines, "Cacowards 25"));

            parsedConfig.Levels = MapModTitles(parsedConfig.Levels, levelTitles);

            // Dump JSON
            var output = JsonConvert.SerializeObject(parsedConfig, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(@".\output.json", output);
            Console.WriteLine(output);
        }

        private static List<(string code, string title, string category)> ParseModTitleRange(int start, int end, IReadOnlyList<string> fileLines, string category)
        {
            var names = new List<(string code, string title, string category)>();

            for (var i = start - 1; i < end; i++)
            {
                names.Add((fileLines[i].Substring(5, 4).Trim(), fileLines[i].Substring(12), category));
            }

            return names;
        }

        private static List<Mod> ParseModRange(int start, int end, IReadOnlyList<string> fileLines)
        {
            var mods = new List<Mod>();
            var wipMod = new Mod
            {
                Path = new List<string>(),
                Tags = new List<string>()
            };

            for (var i = start; i < end; i++)
            {
                var activeLine = fileLines[i];

                if (activeLine[0] == ':')
                    wipMod.Code = activeLine.Substring(1);
                else if (activeLine.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
                    continue;
                else if (activeLine.StartsWith("start", StringComparison.OrdinalIgnoreCase))
                {
                    var lineParts = SplitString(activeLine);

                    foreach (var linePart in lineParts)
                    {
                        if (linePart.StartsWith(@"E:\DOOM"))
                            wipMod.Path.Add(linePart);
                        else if (linePart.EndsWith(".wad", StringComparison.OrdinalIgnoreCase))
                            wipMod.IWad = linePart;
                    }
                }
                else if (activeLine.Equals(@"EXIT /B", StringComparison.OrdinalIgnoreCase))
                {
                    mods.Add(wipMod);
                    wipMod = new Mod
                    {
                        Path = new List<string>(),
                        Tags = new List<string>()
                    };
                }
            }

            return mods;
        }

        private static List<Mod> ParseLevelRange(int start, int end, IReadOnlyList<string> fileLines)
        {
            var mods = new List<Mod>();

            for (var i = start; i < end; i++)
            {
                var wipMod = new Mod
                {
                    Path = new List<string>(),
                    Tags = new List<string>()
                };

                var mapItems = SplitString(fileLines[i]);

                var codeIndex = mapItems.FindIndex(f => f.Contains("%CHOICE%", StringComparison.OrdinalIgnoreCase));
                var iwadIndex = mapItems.FindIndex(f => f.Contains("-iwad", StringComparison.OrdinalIgnoreCase));

                wipMod.Code = mapItems[codeIndex].Substring(10).Trim();

                if (iwadIndex != -1)
                    wipMod.IWad = mapItems.ElementAtOrDefault(iwadIndex + 1);

                wipMod.Path = mapItems.Where(f => f.StartsWith(@"E:\DOOM", StringComparison.OrdinalIgnoreCase)).ToList();

                mods.Add(wipMod);
            }

            return mods;

        }

        private static List<Mod> MapModTitles(List<Mod> modList, List<(string code, string title, string category)> titles)
        {
            for (var index = 0; index < modList.Count; index++)
            {
                var title = titles.FirstOrDefault(f => f.code.Equals(modList[index].Code, StringComparison.OrdinalIgnoreCase));

                modList[index].Description = title.title;
                modList[index].Category = title.category;

                var modPath = modList[index].Path.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(modPath))
                    modList[index].Year = File.GetCreationTime(modPath).Year;
            }

            return modList;
        }

        private static List<string> SplitString(string str) => 
            Regex.Matches(str, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value)
            .ToList();
    }
}
