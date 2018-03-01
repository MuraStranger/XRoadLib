﻿namespace XRoadLib.Extensions.AspNetCore
{
    /// <inheritdoc />
    /// <summary>
    /// Handle X-Road service description request on AspNetCore platform.
    /// </summary>
    public class XRoadWsdlHandler : XRoadHandlerBase
    {
        /// <summary>
        /// Initialize new handler for certain protocol.
        /// </summary>
        public XRoadWsdlHandler(IServiceManager serviceManager)
            : base(serviceManager)
        { }

        /// <inheritdoc />
        public override void HandleRequest(XRoadContext context)
        {
            ServiceManager.CreateServiceDescription()
                          .SaveTo(context.HttpContext.Response.Body);
        }
    }
}