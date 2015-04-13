using System;
using System.Collections.Generic;
using AsterNET.ARI.Models;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;
using RestSharp;

namespace AsterNET.ARI.Proxy
{
	public class ApplicationProxy
	{
		private readonly IBackendProvider _provider;
		private readonly StasisEndpoint _endpoint;
		private readonly string _appName;
		private readonly Dictionary<string, IDialogue> _dialogues;
		private readonly AriClient _client;
		private RestClient _restClient;

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

			// Init event handler
			_client.OnUnhandledEvent += _client_OnUnhandledEvent;
		}

		private void _client_OnUnhandledEvent(IAriClient sender, Event eventMessage)
		{
			IDialogue dialogue = null;

			// Catch all events and handle as required
			switch (eventMessage.Type.ToLower())
			{
				case "statisstart":
					// Check for an existing dialogue setup for this, if none found, create a new one
					var startArgs = (StasisStartEvent)eventMessage;
					dialogue = _dialogues.ContainsKey(startArgs.Channel.Id)
						? _dialogues[startArgs.Channel.Id]
						: CreateNewDialogue(startArgs.Channel.Id);
					break;
				case "statisend":
					// Application ended
					
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
					}
					else
					{
						// Something else...
					}
					break;
			}
			if (dialogue != null)
			{
				// Get the dialogue channel to send this event out one
				dialogue.PushMessage(DialogueEventMessage.Create(eventMessage));
			}
			else
			{
				// unmatched dialogue
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

			return newDialogue;
		}

		private void Dialogue_OnDialogueDestroyed(object sender, EventArgs e)
		{
			
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