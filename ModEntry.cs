using System;
using System.Collections.Generic;
using System.IO;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Linq;
using System.Reflection;
using EventRepeater.Integrations;
using EventRepeater.Framework;
using StardewModdingAPI.Utilities;
using Netcode;

namespace EventRepeater
{
    /// <summary>The mod entry class loaded by SMAPI.</summary>
    public class ModEntry : Mod
    {
        /*********
        ** Fields
        *********/
        /// <summary>The event IDs to forget.</summary>
        private HashSet<string> EventsToForget = new();
        private HashSet<string> MailToForget = new();
        private HashSet<string> ResponseToForget = new();
        private Event? LastEvent;
        private List<string> ManualRepeaterList = new();
        private bool ShowEventIDs;
        private string? LastPlayed;
        private ConfigModel Config = null!; //Menu Button
        private int EventRemovalTimer;
        private string? eventtoskip; //uses Game1.CurrentEvent.id to acquire the ID
        private readonly PerScreen<int> mailIDCount;
        private readonly PerScreen<int> responseCount;
        private readonly PerScreen<HashSet<string>> OldFlags;
        private readonly PerScreen<HashSet<string>> responseMon;
        private bool CheckMailBox;
        private readonly PerScreen<string[]?> MailboxContent;
        public bool DebuggerMode;


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += this.OnLaunched;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.UpdateTicked += this.UpdateTicked;
            helper.Events.Input.ButtonReleased += this.OnButtonReleased;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

            this.Config = helper.ReadConfig<ConfigModel>();

            AssetManager.Initialize(helper.GameContent, this.Monitor);
            helper.Events.Content.AssetRequested += static (_, e) => AssetManager.Apply(e);
            helper.Events.Content.AssetsInvalidated += static (_, e) => AssetManager.Reset(e.NamesWithoutLocale);

            helper.ConsoleCommands.Add("eventforget", "'usage: eventforget <id>", ForgetManualCommand);
            helper.ConsoleCommands.Add("showevents", "'usage: Lists all completed events", ShowEventsCommand);
            helper.ConsoleCommands.Add("showmail", "'usage: Lists all seen mail", ShowMailCommand);
            helper.ConsoleCommands.Add("mailforget", "'usage: mailforget <id>", ForgetMailCommand);
            helper.ConsoleCommands.Add("sendme", "'usage: sendme <id>", SendMailCommand);
            helper.ConsoleCommands.Add("showresponse", "'usage: Lists Response IDs.  For ADVANCED USERS!!", ShowResponseCommand);
            helper.ConsoleCommands.Add("responseforget", "'usage: responseforget <id>'", ForgetResponseCommand);
            //helper.ConsoleCommands.Add("responseadd", "'usage: responseadd <id>'  Inject a question response.", ResponseAddCommand);
            helper.ConsoleCommands.Add("repeateradd", "'usage: repeateradd <id(optional)>' Create a repeatable event.  If no id is given, the last seen will be repeated.  Works on Next Day", ManualRepeater);
            helper.ConsoleCommands.Add("repeatersave", "'usage: repeatersave <filename>' Creates a textfile with all events you set to repeat manually.", SaveManualCommand);
            helper.ConsoleCommands.Add("repeaterload", "'usage: repeaterload <filename>' Loads the file you designate.", LoadCommand);
            helper.ConsoleCommands.Add("inject", "'usage: inject <event, mail, response> <ID>' Example: 'inject event 1324329'  Inject IDs into the game.", injectCommand);
            helper.ConsoleCommands.Add("stopevent", "'usage: stops current event.", StopEventCommand);
            helper.ConsoleCommands.Add("showinfo", "Toggles in game visuals of certain alerts.", ShowInfoCommand);
            helper.ConsoleCommands.Add("emergencyskip", "Forces an event skip.. will progress the game", EmergencySkipCommand);
            helper.ConsoleCommands.Add("fastmail", "`usage: fastmail <mailID>` Send mail instantly to your Mailbox", new Action<string, string[]>(this.FastMailCommand));
            helper.ConsoleCommands.Add("toggledebug", "Toggles Mail Monitor, Response Monitor, and Mailbox Monitor.  Use in splitscreen not recommended!", new Action<string, string[]>(this.DebuggerCommand));
        }

