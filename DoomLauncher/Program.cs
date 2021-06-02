using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DoomLauncher.Constants;
using DoomLauncher.Models;
using Newtonsoft.Json;

namespace DoomLauncher
{
    class Program
    {
        private static LauncherConfig _launcherConfig;
        private static int _activeExecutableIndex;
        private static int? _activeModIndex;
        private static int? _activeLevelIndex;
        private static List<int> _activeMutatorIndexes;

        private static DoomExecutable _activeExecutable =>
            _launcherConfig?.Executables.ElementAtOrDefault(_activeExecutableIndex);
        private static Mod _activeMod =>
            _launcherConfig?.Mods.ElementAtOrDefault(_activeModIndex ?? -1);
        private static Mod _activeLevel =>
            _launcherConfig?.Levels.ElementAtOrDefault(_activeLevelIndex ?? -1);
        private static List<Mod> _activeMutators()
        {
            var results = new List<Mod>();

            if (_activeMutatorIndexes != null)
                foreach (var activeMutator in _activeMutatorIndexes)
                {
                    results.Add(_launcherConfig.Mods.ElementAtOrDefault(activeMutator));
                }

            return results;
        }

        static int Main(string[] args)
        {
            const string settingsPath = @".\DoomSettings.json";
            if (!File.Exists(settingsPath))
            {
                File.WriteAllText(settingsPath, JsonConvert.SerializeObject(new LauncherConfig
                {
                    Executables = new List<DoomExecutable>{new DoomExecutable()},
                    Levels = new List<Mod>{new Mod()},
                    Mods = new List<Mod>{new Mod()},
                    Mutators = new List<Mod>{new Mod()}
                }, Formatting.Indented));

                Console.WriteLine("Failed to load configuration settings.\nA default empty settings file was created.");
                return -1;
            }

            _launcherConfig = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(settingsPath));
            
            GetMod();

            if(!string.IsNullOrWhiteSpace(_activeMod.IWad))
                ExecuteDoom();

            GetLevel();

            ExecuteDoom();

            return 0;
        }

        private static void GetMod()
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Pick a gameplay wad:");
            PrintMods(_launcherConfig.Mods);

            _activeModIndex = GetInput(_launcherConfig.Mods);
        }

        private static void GetLevel()
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Pick a level wad:");
            PrintMods(_launcherConfig.Levels);

            _activeLevelIndex = GetInput(_launcherConfig.Levels);
        }

        private static void GetMutator()
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Add a mutator:");
            PrintMods(_launcherConfig.Mutators);

            _activeMutatorIndexes.Add(GetInput(_launcherConfig.Mutators));
        }

        private static int GetInput(List<Mod> mods)
        {
            var returnIndex = -1;
            while(true)
            {
                Console.Write(LanguageConst.ConsolePrompt);
                var input = Console.ReadLine();

                if(input == null)
                {
                    Console.WriteLine("Invalid input");
                    continue;
                }

                var selectedMod = mods?.FindIndex(f =>
                    f.Code != null && f.Code.Equals(input, StringComparison.OrdinalIgnoreCase));

                if (selectedMod != null && selectedMod != -1)
                {
                    // Valid input
                    returnIndex = selectedMod.Value;
                    break;
                }

                Console.WriteLine("Invalid input");
            }

            return returnIndex;
        }

        private static void PrintMods(List<Mod> mods)
        {
            foreach (var modGrouping in mods
                .OrderBy(f => f.Year).ThenBy(f => f.Description)
                .Where(f => !string.IsNullOrWhiteSpace(f.Code))
                .GroupBy(f => f.Category))
            {
                if(!string.IsNullOrWhiteSpace(modGrouping.Key))
                    Console.WriteLine(modGrouping.Key);

                foreach (var configMod in modGrouping)
                {
                    Console.WriteLine($"{configMod.Code,-5}- {configMod.Description}");
                }

                Console.WriteLine();
            }
        }

        private static void WriteMenu()
        {
            var mutatorNames = "";

            if (_activeMutatorIndexes != null)
                foreach (var activeMutator in _activeMutatorIndexes)
                {
                    mutatorNames += _launcherConfig.Mods.ElementAtOrDefault(activeMutator)?.Code + ",";
                }

            Console.WriteLine($"EXEC:{_activeExecutable?.Code}  MOD:{_activeMod?.Code}  LVL:{_activeLevel?.Code} MUT:{mutatorNames}\n");
        }

        private static void ExecuteDoom()
        {
            var args = "";

            if (!string.IsNullOrWhiteSpace(_activeMod.IWad))
                args += $"-iwad {_activeMod.IWad} ";
            else if (!string.IsNullOrWhiteSpace(_activeLevel.IWad))
                args += $"-iwad {_activeLevel.IWad} ";

            args += "-file ";

            // Mutators
            args += string.Join(" ", _activeMutators().Select(f => string.Join(" ", f.Path)));
            
            // Mod
            args += string.Join(" ", _activeMod.Path);
            
            // Level
            args += string.Join(" ", _activeLevel.Path);

            Process.Start(_activeExecutable.Path, args);
        }
    }
}
