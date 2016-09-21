﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using XRoadLib.Protocols;
using XRoadLib.Protocols.Headers;
using XRoadLib.Schema;
using XRoadLib.Serialization.Mapping;
using XRoadLib.Serialization.Template;
using XRoadLib.Extensions;

#if NET40
using System.Web;
#else
using Microsoft.AspNetCore.Http;
#endif

namespace XRoadLib.Serialization
{
    /// <summary>
    /// Deserialized content of X-Road message (input or output).
    /// </summary>
    public class XRoadMessage : IAttachmentManager
    {
        /// <summary>
        /// Expected content type of message with SOAP attachments.
        /// </summary>
        public const string MULTIPART_CONTENT_TYPE_SOAP = "text/xml";

        /// <summary>
        /// Expected content type of message with XOP attachments.
        /// </summary>
        public const string MULTIPART_CONTENT_TYPE_XOP = "application/xop+xml";

        private readonly List<XRoadAttachment> attachments = new List<XRoadAttachment>();

        /// <summary>
        /// When enabled, serialization/deserialization process applies filter specified in
        /// <see>FilterName</see> field, to exclude certain elements from outgoing messages, or ignore
        /// their values in incoming messages.
        /// </summary>
        public bool EnableFiltering { get; private set; }

        /// <summary>
        /// Specifies filter which is used on this particular X-Road message, when
        /// <see>EnableFiltering</see> is set to <value>true</value>.
        /// </summary>
        public string FilterName { get; private set; }

        /// <summary>
        /// Allows to limit depth of serialization/deserialization for recursive type
        /// definitions.
        /// Template allows to skip serialization of certain XML elements of the X-Road
        /// message (elements are present in document, but set to null even if type contains value).
        /// For deserialization incoming elements are ignored even if they contain value.
        /// </summary>
        public IXmlTemplate XmlTemplate { get; set; }

        /// <summary>
        /// Content type of the MIME multipart message container.
        /// </summary>
        public string MultipartContentType { get; set; }

        /// <summary>
        /// Encoding that is used to transfer X-Road message content.
        /// </summary>
        public Encoding ContentEncoding { get; set; } = XRoadEncoding.UTF8;

        /// <summary>
        /// XML document part of the X-Road message.
        /// </summary>
        public Stream ContentStream { get; set; }

        /// <summary>
        /// X-Road message protocol version used to serialize/deserialize this message.
        /// </summary>
        public XRoadProtocol Protocol { get; set; }

        /// <summary>
        /// X-Road protocol compliant header values extracted from SOAP header of
        /// the message.
        /// </summary>
        public IXRoadHeader Header { get; set; }

        /// <summary>
        /// Remaining non-standard elements in X-Road message header.
        /// </summary>
        public IList<XElement> UnresolvedHeaders { get; set; }

        /// <summary>
        /// Identifies operation name for the X-Road message.
        /// </summary>
        public XName RootElementName { get; set; }

        /// <summary>
        /// Serialization format for binary content.
        /// </summary>
        public BinaryMode BinaryMode { get; set; }

        /// <summary>
        /// Shows if this X-Road message wrapped inside MIME multipart container.
        /// </summary>
        public bool IsMultipartContainer { get; set; }

        /// <summary>
        /// When X-Road message represents X-Road meta service operation, this
        /// property is assigned to correct service map to handle the operation.
        /// </summary>
        public IServiceMap MetaServiceMap { get; set; }

        /// <summary>
        /// Entire length of the X-Road message in serialized form.
        /// </summary>
        public long ContentLength { get; internal set; }

        /// <summary>
        /// All attachments (including inline content) that are packaged together
        /// with current X-Road message.
        /// </summary>
        public IList<XRoadAttachment> AllAttachments => attachments;

        /// <summary>
        /// Multipart attachments that are packaged together with current X-Road message.
        /// </summary>
        public IEnumerable<XRoadAttachment> MultipartContentAttachments { get { return attachments.Where(x => x.IsMultipartContent); } }

        /// <summary>
        /// Operation version of current X-Road message.
        /// </summary>
        public uint Version => Header == null || Header.Service == null || !Header.Service.Version.HasValue ? 1u : Header.Service.Version.Value;

        /// <summary>
        /// X-Road message template request element root node.
        /// </summary>
        public IXmlTemplateNode RequestNode => XmlTemplate != null ? XmlTemplate.RequestNode : XRoadXmlTemplate.EmptyNode;

        /// <summary>
        /// X-Road message template response element root node.
        /// </summary>
        public IXmlTemplateNode ResponseNode => XmlTemplate != null ? XmlTemplate.ResponseNode : XRoadXmlTemplate.EmptyNode;

        /// <summary>
        /// Initializes new empty X-Road message for deserialization.
        /// </summary>
        public XRoadMessage()
        { }

        /// <summary>
        /// Initializes new empty X-Road message for request serialization.
        /// </summary>
        public XRoadMessage(XRoadProtocol protocol, IXRoadHeader header)
            : this(new MemoryStream())
        {
            Protocol = protocol;
            Header = header;
        }

        /// <summary>
        /// Initializes new empty X-Road message for response serialization.
        /// </summary>
        public XRoadMessage(Stream contentStream)
        {
            ContentStream = contentStream;
        }

