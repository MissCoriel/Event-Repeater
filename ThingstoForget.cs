using System.Collections.Generic;

//whatever other using stuff. is cool
namespace EventRepeater
{
    public class ThingsToForget
    {
        public List<string> RepeatEvents { get; set; } = new();
        public List<string> RepeatMail { get; set; } = new();
        public List<string> RepeatResponse { get; set; } = new();
    }
}