using System.Collections.Generic;

namespace DoomLauncher.Models
{
    public class LauncherConfig
    {
        public List<DoomExecutable> Executables { get; set; }
        public Dictionary<string, List<Mod>> Mods { get; set; }
        public Dictionary<string, List<Mod>> Mutators { get; set; }
        public Dictionary<string, List<Mod>> Levels { get; set; }
    }
}
