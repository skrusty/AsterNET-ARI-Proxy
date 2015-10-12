using System;
using System.Linq;
using AsterNET.ARI.Proxy.APCoR.Models;
using AsterNET.ARI.Proxy.Config;
using Nancy;

namespace AsterNET.ARI.Proxy.APCoR
{
    public class ApplicationsModule : NancyModule
    {
        public ApplicationsModule() : base("/v1/applications")
        {
            Get[""] = _ => (from x in ApplicationProxy.Instances
                select new ApplicationModel()
                {
                    Created = x.Created,
                    Name = x.AppName,
                    DialogueCount = x.ActiveDialogues.Count
                }).ToList();

            Post["/{name}"] = args =>
            {
                // Check it doesn't already exist
                if (ApplicationProxy.Instances.Any(x => x.AppName == args.name))
                    return HttpStatusCode.BadRequest;

                // Add to configuration (doesn't commit)
                ProxyConfig.Current.Applications.Add(args.name);

                // Create new instance
                ApplicationProxy newApp = ApplicationProxy.Create(BackendProvider.Current,
                   new StasisEndpoint(ProxyConfig.Current.AriHostname, ProxyConfig.Current.AriPort, ProxyConfig.Current.AriUsername,
                       ProxyConfig.Current.AriPassword), args.name);

                return HttpStatusCode.OK;
            };

            Delete["/{name}"] = args =>
            {
                var app = ApplicationProxy.Instances.SingleOrDefault(x => x.AppName == args.name);
                if (app == null)
                    return HttpStatusCode.NotFound;

                // Stop App
                ApplicationProxy.Terminate(app);

                return HttpStatusCode.OK;
            };

            Get["/{name}/dialogues"] = args =>
            {
                var app = ApplicationProxy.Instances.SingleOrDefault(x => x.AppName == args.name);
                if (app == null)
                    return HttpStatusCode.NotFound;

                return (from d in app.ActiveDialogues
                        select new DialogueModel()
                        {
                            Created = d.Created,
                            Id = d.DialogueId.ToString(),
                            Application = args.name,
                            MsgCount = 0
                        }).ToList();
            };
        }
    }
}
