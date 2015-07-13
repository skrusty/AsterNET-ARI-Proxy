using Nancy;
using AsterNET.ARI.Proxy.Config;

namespace AsterNET.ARI.Proxy.APCoR
{
    public class ConfigModule : NancyModule
    {
        public ConfigModule() : base("/v1/applications")
        {
            Post["/save"] = _ =>
            {
                // Commit the config back to disk
                ProxyConfig.Current.Save();
                return HttpStatusCode.OK;
            };

            Post["/reload"] = _ =>
            {
                // Currently just reloads config, needs to re-init
                ProxyConfig.Current = ProxyConfig.Load();

                return HttpStatusCode.OK;
            };
        }
    }
}
