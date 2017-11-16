using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterNET.ARI.Models;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Config;
using AsterNET.ARI.Proxy.Common.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using RestSharp;
using RestSharp.Authenticators;

namespace AsterNET.ARI.Proxy
{
    public class ApplicationProxy
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static List<ApplicationProxy> Instances = new List<ApplicationProxy>();
        private readonly AriClient _client;
        public readonly ConcurrentDictionary<string, IDialogue> _dialogues;
        private readonly StasisEndpoint _endpoint;
        private readonly IBackendProvider _provider;
        private readonly RestClient _restClient;
        public readonly string AppName;
        public List<IDialogue> ActiveDialogues;
        public DateTime Created;

        private ApplicationProxy(IBackendProvider provider, StasisEndpoint endpoint, string appName)
        {
            _provider = provider;
            _endpoint = endpoint;
            AppName = appName;
            Created = DateTime.Now;

            // Init
            _dialogues = new ConcurrentDictionary<string, IDialogue>();
            _client = new AriClient(_endpoint, AppName)
            {
                EventDispatchingStrategy = EventDispatchingStrategy.DedicatedThread
            };

            _restClient = new RestClient(_endpoint.AriEndPoint)
            {
                Authenticator = new HttpBasicAuthenticator(_endpoint.Username, _endpoint.Password)
            };
            ActiveDialogues = new List<IDialogue>();
            // Register this app name with the backend provider
            _provider.RegisterApplication(AppName);

            // Init event handler
            _client.OnUnhandledEvent += _client_OnUnhandledEvent;
            _client.OnConnectionStateChanged += _client_OnConnectionStateChanged;
        }

        #region Private Methods

        private IDialogue GetDialogue(string id)
        {
            return _dialogues.ContainsKey(id) ? _dialogues[id] : null;
        }

        private IDialogue CreateNewDialogue(string id)
        {
            try
            {
                var newDialogue = _provider.CreateDialogue(AppName);
                newDialogue.Created = DateTime.UtcNow;
                newDialogue.PrimaryDialogueChannel = id;

                // Activate dialogue
                ActiveDialogues.Add(newDialogue);
                AddToDialogue(id, newDialogue);

                // Hook dialogue events
                newDialogue.OnNewCommandRequest += Dialogue_OnNewCommandRequest;
                newDialogue.OnDialogueDestroyed += Dialogue_OnDialogueDestroyed;

                Logger.Debug("Created new Dialogue {0}", newDialogue.DialogueId);
                Logger.Debug("Attached {0} to Dialogue {1}", id, newDialogue.DialogueId);

                return newDialogue;
            }
            catch (Exception ex)
            {
                // Failed to create new Dialogue
                throw new DialogueException(ex.Message, ex);
            }
        }

        private void AddToDialogue(string newId, IDialogue dialogue)
        {
            Logger.Info("Attaching ID {0} to Dialogue {1}", newId, dialogue.DialogueId);
            if (!_dialogues.ContainsKey(newId))
                _dialogues[newId] = dialogue;
            else
                Logger.Warn("Unable to attached ID {0} as it's already assigned to a dialogue", newId);
        }

        #endregion

        #region Event Handlers

        private void _client_OnConnectionStateChanged(object sender)
        {
            Logger.Warn("ARI connection state changed for {0} to {1}", _endpoint.Host,
                _client.Connected ? "Connected" : "Disconnected");
        }

