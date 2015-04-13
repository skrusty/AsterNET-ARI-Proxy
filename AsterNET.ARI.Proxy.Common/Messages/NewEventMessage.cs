using System;
using Newtonsoft.Json;

namespace AsterNET.ARI.Proxy.Common.Messages
{
	public class NewEventMessage
	{
		[JsonProperty("server_id")]
		public string ServerId { get; set; }
		[JsonProperty("timestamp")]
		public DateTime Timestamp { get; set; }
		[JsonProperty("type")]
		public string Type { get; set; }
		[JsonProperty("ari_body")]
		public object AriBody { get; set; }
	}
}
