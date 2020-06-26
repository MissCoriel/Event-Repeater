using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using Newtonsoft.Json;

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
        private HashSet<string> MailToForget = new HashSet<string>();
        private HashSet<int> ResponseToForget = new HashSet<int>();

        


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;

            // collect data models
            // IList<ThingsToForget> models = new List<ThingsToForget>();
            // models.Add(this.Helper.Data.ReadJsonFile<ThingsToForget>("content.json"));
            //foreach (IContentPack contentPack in this.Helper.ContentPacks.GetOwned())
            //    models.Add(contentPack.ReadJsonFile<ThingsToForget>("content.json"));
            IList<ThingsToForget> models = new List<ThingsToForget>();
            foreach (IModInfo mod in this.Helper.ModRegistry.GetAll())
            {
                // make sure it's a Content Patcher pack
                if (!mod.IsContentPack || mod.Manifest.ContentPackFor?.UniqueID.Trim().Equals("Pathoschild.ContentPatcher", StringComparison.InvariantCultureIgnoreCase) != true)
                    continue;

                // get the directory path containing the manifest.json
                // HACK: IModInfo is implemented by ModMetadata, an internal SMAPI class which
                // contains much more non-public information. Caveats:
                //   - This isn't part of the public API so it may break in future versions.
                //   - Since the class is internal, we need reflection to access the values.
                //   - SMAPI's reflection API doesn't let us reflect into SMAPI, so we need manual
                //     reflection instead.
                //   - SMAPI's data API doesn't let us access an absolute path, so we need to parse
                //     the model ourselves.
                string directoryPath = (string)mod.GetType().GetProperty("DirectoryPath")?.GetValue(mod);
                if (directoryPath == null)
                    throw new InvalidOperationException($"Couldn't fetch the DirectoryPath property from the mod info for {mod.Manifest.Name}.");

                // read the JSON file
                IContentPack contentPack = this.Helper.ContentPacks.CreateFake(directoryPath);
                models.Add(contentPack.ReadJsonFile<ThingsToForget>("content.json"));
                // extract event IDs
                foreach (ThingsToForget model in models)
                {
                    if (model?.RepeatEvents == null)
                        continue;

                    foreach (int eventID in model.RepeatEvents)
                        this.EventsToForget.Add(eventID);

                }
                foreach (ThingsToForget model in models)
                {
                    if (model?.RepeatMail == null)
                        continue;

                    foreach (string mailID in model.RepeatMail)
                        this.MailToForget.Add(mailID);

                }
                foreach (ThingsToForget model in models)
                {
                    if (model?.RepeatResponse == null)
                        continue;

                    foreach (int ResponseID in model.RepeatResponse)
                        this.ResponseToForget.Add(ResponseID);

                } 

            }
                helper.ConsoleCommands.Add("eventforget", "'usage: eventforget <id>", ForgetManualCommand);
                helper.ConsoleCommands.Add("showevents", "'usage: Lists all completed events", ShowEventsCommand);
                helper.ConsoleCommands.Add("showmail", "'usage: Lists all seen mail", ShowMailCommand);
                helper.ConsoleCommands.Add("mailforget", "'usage: mailforget <id>", ForgetMailCommand);
                helper.ConsoleCommands.Add("sendme", "'usage: sendme <id>", SendMailCommand);
                helper.ConsoleCommands.Add("showresponse", "'usage: Lists Response IDs.  For ADVANCED USERS!!", ShowResponseCommand);
                helper.ConsoleCommands.Add("responseforget", "'usage: responseforget <id>'", ForgetResponseCommand);
                helper.ConsoleCommands.Add("responseadd", "'usage: responseadd <id>'  Inject a question response.", ResponseAddCommand);

            
        }

        /*********
        ** A bunch of Methods
        *********/
        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            foreach (var seenEvent in this.EventsToForget)
            {
                bool anyEventRemoved = false;

                
                    if (Game1.player.eventsSeen.Remove(seenEvent))
                    {
                        anyEventRemoved = true;
                        this.Monitor.Log("Repeatable Event Found! Resetting for next time! Event ID: " + seenEvent, LogLevel.Debug);
                    }
                

                if (!anyEventRemoved)
                    this.Monitor.Log("You have not seen any Repeatable Events.");
            }
            
            foreach (string seenMail in this.MailToForget)
            {
                bool anyMailRemoved = false;
                if (Game1.player.mailReceived.Remove(seenMail))
                {
                    anyMailRemoved = true;
                    Monitor.Log("Repeatable Mail found!  Resetting: " + seenMail, LogLevel.Debug);
                }
                if (!anyMailRemoved)
                {
                    this.Monitor.Log("You have not opened any Repeatable mail.");
                }
                    
            }
            foreach (var seenResponse in this.ResponseToForget)
            {
                bool anyResponseRemoved = false;
                if (Game1.player.dialogueQuestionsAnswered.Remove(seenResponse))
                {
                    anyResponseRemoved = true;
                    Monitor.Log("Repeatable Response Found! Resetting: " + seenResponse, LogLevel.Debug);
                }
                if (!anyResponseRemoved)
                {
                    this.Monitor.Log("No repeatable responses found.");
                }
            }

        }



        private void ForgetManualCommand(string command, string[] parameters)
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
        private void ShowEventsCommand(string command, string[] parameters)
        {
            string eventsSeen = "Events seen: ";
            foreach (var e in Game1.player.eventsSeen)
            {
                eventsSeen += e + ", ";
            }
            Monitor.Log(eventsSeen, LogLevel.Debug);
        }
        private void ShowMailCommand(string command, string[] parameters)
        {
            string mailSeen = "Mail Seen: ";
            foreach (var e in Game1.player.mailReceived)
            {
                mailSeen += e + ", ";
            }
            Monitor.Log(mailSeen, LogLevel.Debug);
        }
        private void ForgetMailCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0) return;
            try
            {
                string MailToForget = parameters[0];
                Game1.player.mailReceived.Remove(MailToForget);
                Monitor.Log("Forgetting event id: " + MailToForget, LogLevel.Debug);

            }
            catch (Exception) { }
        }
        private void SendMailCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0) return;
            try
            {
                string MailtoSend = parameters[0];
                Game1.addMailForTomorrow(MailtoSend);
                Monitor.Log("Check Mail Tomorrow!! Sending: " + MailtoSend, LogLevel.Debug);

            }
            catch (Exception) { }
        }
        private void ShowResponseCommand(string command, string[] parameters)
        {
            string dialogueQuestionsAnswered = "Response IDs: ";
            foreach (var e in Game1.player.dialogueQuestionsAnswered)
            {
                dialogueQuestionsAnswered += e + ", ";
            }
            Monitor.Log(dialogueQuestionsAnswered, LogLevel.Debug);
        }
        private void ForgetResponseCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0) return;
            try
            {
                int responseToForget = int.Parse(parameters[0]);
                Game1.player.dialogueQuestionsAnswered.Remove(responseToForget);
                Monitor.Log("Forgetting Response ID: " + responseToForget, LogLevel.Debug);

            }
            catch (Exception) { }
        }
        private void ResponseAddCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0) return;
            try
            {
                int responseAdd = int.Parse(parameters[0]);
                Game1.player.dialogueQuestionsAnswered.Add(responseAdd);
                Monitor.Log("Injecting Response ID: " + responseAdd, LogLevel.Debug);
            }
            catch (Exception) { }
                        
        }



    }
}