using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace EventRepeater
{
    /// <summary>The mod entry class loaded by SMAPI.</summary>
    public class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
        /// <summary>The event IDs to forget.</summary>
        private HashSet<int> EventsToForget = new HashSet<int>();



        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;

            // collect data models
            IList<ThingsToForget> models = new List<ThingsToForget>();
            models.Add(this.Helper.Data.ReadJsonFile<ThingsToForget>("events.json"));
            foreach (IContentPack contentPack in this.Helper.ContentPacks.GetOwned())
                models.Add(contentPack.ReadJsonFile<ThingsToForget>("events.json"));

            // extract event IDs
            foreach (ThingsToForget model in models)
            {
                if (model?.RepeatEvents == null)
                    continue;

                foreach (int eventID in model.RepeatEvents)
                    this.EventsToForget.Add(eventID);

            }
            helper.ConsoleCommands.Add("eventforget", "'usage: eventforget <id>", ForgetManualCommand);
            helper.ConsoleCommands.Add("showevents", "'usage: Lists all completed events", ShowEventsCommand);

        }


        /*********
        ** Public methods
        *********/
        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            foreach (var seenEvent in this.EventsToForget)
            {
                Game1.player.eventsSeen.Remove(seenEvent);
                Monitor.Log("Forgetting event id: " + seenEvent, LogLevel.Debug);
            }
        }

        public void ForgetManualCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0) return;
            try
            {
                int eventToForget = int.Parse(parameters[0]);
                Game1.player.eventsSeen.Remove(eventToForget);
                Monitor.Log("Forgetting event id: " + eventToForget, LogLevel.Debug);
            }
            catch (Exception) { }
        }
        public void ShowEventsCommand(string command, string[] parameters)
        {
            string eventsSeen = "Events seen: ";
            foreach (var e in Game1.player.eventsSeen)
            {
                eventsSeen += e + ", ";
            }
            Monitor.Log(eventsSeen, LogLevel.Debug);
        }
    }
}