using System;
using System.IO;
using Newtonsoft.Json;

namespace AsterNET.ARI.Proxy.Common.Config.ConfigurationProviders
{
    public class JsonConfigurationProvider : IConfigurationProvider
    {
        public void SaveConfiguration<T>(object configObject, string configName)
        {
            lock (configObject)
            {
                var json = JsonConvert.SerializeObject(configObject, typeof (T), new JsonSerializerSettings());
                using (var fs = File.Open(configName + ".json", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(json);
                    }
                }
            }
        }

        public T LoadConfiguration<T>(string configPath) where T : class
        {
            // Load Config File

            try
            {
                using (var fs = File.OpenRead(configPath + ".json"))
                {
                    using (var sr = new StreamReader(fs))
                    {
                        var json = sr.ReadToEnd();
                        var rtn = JsonConvert.DeserializeObject<T>(json);
                        

                        return rtn;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}