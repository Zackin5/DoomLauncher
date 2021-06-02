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
        public string[] Path { get; set; }

        /// <summary>
        /// WAD filter tags
        /// </summary>
        public string[] Tags { get; set; }

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