        /// <summary>
        /// Find X-Road message attachment by content ID.
        /// </summary>
        public XRoadAttachment GetAttachment(string contentID)
        {
            return attachments.FirstOrDefault(attachment => attachment.ContentID.Contains(contentID));
        }

#if NET451
        /// <summary>
        /// Loads X-Road message contents from request message.
        /// </summary>
        public void LoadRequest(System.Web.HttpContext httpContext, string storagePath, IEnumerable<XRoadProtocol> supportedProtocols)
        {
            LoadRequest(httpContext.Request.InputStream, httpContext.Request.Headers.GetContentTypeHeader(), storagePath, supportedProtocols);
        }
#endif

        /// <summary>
        /// Loads X-Road message contents from request message.
        /// </summary>
        public void LoadRequest(HttpContext httpContext, string storagePath, IEnumerable<XRoadProtocol> supportedProtocols)
        {
#if NET40
            var requestStream = httpContext.Request.InputStream;
#else
            var requestStream = httpContext.Request.Body;
#endif
            LoadRequest(requestStream, httpContext.Request.Headers.GetContentTypeHeader(), storagePath, supportedProtocols);
        }

        /// <summary>
        /// Loads X-Road message contents from request message.
        /// </summary>
        public void LoadRequest(Stream stream, string contentTypeHeader, string storagePath, XRoadProtocol protocol)
        {
            LoadRequest(stream, contentTypeHeader, storagePath, new [] { protocol });
        }

        /// <summary>
        /// Loads X-Road message contents from request message.
        /// </summary>
        public void LoadRequest(Stream stream, string contentTypeHeader, string storagePath, IEnumerable<XRoadProtocol> supportedProtocols)
        {
            using (var reader = new XRoadMessageReader(stream, contentTypeHeader, storagePath, supportedProtocols))
                reader.Read(this);
        }

        /// <summary>
        /// Loads X-Road message contents from response message.
        /// </summary>
        public void LoadResponse(Stream stream, string contentTypeHeader, string storagePath, XRoadProtocol protocol)
        {
            LoadResponse(stream, contentTypeHeader, storagePath, new [] { protocol });
        }

        /// <summary>
        /// Loads X-Road message contents from response message.
        /// </summary>
        public void LoadResponse(Stream stream, string contentTypeHeader, string storagePath, IEnumerable<XRoadProtocol> supportedProtocols)
        {
            using (var reader = new XRoadMessageReader(stream, contentTypeHeader, storagePath, supportedProtocols))
                reader.Read(this, true);
        }

#if NET451
        /// <summary>
        /// Serializes X-Road message into specified HTTP context response.
        /// </summary>
        public void SaveTo(System.Web.HttpContext httpContext)
        {

            var outputStream = httpContext.Response.OutputStream;
            var appendHeader = new Action<string, string>(httpContext.Response.AppendHeader);

            using (var writer = new XRoadMessageWriter(outputStream))
                writer.Write(this, contentType => httpContext.Response.ContentType = contentType, appendHeader);
        }
#endif

        /// <summary>
        /// Serializes X-Road message into specified HTTP context response.
        /// </summary>
        public void SaveTo(HttpContext httpContext)
        {
#if NET40
            var outputStream = httpContext.Response.OutputStream;
            var appendHeader = new Action<string, string>(httpContext.Response.AppendHeader);
#else
            var outputStream = httpContext.Response.Body;
            var appendHeader = new Action<string, string>((k, v) => httpContext.Response.Headers[k] = v);
#endif

            using (var writer = new XRoadMessageWriter(outputStream))
                writer.Write(this, contentType => httpContext.Response.ContentType = contentType, appendHeader);
        }

        /// <summary>
        /// Serializes X-Road message into specified web request.
        /// </summary>
        public void SaveTo(WebRequest webRequest)
        {
#if NET40
            using (var outputStream = webRequest.GetRequestStream())
#else
            using (var outputStream = webRequest.GetRequestStreamAsync().Result)
#endif
            using (var writer = new XRoadMessageWriter(outputStream))
                writer.Write(this, contentType => webRequest.ContentType = contentType, (k, v) => webRequest.Headers[k] = v);
        }

        /// <summary>
        /// Serializes X-Road message into specified stream.
        /// </summary>
        public void SaveTo(Stream outputStream, Action<string> setContentType, Action<string, string> appendHeader)
        {
            using (var writer = new XRoadMessageWriter(outputStream))
                writer.Write(this, setContentType, appendHeader);
        }

        /// <summary>
        /// Clean up unmanaged resources allocated by the X-Road message.
        /// </summary>
        public void Dispose()
        {
            if (ContentStream != null)
            {
                ContentStream.Dispose();
                ContentStream = null;
            }

            foreach (var attachment in attachments)
                attachment.Dispose();

            attachments.Clear();
        }

        /// <summary>
        /// Copy X-Road message parts from another message, which are required to be
        /// consistent across multiple possible protocol implementations or service
        /// versions.
        /// </summary>
        public void Copy(XRoadMessage message)
        {
            Protocol = message.Protocol;
            Header = message.Header;
        }

        /// <summary>
        /// Get type lookup object which is used to serialize or deserialize current X-Road message.
        /// </summary>
        public ISerializerCache GetSerializerCache()
        {
            return Protocol?.GetSerializerCache(Version);
        }

        /// <summary>
        /// Enable filtering for X-Road message, using the filter specified by name.
        /// </summary>
        public void EnableFilter(string filterName)
        {
            EnableFiltering = true;
            FilterName = filterName;
        }
    }
}