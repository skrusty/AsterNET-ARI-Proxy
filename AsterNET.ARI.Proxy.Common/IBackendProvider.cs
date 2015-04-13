namespace AsterNET.ARI.Proxy.Common
{
	/// <summary>
	/// Handles communications with a backend provider, like Azure or RMQ
	/// </summary>
	public interface IBackendProvider
	{
		IDialogue CreateDialogue();
	}
}