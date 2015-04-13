namespace AsterNET.ARI.Proxy.Common
{
	public enum MessageType
	{
		Event,
		Request,
		Response
	}

	public interface IDialogueMessage
	{
		MessageType Type { get; set; }

	}
}