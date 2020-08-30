using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsterNET.ARI.Proxy.Common.Config.ConfigurationProviders;

namespace AsterNET.ARI.Proxy.Common.Config
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
        public bool CloseDialogueOnPrimaryStasisEnd { get; set; }
        public string BackendProvider { get; set; }
		public dynamic BackendConfig { get; set; }
        public APCoRConfig APCoR { get; set; }

        public string ConfigPath;

        public static ProxyConfig Load(string configPath)
        {
            return new JsonConfigurationProvider().LoadConfiguration<ProxyConfig>(configPath);
        }

        public void Save()
        {
            new JsonConfigurationProvider().SaveConfiguration<ProxyConfig>(this, ConfigPath);
        }
	}

    public class APCoRConfig
    {
        public string BindUri { get; set; }
    }
}