        /// <summary>
        /// Reads in packs.
        /// </summary>
        /// <param name="sender">SMAPI</param>
        /// <param name="e">Event args.</param>
        /// <exception cref="InvalidDataException">The mod we tried to grab from is not a CP mod.</exception>
        private void OnLaunched(object? sender, GameLaunchedEventArgs e)
        {
            GMCMHelper gmcm = new(this.Monitor, this.Helper.Translation, this.Helper.ModRegistry, this.ModManifest);

            if (gmcm.TryGetAPI())
            {
                gmcm.Register(
                    () => Config = new(),
                    () => this.Helper.WriteConfig(Config)
                );

                foreach (PropertyInfo property in typeof(ConfigModel).GetProperties())
                    gmcm.AddKeybindList(property, () => Config);
            }

            foreach (IModInfo mod in this.Helper.ModRegistry.GetAll())
            {
                // make sure it's a Content Patcher pack
                if (!mod.IsContentPack || !mod.Manifest.ContentPackFor!.UniqueID.AsSpan().Trim().Equals("Pathoschild.ContentPatcher", StringComparison.InvariantCultureIgnoreCase))
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

                IContentPack? modimpl = mod.GetType().GetProperty("ContentPack")!.GetValue(mod) as IContentPack;
                if (modimpl is null)
                    throw new InvalidDataException($"Couldn't grab mod from modinfo {mod.Manifest}");

                // read the JSON file
                if (modimpl.ReadJsonFile<ThingsToForget>("content.json") is not {} model)
                    continue;

                // extract event IDs
                bool flag = false;
                if (model.RepeatEvents?.Count is > 0)
                {
                    this.EventsToForget.UnionWith(model.RepeatEvents);
                    this.Monitor.Log($"Loading {model.RepeatEvents.Count} forgettable events for {mod.Manifest.UniqueID}");
                    flag = true;
                }
                if (model.RepeatMail?.Count is > 0)
                {
                    this.MailToForget.UnionWith(model.RepeatMail);
                    this.Monitor.Log($"Loading {model.RepeatMail.Count} forgettable mail for {mod.Manifest.UniqueID}");
                    flag = true;
                }
                if (model.RepeatResponse?.Count is > 0)
                {
                    this.ResponseToForget.UnionWith(model.RepeatResponse);
                    this.Monitor.Log($"Loading {model.RepeatResponse.Count} forgettable mail for {mod.Manifest.UniqueID}");
                    flag = true;
                }

                if (flag && !modimpl.Manifest.Dependencies.Any(dep => dep.UniqueID.AsSpan().Trim().Equals("misscoriel.eventrepeater", StringComparison.OrdinalIgnoreCase)))
                    this.Monitor.Log(modimpl.Manifest.Name + " uses Event Repeater features, but doesn't list it as a dependency in its manifest.json. This will stop working in future versions.", (LogLevel)3);

            }
            this.Monitor.Log($"Loaded a grand total of\n\t{this.EventsToForget.Count} events\n\t{this.MailToForget.Count} mail\n\t{this.ResponseToForget.Count} responses.");
        }

        /*********
        ** A bunch of Methods
        *********/
        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        /// 
        private void UpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (this.DebuggerMode)
            {
                if (Game1.mailbox.Count > 0)
                    this.MailboxMonitor();
                if (Game1.mailbox.Count == 0)
                    this.CheckMailBox = true;
                if (this.MailboxContent.Value != null && this.MailboxContent.Value.Length != Game1.mailbox.Count)
                    this.CheckMailBox = true;
                if (this.mailIDCount.Value != ((NetHashSet<string>)Game1.player.mailReceived).Count)
                {
                    if (this.mailIDCount.Value == 0)
                    {
                        this.OldFlags.Value = new HashSet<string>(Game1.player.mailReceived);
                        this.mailIDCount.Value = Game1.player.mailReceived.Count;
                        return;
                    }
                    this.MailIDMonitor();
                }
                if (this.responseCount.Value != ((NetHashSet<string>)Game1.player.mailReceived).Count)
                {
                    if (this.responseCount.Value == 0)
                    {
                        this.responseMon.Value = new HashSet<string>(Game1.player.dialogueQuestionsAnswered);
                        this.responseCount.Value = Game1.player.dialogueQuestionsAnswered.Count;
                        return;
                    }
                    this.ResponseMonitor();
                }
            }

