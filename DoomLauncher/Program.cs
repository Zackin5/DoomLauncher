using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing;
using DoomLauncher.Constants;
using DoomLauncher.Models;
using DoomLauncher.Enums;
using DoomLauncher.Utilities;
using Console = Colorful.Console;

namespace DoomLauncher
{
    class Program
    {
        private static LauncherConfig _launcherConfig;
        private static int _activeExecutableIndex;
        private static (string category, int index) _activeModReference;
        private static (string category, int index) _activeLevelReference;
        private static List<(string category, int index)> _activeMutatorReferences;

        private static DoomExecutable _activeExecutable =>
            _launcherConfig?.Executables.ElementAtOrDefault(_activeExecutableIndex);
        private static Mod _activeMod => _activeModReference.category != null ?
            _launcherConfig?.Mods[_activeModReference.category].ElementAtOrDefault(_activeModReference.index) : null;
        private static Mod _activeLevel => _activeLevelReference.category != null ?
            _launcherConfig?.Levels[_activeLevelReference.category].ElementAtOrDefault(_activeLevelReference.index) : null;
        private static List<Mod> _activeMutators()
        {
            var results = new List<Mod>();

            if (_activeMutatorReferences != null)
                foreach (var activeMutator in _activeMutatorReferences)
                {
                    results.Add(_launcherConfig.Mods[activeMutator.category].ElementAtOrDefault(activeMutator.index));
                }

            return results;
        }

        static int Main(string[] args)
        {
            // Load settings file
            const string settingsPath = @".\DoomSettings.json";

            var (resultCode, config) = SettingsParserUtil.ParseSettings(settingsPath);

            switch (resultCode)
            {
                case SettingParserResult.Success:
                    _launcherConfig = config;
                    break;

                case SettingParserResult.FailFileNotFound:
                    SettingsParserUtil.CreateDefaultSettings(settingsPath);
                    Console.WriteLine("Failed to load configuration settings.\nA default empty settings file was created.");
                    return -404;

                default:
                    Console.WriteLine("An unexpected error occured while trying to parse configuration settings file.");
                    return -1;
            }

            // Execute logic
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
            PrintModsByCategory(_launcherConfig.Mods);

            do
            {
                _activeModReference = GetInput(_launcherConfig.Mods, out nextState);
            } while (_activeModReference.index == -1);

            if (nextState == LauncherState.None)
                nextState = LauncherState.Level;
        }

        private static void GetLevel(out LauncherState nextState)
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Pick a level wad:");
            PrintModsByCategory(_launcherConfig.Levels);

            _activeLevelReference = GetInput(_launcherConfig.Levels, out nextState);

            if (nextState == LauncherState.None)
                nextState = LauncherState.Execute;
        }

        private static void GetMutator(LauncherState previousState, out LauncherState nextState)
        {
            Console.Clear();
            WriteMenu();

            Console.WriteLine("Add a mutator:");
            PrintModsByCategory(_launcherConfig.Mutators);

            _activeMutatorReferences.Add(GetInput(_launcherConfig.Mutators, out nextState));

            if (nextState == LauncherState.None)
                nextState = previousState;
        }

        private static (string category, int index) GetInput(Dictionary<string, List<Mod>> modDictionaries, out LauncherState nextState)
        {
            nextState = LauncherState.None;

            while (true)
            {
                Console.Write(LanguageConst.ConsolePrompt);
                var input = Console.ReadLine();

                if(string.IsNullOrWhiteSpace(input))
                {
                    return (string.Empty, -1);
                }

                if(input.Equals("m", StringComparison.OrdinalIgnoreCase))
                {
                    nextState = LauncherState.Mutator;
                    return (string.Empty, -1);
                }

                if(input.Equals("RAND", StringComparison.OrdinalIgnoreCase))
                {
                    var rand = new Random();

                    var (category, mods) = modDictionaries.ElementAt(rand.Next(0, modDictionaries.Count - 1));
                    var modIndex = rand.Next(0, mods.Count - 1);

                    return (category, modIndex);
                }

                // If we match on a mod, return it's category and index
                foreach (var (category, mods) in modDictionaries)
                {
                    foreach (var (mod, i) in mods.Select((f, i) => (f, i)))
                    {
                        if (mod.Code != null && mod.Code.Equals(input, StringComparison.OrdinalIgnoreCase))
                            return (category, i);
                    }
                }

                // If we failed to find mod, report invalid input (with fuzzy search)
                var fuzzyModCodes = FuzzySharp.Process
                    .ExtractAll(
                        input, 
                        modDictionaries.Values
                            .SelectMany(f => f)
                            .Select(f => f?.Code ?? string.Empty), 
                        cutoff: 80)
                    .Select(f => f.Value)
                    .ToList();
                
                if (fuzzyModCodes.Any())
                {
                    Console.WriteLine("Invalid input. Did you mean one of these?");
                    PrintMods(modDictionaries.Values.SelectMany(f => f).Where(f => fuzzyModCodes.Contains(f.Code)));
                }
                else
                {
                    Console.WriteLine("Invalid input.");
                }
            }
        }

        private static Mod GetModByCode(Dictionary<string, List<Mod>> modList, string modCode)
        {
            return modList.Values.SelectMany(f => f).FirstOrDefault(f => f.Code != null && f.Code.Equals(modCode, StringComparison.OrdinalIgnoreCase));
        }

        private static void PrintModsByCategory(Dictionary<string, List<Mod>> modDictionaries)
        {
            foreach (var (category, mods) in modDictionaries)
            {
                if(!string.IsNullOrWhiteSpace(category))
                    Console.WriteLine(category);

                PrintMods(mods);
            }
        }

        private static void PrintMods(IEnumerable<Mod> mods)
        {
            var orderedMods = mods
                .Select((mod, i) => new { mod, i })
                .OrderBy(f => f.mod.Code).ThenBy(f => f.mod.Description);

            for (var i = 0; i < orderedMods.Count(); i++)
            {
                var configMod = orderedMods.ElementAt(i);
                //var colorPower = (int)((configMod.i / (double)mods.Count()) * 255);
                var colorPower = 255 - (int)((Math.Clamp(mods.Count() - configMod.i, 0, 5) / 5.0) * 255);

                Console.WriteLine($"{configMod.mod.Code,-5}- {configMod.mod.Description}", Color.FromArgb(255, colorPower, 0));
            }

            Console.WriteLine();
        }

        private static void WriteMenu()
        {
            var mutatorNames = "";

            if (_activeMutatorReferences != null)
                foreach (var (mutatorCategory, mutatorIndex) in _activeMutatorReferences)
                {
                    mutatorNames += _launcherConfig.Mods[mutatorCategory].ElementAtOrDefault(mutatorIndex)?.Code + ",";
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

            // TODO: implement/update mutators
            /*var muts = _launcherConfig.Mutators.Select(fmut => GetModPaths(_launcherConfig.Mutators, fmut));

            foreach (var mut in muts)
            {
                modPaths.AddRange(mut);
            }*/

            modPaths.AddRange(GetModPaths(_launcherConfig.Mods, _activeMod));

            args += $" {FormatArg(modPaths)} {FormatArg(passedArgs)}";

            // Execute
            File.Delete(@".\lastArgs.txt");
            File.WriteAllText(@".\lastArgs.txt", args);

            Process.Start(_activeExecutable.Path, args);
        }

        private static List<string> GetModPaths(Dictionary<string, List<Mod>> modList, Mod mod)
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
            writer.WriteLine($"{_activeMod?.Code}, {_activeLevel?.Code}, {string.Join(" ", _activeMutators()?.Select(f => f.Code))}");
        }
    }
}
