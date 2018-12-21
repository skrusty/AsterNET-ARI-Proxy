using System;
using System.Threading;
using AsterNET.ARI.Proxy.Common;
using AsterNET.ARI.Proxy.Common.Config;
using AsterNET.ARI.Proxy.Providers.RabbitMQ;
using Nancy.Hosting.Self;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace AsterNET.ARI.Proxy
{
    internal class Program
    {
        public static ILoggerFactory LogFactory;
        private static void Main(string[] args)
        {
            SetupLogging();
            var log = LogFactory.CreateLogger<Program>();

            var runner = new Runner(LogFactory.CreateLogger<Runner>());
        }

        private static void SetupLogging()
        {
            LogFactory = new LoggerFactory();
            LogFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            LogFactory.ConfigureNLog("NLog.config");
        }
    }

    public class Runner
    {
        private readonly ManualResetEvent _quitEvent = new ManualResetEvent(false);
        private NancyHost _restHost;
        private ILogger<Runner> log;

        public Runner(ILogger<Runner> logger)
        {
            log = logger;

            log.LogInformation("Starting Ari Proxy");

            // Load config
            ProxyConfig.Current = ProxyConfig.Load();
            try
            {
                // Init
                using (
                    BackendProvider.Current =
                        CreateProvider(ProxyConfig.Current.BackendProvider, ProxyConfig.Current.BackendConfig))
                {
                    LoadAPCoRs(log);
                    LoadAppProxies(log);

                    log.LogInformation("Load complete");

                    Console.CancelKeyPress += Console_CancelKeyPress;

                    // Wait for exit
                    _quitEvent.WaitOne();

                    Console.CancelKeyPress -= Console_CancelKeyPress;
                }
            }
            catch (Exception ex)
            {
                log.LogCritical("Something went wrong... " + ex.Message, ex);
            }
        }

        private void LoadAppProxies(Microsoft.Extensions.Logging.ILogger log)
        {
            log.LogInformation("Loading Application Proxies");
            // Load Applicaton Proxies
            foreach (var app in ProxyConfig.Current.Applications)
            {
                log.LogDebug("Starting Proxy for " + app);
                var appProxy = ApplicationProxy.Create(BackendProvider.Current,
                    new StasisEndpoint(ProxyConfig.Current.AriHostname, ProxyConfig.Current.AriPort,
                        ProxyConfig.Current.AriUsername,
                        ProxyConfig.Current.AriPassword), app.Trim(), log);
            }
        }

        private void LoadAPCoRs(Microsoft.Extensions.Logging.ILogger log)
        {
            // Load APCoRs if required
            if (ProxyConfig.Current.APCoR != null)
            {
                log.LogInformation("Starting APCoR");
                _restHost = new NancyHost(new Uri(ProxyConfig.Current.APCoR.BindUri));
                _restHost.Start();
            }
        }

        private RabbitMqProvider CreateProvider(string providerId, dynamic config)
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

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            log.LogInformation("Closing Application Proxies");
            // Terminate Proxy		
            ApplicationProxy.Instances.ForEach(x => { x.Stop(); });

            log.LogInformation("Stopping APCoR");
            _restHost.Stop();
        }
    }

    public class BackendProvider
    {
        public static IBackendProvider Current { get; set; }
    }
}