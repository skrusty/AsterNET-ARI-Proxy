using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsterNET.ARI.Proxy.Config.ConfigurationProviders;

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
		public List<string> Applications { get; set; }
		public string BackendProvider { get; set; }
		public dynamic BackendConfig { get; set; }
        public APCoRConfig APCoR { get; set; }

        public static ProxyConfig Load()
        {
            return new JsonConfigurationProvider().LoadConfiguration<ProxyConfig>("config");
        }

        public void Save()
        {
            new JsonConfigurationProvider().SaveConfiguration<ProxyConfig>(this, "config");
        }
	}

    public class APCoRConfig
    {
        public string BindUri { get; set; }
    }
}
