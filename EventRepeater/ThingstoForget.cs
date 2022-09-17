using System;
using System.Collections.Generic;

//whatever other using stuff. is cool
namespace EventRepeater
{
    public class ThingsToForget
    {
        public List<int> RepeatEvents { get; set; }
        public List<string> RepeatMail { get; set; }
        public List<int> RepeatResponse { get; set; }

        public ThingsToForget()
        {
            RepeatEvents = new List<int>();
            RepeatMail = new List<string>();
            RepeatResponse = new List<int>();
            
        }
    }
}