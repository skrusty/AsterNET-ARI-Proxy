namespace AsterNET.ARI.Proxy.Config
{
    public interface IConfigurationProvider
    {
        void SaveConfiguration<T>(object configObject, string configName);
        T LoadConfiguration<T>(string configName) where T : class;
    }
}