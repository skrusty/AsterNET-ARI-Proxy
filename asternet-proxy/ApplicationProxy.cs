using System;
using System.Collections.Generic;
using AsterNET.ARI.Models;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;
using NLog;
using RestSharp;

namespace AsterNET.ARI.Proxy
{
	public class ApplicationProxy
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly IBackendProvider _provider;
		private readonly StasisEndpoint _endpoint;
		private readonly string _appName;
		private readonly Dictionary<string, IDialogue> _dialogues;
		private readonly AriClient _client;
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
		}

		private void _client_OnUnhandledEvent(IAriClient sender, Event eventMessage)
		{
			Logger.Trace("New Event: {0}", eventMessage.Type);
			IDialogue dialogue = null;

			// Catch all events and handle as required
			switch (eventMessage.Type.ToLower())
			{
				case "stasisstart":
					// Check for an existing dialogue setup for this, if none found, create a new one
					var startArgs = (StasisStartEvent)eventMessage;
					dialogue = _dialogues.ContainsKey(startArgs.Channel.Id)
						? _dialogues[startArgs.Channel.Id]
						: CreateNewDialogue(startArgs.Channel.Id);
					break;
				case "stasisend":
					// Application ended
					var endArgs = (StasisEndEvent)eventMessage;
					if (_dialogues.ContainsKey(endArgs.Channel.Id))
						dialogue = _dialogues[endArgs.Channel.Id];
					// Should we unregister this channel from the dialogue at this point?
					break;
				default:
					if (eventMessage.Type.ToLower().StartsWith("bridge"))
					{
						// Bridge related event
						var bridge = (Bridge)eventMessage.GetType().GetProperty("Bridge").GetValue(eventMessage);
						if (bridge != null)
							dialogue = GetDialogue(bridge.Id);
					}
					else if (eventMessage.Type.ToLower().StartsWith("channel"))
					{
						// Channel related event
						var channel = (Channel) eventMessage.GetType().GetProperty("Channel").GetValue(eventMessage);
						if (channel != null)
							dialogue = GetDialogue(channel.Id);
					}else if (eventMessage.Type.ToLower().StartsWith("recording"))
					{
						// Handle recordings
						var target = ((LiveRecording)eventMessage.GetType().GetProperty("Recording").GetValue(eventMessage)).Target_uri;
						if (target != null)
							dialogue = GetDialogue(target);
					}
					else if (eventMessage.Type.ToLower().StartsWith("playback"))
					{
						// Handle playbacks
						var target = ((Playback)eventMessage.GetType().GetProperty("Playback").GetValue(eventMessage)).Target_uri;
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

		private IDialogue GetDialogue(string id)
		{
			if (_dialogues.ContainsKey(id))
				return _dialogues[id];
			throw new MissingDialogueException();
		}

		private IDialogue CreateNewDialogue(string id)
		{
			var newDialogue = _provider.CreateDialogue(_appName);
			_dialogues[id] = newDialogue;	// Add to dialogues

			// Hook dialogue events
			newDialogue.OnNewCommandRequest += Dialogue_OnNewCommandRequest;
			newDialogue.OnDialogueDestroyed += Dialogue_OnDialogueDestroyed;

			Logger.Debug("Created new Dialogue {0}", newDialogue.DialogueId);
			Logger.Debug("Attached {0} to Dialogue {1}", id, newDialogue.DialogueId);

			return newDialogue;
		}

		private void Dialogue_OnDialogueDestroyed(object sender, EventArgs e)
		{
			// A dialogue's channels have been destroyed in the backend provider
			// We should deregister the dialogue in the application proxy
		}

		private void Dialogue_OnNewCommandRequest(object sender, Command e)
		{
			// Look for in-dialogue addition from OriginateWithId
			if (e.Method == "POST" && e.Url.StartsWith("/channel/"))
			{
				var newChanId = e.Url.Replace("/channel/", "");
				if (!string.IsNullOrEmpty(newChanId))
					_dialogues.Add(newChanId, (IDialogue)sender);
			}

			// Send command to ARI and wait for response
			var request = new RestRequest(e.Url, (Method) Enum.Parse(typeof (Method), e.Method));
			request.AddBody(e.Body);

			var result = _restClient.Execute(request);
			var rtn = new CommandResult()
			{
				UniqueId = e.UniqueId,
				StatusCode = (int)result.StatusCode,
				ResponseBody = result.Content
			};

			// Send back a new response message
			((IDialogue) sender).PushMessage(DialogueResponseMessage.Create(rtn));
		}

		public void Start()
		{
			_client.Connect();
		}

		public void Stop()
		{
			_client.Disconnect();
		}
	}

	internal class MissingDialogueException : Exception
	{
	}
}