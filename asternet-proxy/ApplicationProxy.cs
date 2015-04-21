using System;
using System.Collections.Generic;
using System.Linq;
using AsterNET.ARI.Models;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using RestSharp;

namespace AsterNET.ARI.Proxy
{
	public class ApplicationProxy
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly string _appName;
		private readonly AriClient _client;
		private readonly Dictionary<string, IDialogue> _dialogues;
		private readonly StasisEndpoint _endpoint;
		private readonly IBackendProvider _provider;
		private readonly RestClient _restClient;

		public ApplicationProxy(IBackendProvider provider, StasisEndpoint endpoint, string appName)
		{
			_provider = provider;
			_endpoint = endpoint;
			_appName = appName;

			// Init
			_dialogues = new Dictionary<string, IDialogue>();
			_client = new AriClient(_endpoint, _appName);
			_restClient = new RestClient(_endpoint.AriEndPoint)
			{
				Authenticator = new HttpBasicAuthenticator(_endpoint.Username, _endpoint.Password)
			};

			// Register this app name with the backend provider
			_provider.RegisterApplication(_appName);

			// Init event handler
			_client.OnUnhandledEvent += _client_OnUnhandledEvent;
			_client.OnConnectionStateChanged += _client_OnConnectionStateChanged;
		}

		

		#region Private Methods
		private IDialogue GetDialogue(string id)
		{
			if (_dialogues.ContainsKey(id))
				return _dialogues[id];
			throw new MissingDialogueException();
		}

		private IDialogue CreateNewDialogue(string id)
		{
			var newDialogue = _provider.CreateDialogue(_appName);
			AddToDialogue(id, newDialogue);

			// Hook dialogue events
			newDialogue.OnNewCommandRequest += Dialogue_OnNewCommandRequest;
			newDialogue.OnDialogueDestroyed += Dialogue_OnDialogueDestroyed;

			Logger.Debug("Created new Dialogue {0}", newDialogue.DialogueId);
			Logger.Debug("Attached {0} to Dialogue {1}", id, newDialogue.DialogueId);

			return newDialogue;
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

		private void _client_OnUnhandledEvent(IAriClient sender, Event eventMessage)
		{
			Logger.Trace("New Event: {0}", eventMessage.Type);
			IDialogue dialogue = null;
			try
			{
				// Catch all events and handle as required
				switch (eventMessage.Type.ToLower())
				{
					case "stasisstart":
						// Check for an existing dialogue setup for this, if none found, create a new one
						var startArgs = (StasisStartEvent) eventMessage;
						dialogue = _dialogues.ContainsKey(startArgs.Channel.Id)
							? _dialogues[startArgs.Channel.Id]
							: CreateNewDialogue(startArgs.Channel.Id);
						break;
					case "stasisend":
						// Application ended
						var endArgs = (StasisEndEvent) eventMessage;
						if (_dialogues.ContainsKey(endArgs.Channel.Id))
							dialogue = _dialogues[endArgs.Channel.Id];
						// Should we unregister this channel from the dialogue at this point?
						break;
					default:
						if (eventMessage.Type.ToLower().StartsWith("bridge"))
						{
							// Bridge related event
							var bridge = (Bridge) eventMessage.GetType().GetProperty("Bridge").GetValue(eventMessage);
							if (bridge != null)
								dialogue = GetDialogue(bridge.Id);
						}
						else if (eventMessage.Type.ToLower().StartsWith("channel"))
						{
							// Channel related event
							var channel = (Channel) eventMessage.GetType().GetProperty("Channel").GetValue(eventMessage);
							if (channel != null)
								dialogue = GetDialogue(channel.Id);
						}
						else if (eventMessage.Type.ToLower().StartsWith("recording"))
						{
							// Handle recordings
							var target =
								((LiveRecording) eventMessage.GetType().GetProperty("Recording").GetValue(eventMessage)).Target_uri.Replace(
									"channel:", "").Replace("bridge:", "");
							if (target != null)
								dialogue = GetDialogue(target);
						}
						else if (eventMessage.Type.ToLower().StartsWith("playback"))
						{
							// Handle playbacks
							var target =
								((Playback) eventMessage.GetType().GetProperty("Playback").GetValue(eventMessage)).Target_uri.Replace(
									"channel:", "").Replace("bridge:", "");
							if (target != null)
								dialogue = GetDialogue(target);
						}
						else if (eventMessage.Type.ToLower().StartsWith("dial"))
						{
							// Handle dial events
							// Not 100% sure how this should be done as yet
						}
						else
						{
							// Something else...
							Logger.Warn("Unknown event type {0}", eventMessage.Type);
						}
						break;
				}
				if (dialogue != null)
				{
					Logger.Trace("Pushing message {0} to dialogue {1}", eventMessage.Type, dialogue.DialogueId);
					// Get the dialogue channel to send this event out one
					dialogue.PushMessage(DialogueEventMessage.Create(eventMessage));
				}
				else
				{
					// unmatched dialogue
					Logger.Warn("Unmatched dialogue for event {0}", eventMessage.Type);
				}
			}
			catch (Exception ex)
			{
				Logger.Error("An error occurred while matching the event to a dialogue", ex);
			}
		}
		private void Dialogue_OnDialogueDestroyed(object sender, EventArgs e)
		{
			// A dialogue's channels have been destroyed in the backend provider
			// We should deregister the dialogue in the application proxy
		}

		private void Dialogue_OnNewCommandRequest(object sender, Command e)
		{
			Logger.Debug("New Command on Dialogue {0}: Uri: {1}, Method: {2}, Body: {3}", ((IDialogue)sender).DialogueId, e.Url,
				e.Method, e.Body);
			// Look for in-dialogue addition from OriginateWithId
			if (e.Method == "POST" && e.Url.StartsWith("/channels/") && e.Url.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries).Count() == 2)
			{
				var newChanId = e.Url.Replace("/channels/", "");
				if (!string.IsNullOrEmpty(newChanId))
					AddToDialogue(newChanId, (IDialogue)sender);
			}

			// Send command to ARI and wait for response
			var request = new RestRequest(e.Url, (Method)Enum.Parse(typeof(Method), e.Method));
			if (e.Method == "GET" && e.Body.Length > 2)
			{
				var body = (JObject)JsonConvert.DeserializeObject(e.Body);
				foreach (var p in body.Children().OfType<JProperty>())
				{
					request.AddParameter(p.Name, p.Value);
				}
			}
			else
			{
				request.AddParameter("application/json", e.Body, ParameterType.RequestBody);
			}

			var response = _restClient.Execute(request);

			// Create CommandResult
			var rtn = new CommandResult
			{
				UniqueId = e.UniqueId,
				StatusCode = (int)response.StatusCode,
				ResponseBody = response.Content
			};

			// Check result for new Id
			if (rtn.ResponseBody.Length > 0)
			{
				var responseObj = JsonConvert.DeserializeObject<JObject>(rtn.ResponseBody);
				if (responseObj["id"]!=null)
					AddToDialogue((string)responseObj["id"], (IDialogue)sender);
				//if (responseObj["name"] != null)
				//	AddToDialogue((string)responseObj["name"], (IDialogue)sender);
			}

			// Send back a new response message
			((IDialogue)sender).PushMessage(DialogueResponseMessage.Create(rtn));
		} 
		#endregion

		#region Public Methods
		public void Start()
		{
			_client.Connect();
		}

		public void Stop()
		{
			_client.Disconnect();
		} 
		#endregion
	}

	internal class MissingDialogueException : Exception
	{
	}
}