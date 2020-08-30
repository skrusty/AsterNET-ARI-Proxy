using System;
using System.IO;
using System.Threading;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Config;
using AsterNET.ARI.Proxy.Providers.RabbitMQ;
using CommandLine;
using Nancy.Hosting.Self;
using NLog;

namespace AsterNET.ARI.Proxy
{

    public class Options
    {
        [Option('c', "config", Required = false, HelpText = "Location of configuration file")]
        public string ConfigFile { get; set; }
    }

    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly ManualResetEvent _quitEvent = new ManualResetEvent(false);
        private static NancyHost _restHost;

        private static void Main(string[] args)
        {
            Logger.Info("Starting Ari Proxy");

            // Parse options
            var options = new Options();
            if(!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                // Unable to parse command line options
                Console.WriteLine("Failed to parse command line options");
                return;
            }

            // Load config
            ProxyConfig.Current = ProxyConfig.Load(Path.Combine(options.ConfigFile, "config"));

            try
            {
                // Init
                using (
                    BackendProvider.Current =
                        CreateProvider(ProxyConfig.Current.BackendProvider, ProxyConfig.Current.BackendConfig))
                {
                    LoadAPCoRs();
                    LoadAppProxies();

                    Logger.Info("Load complete");

                    Console.CancelKeyPress += Console_CancelKeyPress;

                    // Wait for exit
                    _quitEvent.WaitOne();

                    Console.CancelKeyPress -= Console_CancelKeyPress;
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal("Something went wrong... " + ex.Message, ex);
            }
        }

        private static void LoadAppProxies()
        {
            Logger.Info("Loading Application Proxies");
            // Load Applicaton Proxies
            foreach (var app in ProxyConfig.Current.Applications)
            {
                Logger.Debug("Starting Proxy for " + app);
                try
                {
                    var appProxy = ApplicationProxy.Create(BackendProvider.Current,
                        new StasisEndpoint(ProxyConfig.Current.AriHostname, ProxyConfig.Current.AriPort,
                            ProxyConfig.Current.AriUsername,
                            ProxyConfig.Current.AriPassword), app.Trim());
                }catch(Exception ex)
                {
                    Logger.Fatal(ex, $"AppProxy failed. {ex.Message}");
                }
            }
        }

        private static void LoadAPCoRs()
        {
            // Load APCoRs if required
            if (ProxyConfig.Current.APCoR != null)
            {
                Logger.Info("Starting APCoR");
                _restHost = new NancyHost(new Uri(ProxyConfig.Current.APCoR.BindUri));
                _restHost.Start();
            }
        }

        private static RabbitMqProvider CreateProvider(string providerId, dynamic config)
        {
            switch (providerId)
            {
                case "rmq":
                    var rmqConfig = RabbitMqBackendConfig.Create(config);
                    var provider = new RabbitMqProvider(rmqConfig);
                    return provider;
                default:
                    throw new Exception("Unknown Provider");
            }
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Logger.Info("Closing Application Proxies");
            // Terminate Proxy		
            ApplicationProxy.Instances.ForEach(x => { x.Stop(); });

            Logger.Info("Stopping APCoR");
            _restHost.Stop();
        }
    }

    public class BackendProvider
    {
        public static IBackendProvider Current { get; set; }
    }
}