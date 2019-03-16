using System;
using System.Collections.Generic;

//whatever other using stuff. is cool
namespace EventRepeater
{
    public class ThingsToForget
    {
        public List<int> RepeatEvents { get; set; }
        public string Format { get; set; }

        public ThingsToForget()
        {
            RepeatEvents = new List<int>();
            Format = "1.0";
        }
    }
}