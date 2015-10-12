using System;
using AsterNET.ARI.Models;
using AsterNET.ARI.Proxy.Common.Messages;
using Newtonsoft.Json;

namespace AsterNET.ARI.Proxy.Common
{
	public class DialogueEventMessage : IDialogueMessage
	{
		public MessageType Type { get; set; }
		public NewEventMessage Body { get; set; }

		public static DialogueEventMessage Create(Event newEvent, string serverId)
		{
			return new DialogueEventMessage()
			{
				Type = MessageType.Event,
				Body = new NewEventMessage()
				{
					Type = newEvent.Type,
					Timestamp = DateTime.UtcNow,
					ServerId = serverId,
					AriBody = JsonConvert.SerializeObject(newEvent)
				}
			};
		}
	}
}