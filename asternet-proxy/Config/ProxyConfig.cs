using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsterNET.ARI.Proxy.Config
{
	public class ProxyConfig
	{
		public static ProxyConfig Current { get; set; }

		public string ServerId { get; set; }
		public string AriHostname { get; set; }
		public int AriPort { get; set; }
		public string AriUsername { get; set; }
		public string AriPassword { get; set; }
		public string Applications { get; set; }
		public string BackendProvider { get; set; }
		public dynamic BackendConfig { get; set; }
	}
}
