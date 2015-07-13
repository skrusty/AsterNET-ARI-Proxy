using System;
using AsterNET.ARI.Proxy.Common.Messages;

namespace AsterNET.ARI.Proxy.Common
{
	/// <summary>
	/// Represents a complete dialogue between proxy and client
	/// </summary>
	public interface IDialogue
	{
		event EventHandler<Command> OnNewCommandRequest;
		event EventHandler OnDialogueDestroyed;
		Guid DialogueId { get; set; }
        DateTime Created { get; set; }
		void PushMessage(IDialogueMessage message);
	}
}