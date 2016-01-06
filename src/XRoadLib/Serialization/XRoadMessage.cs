﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using XRoadLib.Header;

namespace XRoadLib.Serialization
{
    public class XRoadMessage : IAttachmentManager
    {
        private readonly List<XRoadAttachment> attachments = new List<XRoadAttachment>();

        public bool IsMultipart { get; set; }
        public string MultipartContentType { get; internal set; }
        public Encoding ContentEncoding { get; internal set; }
        public Stream ContentStream { get; internal set; }
        public XRoadProtocol Protocol { get; internal set; }
        public IXRoadHeader Header { get; internal set; }
        public XmlQualifiedName RootElementName { get; internal set; }

        public IList<XRoadAttachment> AllAttachments => attachments;
        public IEnumerable<XRoadAttachment> MultipartContentAttachments { get { return attachments.Where(x => x.IsMultipartContent); } }

        public XRoadMessage()
        {
            ContentEncoding = Encoding.UTF8;
        }

        public XRoadMessage(XRoadProtocol protocol) : this()
        {
            Protocol = protocol;
        }

        public XRoadMessage(Stream contentStream, XRoadProtocol protocol) : this(protocol)
        {
            ContentStream = contentStream;
        }

        public XRoadAttachment GetAttachment(string contentID)
        {
            return attachments.FirstOrDefault(attachment => attachment.ContentID.Contains(contentID));
        }

        public void LoadRequest(Stream stream, NameValueCollection headers, Encoding contentEncoding, string storagePath)
        {
            using (var reader = new XRoadMessageReader(stream, headers, contentEncoding, storagePath))
                reader.Read(this);
        }

        public void LoadResponse(Stream stream, NameValueCollection headers, Encoding contentEncoding, string storagePath)
        {
            using (var reader = new XRoadMessageReader(stream, headers, contentEncoding, storagePath))
                reader.Read(this, true);
        }

        public void SaveTo(TextWriter textWriter, Stream outputStream, Action<string> setContentType, Action<string, string> appendHeader)
        {
            using (var writer = new XRoadMessageWriter(textWriter, outputStream))
                writer.Write(this, setContentType, appendHeader);
        }

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

        public MetaServiceName GetMetaServiceName()
        {
            if (Header?.Nimi != null)
                return Header.Nimi.Method == "getState" ? MetaServiceName.GetState : MetaServiceName.None;

            if (RootElementName.Namespace != NamespaceHelper.GetXRoadNamespace(Protocol))
                return MetaServiceName.Unsupported;

            switch (RootElementName.Name)
            {
                case "listMethods":
                    return MetaServiceName.ListMethods;
                case "testSystem":
                    return MetaServiceName.TestSystem;
                default:
                    return MetaServiceName.Unsupported;
            }
        }

        public void Copy(XRoadMessage message)
        {
            Protocol = message.Protocol;
        }
    }
}