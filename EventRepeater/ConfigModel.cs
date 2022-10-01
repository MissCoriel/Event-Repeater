using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace EventRepeater
{
    internal class ConfigModel
    {
        //public SButton EventWindow { get; set; } = SButton.Pause;
        public KeybindList EmergencySkip { get; set; } = KeybindList.Parse("LeftControl + S");
        public KeybindList ShowInfo { get; set; } = KeybindList.Parse("LeftControl + I");
        public KeybindList NormalSkip { get; set; } = KeybindList.Parse("LeftAlt + S");
    }
}
