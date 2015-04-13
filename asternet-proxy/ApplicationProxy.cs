using System;
using System.Collections.Generic;
using AsterNET.ARI.Models;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Messages;
using RestSharp;

namespace AsterNET.ARI.Proxy
{
	public enum DialogueRefernceType { Channel, Bridge }

	public class ApplicationProxy
	{
		private readonly IBackendProvider _provider;
		private readonly StasisEndpoint _endpoint;
		private readonly string _appName;
		private readonly Dictionary<Guid, IDialogue> _dialogues;
		private readonly Dictionary<Tuple<DialogueRefernceType, string>, Guid> _dialogueReferences;
		private readonly AriClient _client;
		private RestClient _restClient;

		public ApplicationProxy(IBackendProvider provider, StasisEndpoint endpoint, string appName)
		{
			_provider = provider;
			_endpoint = endpoint;
			_appName = appName;

			// Init
			_dialogues = new Dictionary<Guid, IDialogue>();
			_dialogueReferences = new Dictionary<Tuple<DialogueRefernceType, string>, Guid>();
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
			var dialogueId = Guid.Empty;

			// Catch all events and handle as required
			switch (eventMessage.Type.ToLower())
			{
				case "statisstart":
					// Check for an existing dialogue setup for this, if none found, create a new one
					var startArgs = (StasisStartEvent)eventMessage;
					dialogueId = _dialogueReferences.ContainsKey(new Tuple<DialogueRefernceType, string>(DialogueRefernceType.Channel, startArgs.Channel.Id))
						? _dialogueReferences[new Tuple<DialogueRefernceType, string>(DialogueRefernceType.Channel, startArgs.Channel.Id)]
						: CreateNewDialogue(startArgs.Channel.Id);
					break;
				case "statisend":
					// Application ended
					
					break;
				default:
					if (eventMessage.Type.ToLower().StartsWith("bridge"))
					{
						// Bridge related event
						var channel = (Bridge)eventMessage.GetType().GetProperty("Bridge").GetValue(eventMessage);
						if (channel != null)
							dialogueId = GetDialogue(DialogueRefernceType.Bridge, channel.Id);
					}
					else if (eventMessage.Type.ToLower().StartsWith("channel"))
					{
						// Channel related event
						var channel = (Channel) eventMessage.GetType().GetProperty("Channel").GetValue(eventMessage);
						if (channel != null)
							dialogueId = GetDialogue(DialogueRefernceType.Channel, channel.Id);
					}
					else
					{
						// Something else...
					}
					break;
			}
			if (dialogueId != Guid.Empty)
			{
				// Get the dialogue channel to send this event out one
				_dialogues[dialogueId].PushMessage(DialogueEventMessage.Create(eventMessage));
			}
			else
			{
				// unmatched dialogue
			}
		}

		private Guid GetDialogue(DialogueRefernceType type, string id)
		{
			Guid dialogueId;
			if (_dialogueReferences.ContainsKey(new Tuple<DialogueRefernceType, string>(type, id)))
				dialogueId = _dialogueReferences[new Tuple<DialogueRefernceType, string>(type, id)];
			else
				throw new MissingDialogueException();
			return dialogueId;
		}

		private Guid CreateNewDialogue(string id)
		{
			var newDialogue = _provider.CreateDialogue();
			_dialogues[newDialogue.DialogueId] = newDialogue;	// Add to dialogues
			_dialogueReferences[new Tuple<DialogueRefernceType, string>(DialogueRefernceType.Channel, id)] = newDialogue.DialogueId;	// Create channel reference

			// Hook dialogue events
			newDialogue.OnNewCommandRequest += Dialogue_OnNewCommandRequest;
			newDialogue.OnDialogueDestroyed += Dialogue_OnDialogueDestroyed;

			return newDialogue.DialogueId;
		}

		private void Dialogue_OnDialogueDestroyed(object sender, EventArgs e)
		{
			
		}

		private void Dialogue_OnNewCommandRequest(object sender, Command e)
		{
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