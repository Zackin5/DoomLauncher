﻿using System.Collections.Generic;

namespace DoomLauncher.Models
{
    public class LauncherConfigV1
    {
        public List<DoomExecutable> Executables { get; set; }
        public List<Mod> Mods { get; set; }
        public List<Mod> Mutators { get; set; }
        public List<Mod> Levels { get; set; }
    }
}