        private void _client_OnUnhandledEvent(object sender, Event eventMessage)
        {
            Logger.Trace("New Event: {0}", eventMessage.Type);
            var dialogueMatchId = string.Empty;
            bool deleteDialogue = false;
            try
            {
                // Catch all events and handle as required
                switch (eventMessage.Type.ToLower())
                {
                    case "stasisstart":
                        // Check for an existing dialogue setup for this, if none found, create a new one
                        var startArgs = (StasisStartEvent) eventMessage;
                        if (_dialogues.ContainsKey(startArgs.Channel.Id))
                            dialogueMatchId = startArgs.Channel.Id;
                        else
                        {
                            CreateNewDialogue(startArgs.Channel.Id);
                            dialogueMatchId = startArgs.Channel.Id;
                        }
                        break;
                    case "stasisend":
                        // Application ended
                        var endArgs = (StasisEndEvent) eventMessage;
                        dialogueMatchId = endArgs.Channel.Id;

                        // Should we unregister this channel from the dialogue at this point?

                        // Once this event has been dispatched to the queue, we should mark it for deletion
                        // if this was the primary channel for stasis start event and we have set CloseDialogueOnPrimaryStasisEnd
                        if (ProxyConfig.Current.CloseDialogueOnPrimaryStasisEnd &&
                            GetDialogue(dialogueMatchId).PrimaryDialogueChannel == dialogueMatchId)
                        {
                            Logger.Debug("Found closing event for dialogue {0} with primary id {1}", dialogueMatchId,
                                GetDialogue(dialogueMatchId).DialogueId);
                            deleteDialogue = true;
                        }
                        break;
                    default:
                        if (eventMessage.Type.ToLower().StartsWith("bridge"))
                        {
                            // Bridge related event
                            var bridge = (Bridge) eventMessage.GetType().GetProperty("Bridge").GetValue(eventMessage);
                            if (bridge != null)
                                dialogueMatchId = bridge.Id;
                        }
                        else if (eventMessage.Type.ToLower().StartsWith("channel"))
                        {
                            // Channel related event
                            var channel = (Channel) eventMessage.GetType().GetProperty("Channel").GetValue(eventMessage);
                            if (channel != null)
                                dialogueMatchId = channel.Id;
                        }
                        else if (eventMessage.Type.ToLower().StartsWith("recording"))
                        {
                            // Handle recordings
                            var target =
                                ((LiveRecording) eventMessage.GetType().GetProperty("Recording").GetValue(eventMessage))
                                    .Target_uri.Replace(
                                        "channel:", "").Replace("bridge:", "");
                            if (target != null)
                                dialogueMatchId = target;
                        }
                        else if (eventMessage.Type.ToLower().StartsWith("playback"))
                        {
                            // Handle playbacks
                            var target =
                                ((Playback) eventMessage.GetType().GetProperty("Playback").GetValue(eventMessage)).Id;
                            if (target != null)
                                dialogueMatchId = target;
                        }
                        else if (eventMessage.Type.ToLower().StartsWith("dial"))
                        {
                            // Handle dial events
                            var target =
                                ((DialEvent)eventMessage.GetType().GetProperty("Caller").GetValue(eventMessage)).Caller;
                        }
                        else
                        {
                            // Something else...
                            Logger.Warn("Unknown event type {0}", eventMessage.Type);
                        }
                        break;
                }

                var dialogue = GetDialogue(dialogueMatchId);
                if (dialogue != null)
                {
                    Logger.Trace("Pushing message {0} to dialogue {1}", eventMessage.Type, dialogue.DialogueId);
                    // Get the dialogue channel to send this event out one
                    dialogue.PushMessage(DialogueEventMessage.Create(eventMessage, ProxyConfig.Current.ServerId));

                    // Was the dialogue marked for deletion
                    if (!deleteDialogue) return;
                    Logger.Info("Dialogue {0} marked for deletion", dialogueMatchId);
                    dialogue.Close();
                }
                else
                {
                    // unmatched dialogue
                    Logger.Warn("Unmatched dialogue for event {0} with id {1}", eventMessage.Type, dialogueMatchId);
                }
            }
            catch (DialogueException dex)
            {
                Logger.Error($"Unable to create new dialogue. {dex.Message}", dex);
                // Should only happen when we start a new dialogue
                // Set channel variables regarding the status of proxy and failure reason
                var startArgs = (StasisStartEvent)eventMessage;
                ((AriClient) sender).Channels.SetChannelVar(startArgs.Channel.Id, "PROXYRESULT", "FAILED");
                ((AriClient) sender).Channels.SetChannelVar(startArgs.Channel.Id, "PROXYRESULT_REASON", dex.Message);
                ((AriClient) sender).Channels.ContinueInDialplan(startArgs.Channel.Id);
            }
            catch (Exception ex)
            {
                Logger.Error("An error occurred while matching the event to a dialogue (" + ex.Message + ")", ex);
            }
        }

        private void Dialogue_OnDialogueDestroyed(object sender, EventArgs e)
        {
            DeleteDialogue((IDialogue) sender);
        }

