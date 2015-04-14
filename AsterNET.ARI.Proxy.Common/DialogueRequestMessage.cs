using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsterNET.ARI.Proxy.Common.Messages;

namespace AsterNET.ARI.Proxy.Common
{
	public class DialogueRequestMessage : IDialogueMessage
	{
		public MessageType Type { get; set; }
		public Command Body { get; set; }

		public static DialogueRequestMessage Create(Command cmd)
		{
			return new DialogueRequestMessage()
			{
				Type = MessageType.Request,
				Body = cmd
			};
		}
	}
}