            if (this.LastEvent is null && Game1.CurrentEvent is not null)
                this.OnEventStarted(Game1.CurrentEvent);

            if (this.EventRemovalTimer > 0)
            {
                this.EventRemovalTimer--;
                if (this.EventRemovalTimer <= 0)
                {
                    Game1.player.eventsSeen.Remove(eventtoskip);
                    Monitor.Log("Event removed from seen list", LogLevel.Debug);

                }
            }
        }

        public void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            /* if(e.Button == this.Config.EventWindow)
             {
                 if (Game1.activeClickableMenu == null)
                     Game1.activeClickableMenu = new EventRepeaterWindow(this.Helper.Data, this.Helper.DirectoryPath);
             }*/
        }

        public void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (this.Config.ShowInfo.JustPressed())
                ShowInfoCommand(null, null);

            if (Game1.CurrentEvent is null)
                return;


            if (this.Config.NormalSkip.JustPressed())
                EmergencySkipCommand(null, null);

            if (this.Config.EmergencySkip.JustPressed())
                StopEventCommand(null, null);
        }

        private void OnEventStarted(Event @event)
        {
            Monitor.Log($"Current Event: {Game1.CurrentEvent.id}", LogLevel.Debug);
            LastPlayed = Game1.CurrentEvent.id;

            if (ShowEventIDs)
            {
                Game1.addHUDMessage(new HUDMessage($"Current Event: {Game1.CurrentEvent.id}!"));
            }
            Game1.CurrentEvent.eventCommands = this.ExtractCommands(Game1.CurrentEvent.eventCommands, new[] { "forgetEvent", "forgetMail", "forgetResponse", "timeAdvance" }, out ISet<string> extractedCommands);

            foreach (string command in extractedCommands)
            {
                // extract command name + raw ID
                string commandName, rawId;
                {
                    string[] parts = command.Split(' ');
                    commandName = parts[0];
                    if (parts.Length != 2) // command name + ID
                    {
                        this.Monitor.Log($"The {commandName} command requires one argument (event command: {command}).", LogLevel.Warn);
                        continue;
                    }
                    rawId = parts[1];
                }

                // handle command
                switch (commandName)
                {
                    case "forgetEvent":
                        Game1.player.eventsSeen.Remove(rawId);
                        break;

                    case "forgetMail":
                        Game1.player.mailReceived.Remove(rawId);
                        break;

                    case "forgetResponse":
                        Game1.player.dialogueQuestionsAnswered.Remove(rawId);
                        break;

                    case "timeAdvance":
                        if (int.TryParse(rawId, out int hours))
                        {
                            int newTime = Utility.ModifyTime(Game1.timeOfDay, hours * 60);
                            if (newTime < 2600)
                            {
                                Game1.timeOfDay = newTime;
                            }
                            else
                                this.Monitor.Log($"Cannot advance time! {hours} would put the time to {newTime}!  Command ignored!", LogLevel.Error);
                        }
                        else
                            this.Monitor.Log($"Time advancement failed: invalid number '{rawId}'.", LogLevel.Warn);
                        break;

                    default:
                        this.Monitor.Log($"Unrecognized command name '{commandName}'.", LogLevel.Warn);
                        break;
                }
            }
        }

        private string[] ExtractCommands(string[] commands, string[] commandNamesToExtract, out ISet<string> extractedCommands)
        {
            var otherCommands = new List<string>(commands.Length);
            extractedCommands = new HashSet<string>();
            foreach (string command in commands)
            {
                if (commandNamesToExtract.Any(name => command.StartsWith(name)))
                    extractedCommands.Add(command);
                else
                    otherCommands.Add(command);
            }

            return otherCommands.ToArray();
        }


        [EventPriority(EventPriority.High + 1000)]
        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            bool removed = false;
            this.mailIDCount.Value = Game1.player.mailReceived.Count;

            if (this.EventsToForget.Count > 0 || this.ManualRepeaterList.Count > 0 || AssetManager.EventsToForget.Value.Count > 0)
            {
                removed = Game1.player.eventsSeen.RemoveWhere(id =>
                {
                    if (this.EventsToForget.Contains(id) /*|| AssetManager.EventsToForget.Value.Contains(id)*/)
                    {
                        this.Monitor.Log("Repeatable Event Found! Resetting for next time! Event ID: " + id);
                        return true;
                    }

                    //if (this.ManualRepeaterList.Contains(id))
                    //{
                    //    this.Monitor.Log("Manual Repeater Engaged! Resetting: " + id);
                    //    return true;
                    //}

                    return false;
                }) > 0;
            }

            if (!removed)
                this.Monitor.Log("No repeatable events were removed");

            var assetMail = Game1.content.Load<Dictionary<string, string>>(AssetManager.MailToRepeatName);

            removed = false;
            if (this.MailToForget.Count > 0 || assetMail.Count > 0)
            {
                removed = Game1.player.mailReceived.RemoveWhere(id =>
                {
                    if (this.MailToForget.Contains(id) || assetMail.ContainsKey(id))
                    {
                        this.Monitor.Log("Repeatable Mail found!  Resetting: " + id);
                        return true;
                    }

                    return false;
                }) > 0;
            }
            if (!removed)
                this.Monitor.Log("No repeatable mail found for removal.");

            removed = false;
            if (this.ResponseToForget.Count > 0 || AssetManager.ResponseToForget.Value.Count > 0)
            {
                removed = Game1.player.dialogueQuestionsAnswered.RemoveWhere(id =>
                {
                    if (this.ResponseToForget.Contains(id) || AssetManager.ResponseToForget.Value.Contains(id))
                    {
                        this.Monitor.Log("Repeatable Response Found! Resetting: " + id);
                        return true;
                    }

                    return false;
                }) > 0;
            }
            if (!removed)
                this.Monitor.Log("No repeatable responses found.");
        }

        private void ManualRepeater(string command, string[] parameters)
        {
            //This command will set a manual repeat to a list and save the event IDs to a file in the SDV folder.  
            //The first thing to do is create a list
            List<string> eventsSeenList = new List<string>(Game1.player.eventsSeen);

            //Check to see if an EventID was added in the command.. If not, then add the last ID on the list
            if (parameters.Length == 0)
            {
                try
                {
                    string lastEvent = eventsSeenList[eventsSeenList.Count - 1];//Count -1 to account for 0
                    Game1.player.eventsSeen.Remove(lastEvent);//Removes ID from events seen
                    ManualRepeaterList.Add(lastEvent);//Adds to the Manual List
                    Monitor.Log($"{lastEvent} has been added to Manual Repeater", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Monitor.Log(ex.Message, LogLevel.Warn);
                }
            }
            else
            {
                try
                {
                    ManualRepeaterList.Add(parameters[0]);
                    Game1.player.eventsSeen.Remove(parameters[0]);
                    Monitor.Log($"{parameters[0]} has been added to Manual Repeater", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Monitor.Log(ex.Message, LogLevel.Warn);
                }
            }
        }

        private void ShowInfoCommand(string? command, string[]? parameters)
        {
            if (ShowEventIDs)
            {
                ShowEventIDs = false;
                Game1.addHUDMessage(new HUDMessage("In Game Alerts Disabled!"));
            }
            else
            {
                ShowEventIDs = true;
                Game1.addHUDMessage(new HUDMessage("In Game Alerts Enabled!"));
            }
        }

        private void FastMailCommand(string command, string[] parameters)
        {
            if (parameters[0] != null)
                Game1.addMail(parameters[0]);
            else
                this.Monitor.Log("No Mail ID was added!", (LogLevel)4);
        }

        private void EmergencySkipCommand(string? command, string[]? parameters)
        {
            if (Game1.CurrentEvent is null)
                return;

            eventtoskip = Game1.CurrentEvent.id;

            try
            {
                Game1.CurrentEvent.skipEvent();
                Monitor.Log($"Event {eventtoskip} was successfully skipped!", LogLevel.Debug);
                if (ShowEventIDs)
                    Game1.addHUDMessage(new HUDMessage($"Event {eventtoskip} was successfully skipped!"));

            }
            catch (Exception ex)
            {
                Monitor.Log(ex.Message, LogLevel.Error);
            }
        }

        private void DebuggerCommand(string? command, string[]? parameters)
        {
            this.DebuggerMode = !this.DebuggerMode;
        }

        private void StopEventCommand(string? command, string[]? parameters)
        {
            if (Game1.CurrentEvent is null)
                return;

            eventtoskip = Game1.CurrentEvent.id;
            EventRemovalTimer = 120;
            try
            {
                string[] eventCommands = Game1.CurrentEvent.eventCommands;
                int stoppedCommand = Game1.CurrentEvent.currentCommand;
                Monitor.Log($"Emergency skip was engaged! Event Broke at this command: {eventCommands[stoppedCommand]}", LogLevel.Error);
                eventCommands[stoppedCommand] = eventCommands[stoppedCommand] + " <=== Event was stopped here!!";

                Game1.CurrentEvent.exitEvent();
                Game1.warpFarmer("FarmHouse", 0, 0, false);
                Monitor.Log($"The Event {eventtoskip} has been interrupted. A dump of the Event is in the SDV folder.");
                if (ShowEventIDs)
                {
                    Game1.addHUDMessage(new HUDMessage($"The Event {eventtoskip} has been interrupted.  A dump of the Event is in the SDV folder."));
                }
                File.WriteAllLines(Path.Combine(Environment.CurrentDirectory, $"EventDump{eventtoskip}.txt"), eventCommands);



            }
            catch (Exception ex)
            {
                Monitor.Log(ex.Message, LogLevel.Error);
            }
        }

        private void SaveManualCommand(string command, string[] parameters)
        {
            //This will allow you to save your repeatable events from the manual repeater
            //Create Directory
            Directory.CreateDirectory(Environment.CurrentDirectory + "\\ManualRepeaterFiles");
            string savePath = Environment.CurrentDirectory + "\\ManualRepeaterFiles\\" + parameters[0] + ".txt"; //Saves file in the name you designate
            string[] parse = ManualRepeaterList.ToArray(); //Converts the Manual list to a string array
            File.WriteAllLines(savePath, parse);
            Monitor.Log($"Saved file to {savePath}", LogLevel.Debug);
        }

        private void LoadCommand(string command, string[] parameters)
        {
            //This will allow you to load a saved manual repeater file
            //First Check to see if you have the Directory
            if (Directory.Exists(Environment.CurrentDirectory + "\\ManualRepeaterFiles"))
            {
                string loadPath = Environment.CurrentDirectory + "\\ManualRepeaterFiles\\" + parameters[0] + ".txt"; //loads the filename you choose
                
                //Save all strings to a List
                List<string> fileIds = new List<string>(File.ReadAllLines(loadPath));

                //Transfer all items to ManualList and convert to int
                foreach (string eventId in fileIds)
                {
                    ManualRepeaterList.Add(eventId);
                    Game1.player.eventsSeen.Remove(eventId);
                }
                Monitor.Log($"{parameters[0]} loaded!", LogLevel.Debug);
            }
        }

        private void ForgetManualCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0)
                return;

            try
            {
                switch (parameters[0])
                {
                    case "last":
                        if (string.IsNullOrEmpty(this.LastPlayed))
                        {
                            Monitor.Log("There is no previously played event.  Did you restart your game?", LogLevel.Error);
                            return;
                        }

                        Game1.player.eventsSeen.Remove(LastPlayed);
                        Monitor.Log($"Last played event, {LastPlayed}, was removed!", LogLevel.Debug);
                        if (ShowEventIDs)
                            Game1.addHUDMessage(new HUDMessage($"Last played event, {LastPlayed}, was removed!"));
                        break;

                    case "all":
                        Game1.player.eventsSeen.Clear();
                        Game1.player.eventsSeen.Add("60367");
                        Monitor.Log("All events removed! (Except the initial event)", LogLevel.Debug);
                        if (ShowEventIDs)
                            Game1.addHUDMessage(new HUDMessage("All events removed! (except the initial event)"));
                        break;

                    default:
                        Game1.player.eventsSeen.Remove(parameters[0]);
                        Monitor.Log("Forgetting event id: " + parameters[0], LogLevel.Debug);
                        if (ShowEventIDs)
                            Game1.addHUDMessage(new HUDMessage($"Forgetting event id: {parameters[0]}"));
                        break;
                }
            }
            catch (Exception ex)
            {
                Monitor.Log(ex.Message, LogLevel.Error);
            }
        }

        private void ShowEventsCommand(string command, string[] parameters)
        {
            string eventsSeen = "Events seen: " + string.Join(", ", Game1.player.eventsSeen);
            Monitor.Log(eventsSeen, LogLevel.Debug);
        }

        private void ShowMailCommand(string command, string[] parameters)
        {
            string mailSeen = "Mail Seen: " + string.Join(", ", Game1.player.mailReceived);
            Monitor.Log(mailSeen, LogLevel.Debug);
        }

        private void ForgetMailCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0)
                return;

            try
            {
                string mailToForget = parameters[0];
                Game1.player.mailReceived.Remove(mailToForget);
                this.OldFlags.Value = new HashSet<string>(Game1.player.mailReceived);
                Monitor.Log("Forgetting mail id: " + mailToForget, LogLevel.Debug);
                if (ShowEventIDs)
                    Game1.addHUDMessage(new HUDMessage($"Forgetting mail id: {mailToForget}"));

            }
            catch (Exception) { }
        }

        private void SendMailCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0)
                return;

            try
            {
                string mailtoSend = parameters[0];
                Game1.addMailForTomorrow(mailtoSend);
                Monitor.Log("Check Mail Tomorrow!! Sending: " + mailtoSend, LogLevel.Debug);
                if (ShowEventIDs)
                    Game1.addHUDMessage(new HUDMessage($"Check Mail Tomorrow!! Sending: {mailtoSend}"));

            }
            catch (Exception) { }
        }

        private void ShowResponseCommand(string command, string[] parameters)
        {
            string dialogueQuestionsAnswered = "Response IDs: " + string.Join(", ", Game1.player.dialogueQuestionsAnswered);
            Monitor.Log(dialogueQuestionsAnswered, LogLevel.Debug);
        }

        private void ForgetResponseCommand(string command, string[] parameters)
        {
            if (parameters.Length == 0)
                return;

            try
            {
                Game1.player.dialogueQuestionsAnswered.Remove(parameters[0]);
                this.responseMon.Value = new HashSet<string>(Game1.player.dialogueQuestionsAnswered);
                Monitor.Log("Forgetting Response ID: " + parameters[0], LogLevel.Debug);
                if (ShowEventIDs)
                    Game1.addHUDMessage(new HUDMessage($"Forgetting Response ID: {parameters[0]}"));

            }
            catch (Exception) { }
        }

        private void ResponseMonitor()
        {
            if (this.responseMon.Value != null && this.responseMon.Value.Count == Game1.player.dialogueQuestionsAnswered.Count)
                return;
            HashSet<string> stringSet = new HashSet<string>(Game1.player.dialogueQuestionsAnswered);
            if (this.responseMon.Value != null)
            {
                foreach (string str in this.responseMon.Value)
                {
                    if (!stringSet.Contains(str))
                        this.Monitor.Log("Response ID " + str + " has been removed from dialogueQuestionsAnswered!", (LogLevel)1);
                }
                foreach (string str in stringSet)
                {
                    if (!this.responseMon.Value.Contains(str))
                        this.Monitor.Log("Response ID " + str + " was added to dialogueQuestionsAnswered!", (LogLevel)1);
                }
            }
            this.responseMon.Value = stringSet;
        }

        private void MailboxMonitor()
        {
            if (Game1.mailbox.Count < 0 || !this.CheckMailBox)
                return;

            this.MailboxContent.Value = Game1.mailbox.ToArray();
            string text = "You have the following Mail waiting in your Mailbox: " + string.Join(", ", this.MailboxContent.Value);
            this.Monitor.Log(text, LogLevel.Debug);
            if (this.ShowEventIDs)
                Game1.addHUDMessage(new HUDMessage(text));
            this.CheckMailBox = false;
        }

        private void MailIDMonitor()
        {
            if (this.OldFlags.Value != null && this.OldFlags.Value.Count == Game1.player.mailReceived.Count)
                return;
            HashSet<string> stringSet = new HashSet<string>(Game1.player.mailReceived);
            if (this.OldFlags.Value != null)
            {
                foreach (string str in this.OldFlags.Value)
                {
                    if (!stringSet.Contains(str))
                        this.Monitor.Log("Mail ID " + str + " was removed from mailRecieved", (LogLevel)1);
                }
                foreach (string str in stringSet)
                {
                    if (!this.OldFlags.Value.Contains(str))
                        this.Monitor.Log("Mail ID " + str + " was added to mailRecieved", (LogLevel)1);
                }
            }
            this.OldFlags.Value = stringSet;
        }

        private void injectCommand(string command, string[] parameters)
        {
            // This will replace ResponseAdd in order to inject a more versitile code
            // function: inject <type> <ID> whereas type is event, response, mail
            // this will not have an indicator of existing events, however will look for the ID in the appropriate list.
            // 
            if (parameters.Length == 0)
                return;

            if (parameters.Length == 1)
            {
                if (parameters[0] == "response")
                {
                    Monitor.Log("No response ID entered.  Please input a response ID", LogLevel.Error);
                }
                if (parameters[0] == "mail")
                {
                    Monitor.Log("No mail ID entered.  Please input a mail ID", LogLevel.Error);
                }
                if (parameters[0] == "event")
                {
                    Monitor.Log("No event ID entered.  Please input a event ID", LogLevel.Error);
                }
            }
            if (parameters.Length == 2)
            {
                //check for existing IDs
                switch (parameters[0])
                {
                    case "event":
                        if (Game1.player.eventsSeen.Contains(parameters[1]))
                        {
                            Monitor.Log($"{parameters[1]} Already exists within seen events.", LogLevel.Warn);
                        }
                        else
                        {
                            Game1.player.eventsSeen.Add(parameters[1]);
                            Monitor.Log($"{parameters[1]} has been added to the seen events list.", LogLevel.Debug);
                        }
                        break;

                    case "response":
                        if (Game1.player.dialogueQuestionsAnswered.Contains(parameters[1]))
                        {
                            Monitor.Log($"{parameters[1]} Already exists within the response list.", LogLevel.Warn);
                        }
                        else
                        {
                            Game1.player.dialogueQuestionsAnswered.Add(parameters[1]);
                            Monitor.Log($"{parameters[1]} has been added to the response list.", LogLevel.Debug);
                        }
                        break;

                    case "mail":
                        if (Game1.player.mailReceived.Contains(parameters[1]))
                        {
                            Monitor.Log($"{parameters[1]} Already exists within seen events.", LogLevel.Warn);
                        }
                        else
                        {
                            Game1.player.mailReceived.Add(parameters[1]);
                            Monitor.Log($"{parameters[1]} has been added to the seen events list.", LogLevel.Debug);
                        }
                        break;
                }
            }
        }
    }
}
