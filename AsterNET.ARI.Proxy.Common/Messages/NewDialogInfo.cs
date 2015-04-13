using Newtonsoft.Json;

namespace AsterNET.ARI.Proxy.Common.Messages
{
	/// <summary>
	///     This is the message sent by the client to inform them of a new
	///     channel entering the application. 
	/// </summary>
	public class NewDialogInfo
	{
		public string Application { get; set; }

		[JsonProperty("dialog_id")]
		public string DialogId { get; set; }

		[JsonProperty("server_id")]
		public string ServerId { get; set; }
	}
}