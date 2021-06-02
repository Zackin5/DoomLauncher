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

            var mods = new List<Mod>();

            // Parse mods
            mods.AddRange(ParseModRange(511, 866, fileLines));

            // Add mod attributes
            var titles = ParseModTitleRange(19, 24, fileLines, "Mostly Vanilla");
            titles.AddRange(ParseModTitleRange(27, 97, fileLines, "Gameplay Mods"));
            titles.AddRange(ParseModTitleRange(100, 106, fileLines, "Total Conversions"));
            titles.AddRange(ParseModTitleRange(109, 113, fileLines, "BRUTAL DOOM"));

            for (var index = 0; index < mods.Count; index++)
            {
                var title = titles.FirstOrDefault(f => f.code.Equals(mods[index].Code, StringComparison.OrdinalIgnoreCase));

                mods[index].Description = title.title;
                mods[index].Category = title.category;

                var modPath = mods[index].Path.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(modPath))
                    mods[index].Year = File.GetCreationTime(modPath).Year;
            }

            var output = JsonConvert.SerializeObject(mods, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            File.WriteAllText(@".\output.json", output);
            Console.WriteLine(output);
        }

        private static List<(string code, string title, string category)> ParseModTitleRange(int start, int end, IReadOnlyList<string> fileLines, string category)
        {
            var names = new List<(string code, string title, string category)>();

            for (var i = start; i < end; i++)
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
                    var lineParts = Regex.Matches(activeLine, @"[\""].+?[\""]|[^ ]+")
                        .Select(m => m.Value)
                        .ToList();

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
    }
}
