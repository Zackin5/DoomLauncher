using System.Collections.Generic;

namespace DoomLauncher.Models
{
    public class Mod
    {
        /// <summary>
        /// Mod name
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Mod code for selection
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Filter category
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// WAD path(s)
        /// </summary>
        public List<string> Path { get; set; }

        /// <summary>
        /// WAD filter tags
        /// </summary>
        public List<string> Tags { get; set; }

        /// <summary>
        /// WAD year
        /// </summary>
        public int? Year { get; set; }
        
        /// <summary>
        /// IWAD name for override
        /// </summary>
        public string IWad { get; set; }
    }
}
