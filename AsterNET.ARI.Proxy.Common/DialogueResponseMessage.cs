using AsterNET.ARI.Proxy.Common.Messages;

namespace AsterNET.ARI.Proxy.Common
{
	public class DialogueResponseMessage : IDialogueMessage
	{
		public MessageType Type { get; set; }

		public CommandResult Body { get; set; }

		public static DialogueResponseMessage Create(CommandResult response)
		{
			return new DialogueResponseMessage()
			{
				Type = MessageType.Response,
				Body = response
			};
		}
	}
}