        private void DeleteDialogue(IDialogue sender)
        {
            // A dialogue's channels have been destroyed in the backend provider
            // We should deregister the dialogue in the application proxy
            Logger.Debug("Dialogue {0} has been destroyed", sender.DialogueId);
            foreach (var d in _dialogues.Where(x => x.Value == sender).ToList())
            {
                IDialogue tryout = null;
                if (!_dialogues.TryRemove(d.Key, out tryout))
                    Logger.Warn("Unable to remove Dialogue match {0} {1}", d.Key, d.Value.DialogueId);
            }

            // Remove from Active Dialogues
            ActiveDialogues.RemoveAll(x => x.DialogueId == sender.DialogueId);
        }

        private void Dialogue_OnNewCommandRequest(object sender, Command e)
        {
            Logger.Debug("New Command on Dialogue {0}: Uri: {1}, Method: {2}, Body: {3}",
                ((IDialogue) sender).DialogueId, e.Url,
                e.Method, e.Body);
            // Look for in-dialogue addition from OriginateWithId
            if (e.Method == "POST" && e.Url.StartsWith("/channels/") &&
                e.Url.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries).Count() == 2)
            {
                var newChanId = e.Url.Replace("/channels/", "");
                if (!string.IsNullOrEmpty(newChanId))
                    AddToDialogue(newChanId, (IDialogue) sender);
            }

            // Send command to ARI and wait for response
            var request = new RestRequest(e.Url, (Method) Enum.Parse(typeof (Method), e.Method));
            if ((e.Method == "GET" && e.Body.Length > 2))
            {
                var body = (JObject) JsonConvert.DeserializeObject(e.Body);
                foreach (var p in body.Children().OfType<JProperty>())
                {
                    request.AddParameter(p.Name, p.Value);
                }
            }
            else
            {
                var body = (JObject) JsonConvert.DeserializeObject(e.Body);
                if (body != null)
                {
                    if (body["playbackId"] != null)
                        AddToDialogue((string) body["playbackId"], (IDialogue) sender);
                    request.AddParameter(
                        "application/json",
                        e.Body.Replace(":\"True\"", ":true").Replace(":\"False\"", ":false"),
                        // Asterisk doesn't like bool with quotes
                        ParameterType.RequestBody);
                }
            }

            var response = _restClient.Execute(request);

            // Create CommandResult
            var rtn = new CommandResult
            {
                UniqueId = e.UniqueId,
                StatusCode = (int) response.StatusCode,
                ResponseBody = response.Content
            };

            // Check result for new Id
            if (rtn.ResponseBody.Length > 0)
            {
                var responseObj = JsonConvert.DeserializeObject<JObject>(rtn.ResponseBody);
                if (responseObj["id"] != null)
                    AddToDialogue((string) responseObj["id"], (IDialogue) sender);
                //if (responseObj["name"] != null)
                //	AddToDialogue((string)responseObj["name"], (IDialogue)sender);
            }

            // Send back a new response message
            ((IDialogue) sender).PushMessage(DialogueResponseMessage.Create(rtn));
        }

        #endregion

        #region Public Methods

        public void Start()
        {
            _client.Connect();
        }

        public void Stop()
        {
            Logger.Info("Disconnecting proxy from {0}", AppName);
            if(_client.ConnectionState == Middleware.ConnectionState.Open)
                _client.Disconnect();
        }

        public void DeleteDialogue(string Id)
        {
            var dialogue = ActiveDialogues.SingleOrDefault(x => x.DialogueId == Guid.Parse(Id));
            if (dialogue == null)
                return;
            DeleteDialogue(dialogue);
        }

        public static ApplicationProxy Create(IBackendProvider provider, StasisEndpoint endpoint, string appName)
        {
            Logger.Info("Starting Application Proxy for {0}", appName);

            var rtn = new ApplicationProxy(provider, endpoint, appName);
            Instances.Add(rtn);
            rtn.Start();

            return rtn;
        }

        public static void Terminate(ApplicationProxy proxy)
        {
            proxy.Stop();
            Instances.Remove(proxy);
        }

        #endregion
    }

    internal class DialogueException : Exception
    {
        public DialogueException(string message, Exception exception) : base(message, exception)
        {
            
        }
    }

    internal class MissingDialogueException : Exception
    {
    }
}