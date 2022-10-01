using StardewModdingAPI.Utilities;

namespace EventRepeater
{
    internal sealed class ConfigModel
    {
        //public SButton EventWindow { get; set; } = SButton.Pause;
        public KeybindList EmergencySkip { get; set; } = KeybindList.Parse("LeftControl + S");
        public KeybindList ShowInfo { get; set; } = KeybindList.Parse("LeftControl + I");
        public KeybindList NormalSkip { get; set; } = KeybindList.Parse("LeftAlt + S");
    }
}
