﻿using System.Reflection;
using System.Web;
using XRoadLib.Extensions;
using XRoadLib.Header;

namespace XRoadLib.Handler
{
    public abstract class ServiceDescriptionHandler : ServiceHandlerBase
    {
        protected readonly uint? version;
        protected readonly Assembly contractAssembly;
        protected readonly XRoadProtocol protocol;
        protected readonly string producerName;

        protected virtual string EnvironmentProducerName => producerName;

        protected ServiceDescriptionHandler(Assembly contractAssembly, XRoadProtocol protocol, uint? version = null)
        {
            this.contractAssembly = contractAssembly;
            this.protocol = protocol;
            this.version = version;

            producerName = contractAssembly.GetProducerName();
        }

        protected override void HandleRequest(HttpContext context)
        {
            var definition = new ProducerDefinition(contractAssembly, protocol, version, EnvironmentProducerName);

            OnPrepareHeaders(definition);
            OnPrepareDefinition(definition);

            definition.SaveTo(context.Response.OutputStream);
        }

        protected virtual void OnPrepareHeaders(ProducerDefinition definition)
        {
            if (protocol == XRoadProtocol.Version20)
            {
                definition.AddHeader(x => ((IXRoadHeader20)x).Asutus);
                definition.AddHeader(x => ((IXRoadHeader20)x).Andmekogu);
                definition.AddHeader(x => ((IXRoadHeader20)x).Nimi);
                definition.AddHeader(x => ((IXRoadHeader20)x).Isikukood);
                definition.AddHeader(x => ((IXRoadHeader20)x).Id);
                definition.AddHeader(x => ((IXRoadHeader20)x).AmetnikNimi);
            }

            if (protocol == XRoadProtocol.Version31)
            {
                definition.AddHeader(x => ((IXRoadHeader31)x).Consumer);
                definition.AddHeader(x => ((IXRoadHeader31)x).Producer);
                definition.AddHeader(x => ((IXRoadHeader31)x).Service);
                definition.AddHeader(x => ((IXRoadHeader31)x).UserId);
                definition.AddHeader(x => ((IXRoadHeader31)x).Id);
                definition.AddHeader(x => ((IXRoadHeader31)x).UserName);
            }

            if (protocol == XRoadProtocol.Version40)
            {
                definition.AddHeader(x => ((IXRoadHeader40)x).Client);
                definition.AddHeader(x => ((IXRoadHeader40)x).Service);
                definition.AddHeader(x => ((IXRoadHeader40)x).UserId);
                definition.AddHeader(x => ((IXRoadHeader40)x).Id);
                definition.AddHeader(x => ((IXRoadHeader40)x).Issue);
            }
        }

        protected virtual void OnPrepareDefinition(ProducerDefinition definition)
        { }
    }
}