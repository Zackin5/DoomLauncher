using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing;
using DoomLauncher.Constants;
using DoomLauncher.Models;
using DoomLauncher.Enums;
using Newtonsoft.Json;
using Console = Colorful.Console;

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

            var launcherState = LauncherState.Mod;
            var previousState = launcherState;
            while (launcherState != LauncherState.Execute)
            {
                switch (launcherState)
                {
                    case LauncherState.Mod:
                        GetMod(out launcherState);
                        break;

                    case LauncherState.Level:
                        if (!string.IsNullOrWhiteSpace(_activeMod.IWad))
                        {
                            launcherState = LauncherState.Execute;
                            break;
                        }

                        GetLevel(out launcherState);
                        break;

                    case LauncherState.Mutator:
                        GetMutator(previousState, out launcherState);
                        break;

                    default:
                        throw new NotSupportedException();
                }

                previousState = launcherState;
            }

            ExecuteDoom(args);

            return 0;
        }

        private static void GetMod(out LauncherState nextState)
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Pick a gameplay wad:");
            PrintMods(_launcherConfig.Mods);

            do
            {
                _activeModIndex = GetInput(_launcherConfig.Mods, out nextState);
            } while (_activeModIndex == -1);

            if (nextState == LauncherState.None)
                nextState = LauncherState.Level;
        }

        private static void GetLevel(out LauncherState nextState)
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Pick a level wad:");
            PrintMods(_launcherConfig.Levels);

            _activeLevelIndex = GetInput(_launcherConfig.Levels, out nextState);

            if (nextState == LauncherState.None)
                nextState = LauncherState.Execute;
        }

        private static void GetMutator(LauncherState previousState, out LauncherState nextState)
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Add a mutator:");
            PrintMods(_launcherConfig.Mutators);

            _activeMutatorIndexes.Add(GetInput(_launcherConfig.Mutators, out nextState));

            if (nextState == LauncherState.None)
                nextState = previousState;
        }

        private static int GetInput(List<Mod> mods, out LauncherState nextState)
        {
            nextState = LauncherState.None;

            while (true)
            {
                Console.Write(LanguageConst.ConsolePrompt);
                var input = Console.ReadLine();

                if(string.IsNullOrWhiteSpace(input))
                {
                    return -1;
                }

                if(input.Equals("m", StringComparison.OrdinalIgnoreCase))
                {
                    nextState = LauncherState.Mutator;
                    return -1;
                }

                // Find mod and execute
                var selectedMod = mods?.FindIndex(f =>
                    f.Code != null && f.Code.Equals(input, StringComparison.OrdinalIgnoreCase));

                if (selectedMod != null && selectedMod != -1)
                {
                    // Valid input
                    return selectedMod.Value;
                }

                // Report invalid input (with fuzzy search)
                var fuzzyModCodes = FuzzySharp.Process
                    .ExtractAll(input, mods.Select(f => f?.Code ?? string.Empty), cutoff: 80)
                    .Select(f => f.Value)
                    .ToList();
                
                if (fuzzyModCodes.Any())
                {
                    Console.WriteLine("Invalid input. Did you mean one of these?");
                    PrintMods(mods.Where(f => fuzzyModCodes.Contains(f.Code)));
                }
                else
                {
                    Console.WriteLine("Invalid input.");
                }
            }
        }

        private static Mod GetModByCode(List<Mod> modList, string modCode)
        {
            return modList.FirstOrDefault(f => f.Code != null && f.Code.Equals(modCode, StringComparison.OrdinalIgnoreCase));
        }

        private static void PrintMods(IEnumerable<Mod> mods)
        {
            foreach (var modGrouping in mods
                .Where(f => !string.IsNullOrWhiteSpace(f.Code))
                .GroupBy(f => f.Category)
                .OrderBy(f => f.Key))
            {
                if(!string.IsNullOrWhiteSpace(modGrouping.Key))
                    Console.WriteLine(modGrouping.Key);

                var orderedMods = modGrouping
                    .Select((mod, i) => new { mod, i })
                    .OrderBy(f => f.mod.Code).ThenBy(f => f.mod.Description);

                for (var i = 0; i < orderedMods.Count(); i++)
                {
                    var configMod = orderedMods.ElementAt(i);
                    //var colorPower = (int)((configMod.i / (double)modGrouping.Count()) * 255);
                    var colorPower = 255 - (int)((Math.Clamp(modGrouping.Count() - configMod.i, 0, 5) / 5.0) * 255);

                    Console.WriteLine($"{configMod.mod.Code,-5}- {configMod.mod.Description}", Color.FromArgb(255, colorPower, 0));
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

        private static string FormatArg(IEnumerable<string> args)
        {
            return string.Join(' ', args.Select(f => f.Contains(' ') ? $"\"{f}\"" : f));
        }

        private static void ExecuteDoom(string[] passedArgs)
        {
            WriteHistory();

            var args = "";

            // TODO: inherit iwads from parents
            if (!string.IsNullOrWhiteSpace(_activeMod.IWad))
                args += $"-iwad {_activeMod.IWad} ";
            else if (!string.IsNullOrWhiteSpace(_activeLevel?.IWad))
                args += $"-iwad {_activeLevel.IWad} ";

            args += "-file";

            // Merge and flatten mod list
            var modPaths = new List<string>();
            modPaths.AddRange(GetModPaths(_launcherConfig.Levels, _activeLevel));

            var muts = _launcherConfig.Mutators.Select(fmut => GetModPaths(_launcherConfig.Mutators, fmut));

            foreach (var mut in muts)
            {
                modPaths.AddRange(mut);
            }

            modPaths.AddRange(GetModPaths(_launcherConfig.Mods, _activeMod));

            args += $" {FormatArg(modPaths)} {FormatArg(passedArgs)}";

            // Execute
            File.Delete(@".\lastArgs.txt");
            File.WriteAllText(@".\lastArgs.txt", args);

            Process.Start(_activeExecutable.Path, args);
        }

        private static List<string> GetModPaths(List<Mod> modList, Mod mod)
        {
            if(mod == null)
                return new List<string>();

            var result = new List<string>();

            if (mod.ParentMod != null)
            {
                result.AddRange(GetModPaths(modList, GetModByCode(modList, mod.ParentMod)));
            }

            result.AddRange(mod.Path);

            return result;
        }

        private static void WriteHistory()
        {
            using var writer = File.AppendText(@".\history.txt");
            writer.WriteLine($"{_activeMod?.Code}, {_activeLevel?.Code}, {string.Join(" ", _launcherConfig?.Mutators?.Select(f => f.Code))}");
        }
    }
}
