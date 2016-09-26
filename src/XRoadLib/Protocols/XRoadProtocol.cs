﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web.Services.Description;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using XRoadLib.Extensions;
using XRoadLib.Protocols.Description;
using XRoadLib.Protocols.Headers;
using XRoadLib.Protocols.Styles;
using XRoadLib.Schema;
using XRoadLib.Serialization;

namespace XRoadLib.Protocols
{
    /// <summary>
    /// X-Road message protocol implementation details.
    /// </summary>
    public abstract class XRoadProtocol
    {
        /// <summary>
        /// Qualified name of string type.
        /// </summary>
        protected static readonly XName stringTypeName = XName.Get("string", NamespaceConstants.XSD);

        /// <summary>
        /// Qualified name of boolean type.
        /// </summary>
        protected static readonly XName booleanTypeName = XName.Get("boolean", NamespaceConstants.XSD);

        /// <summary>
        /// Qualified name of binary type.
        /// </summary>
        protected static readonly XName base64TypeName = XName.Get("base64", NamespaceConstants.XSD);

        /// <summary>
        /// <see>XmlDocument</see> instance for building element and attribute nodes.
        /// </summary>
        protected readonly XmlDocument document = new XmlDocument();

        private readonly SchemaDefinitionReader schemaDefinitionReader;

        private IDictionary<uint, ISerializerCache> versioningSerializerCaches;
        private ISerializerCache serializerCache;

        internal ISet<XName> MandatoryHeaders { get; } = new SortedSet<XName>(new XNameComparer());

        /// <summary>
        /// String form of message protocol version.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// String serialization mode used by protocol instance.
        /// </summary>
        public virtual StringSerializationMode StringSerializationMode => StringSerializationMode.HtmlEncoded;

        /// <summary>
        /// Global versions supported by this X-Road message protocol instance.
        /// </summary>
        public IEnumerable<uint> SupportedVersions => versioningSerializerCaches?.Keys ?? Enumerable.Empty<uint>();

        /// <summary>
        /// XML document style of messages (RPC/Encoded or Document/Literal).
        /// </summary>
        public Style Style { get; }

        /// <summary>
        /// Main namespace which defines current producer operations and types.
        /// </summary>
        public string ProducerNamespace { get; }

        /// <summary>
        /// Assembly which provides runtime types for operations and types.
        /// </summary>
        public Assembly ContractAssembly { get; private set; }

        /// <summary>
        /// Defines X-Road header elements which are required by specification.
        /// </summary>
        protected abstract void DefineMandatoryHeaderElements();

        /// <summary>
        /// Initializes new X-Road message protocol instance.
        /// </summary>
        protected XRoadProtocol(string producerNamespace, Style style, ISchemaExporter schemaExporter)
        {
            if (style == null)
                throw new ArgumentNullException(nameof(style));
            Style = style;

            if (string.IsNullOrWhiteSpace(producerNamespace))
                throw new ArgumentNullException(nameof(producerNamespace));
            ProducerNamespace = producerNamespace;

            schemaDefinitionReader = new SchemaDefinitionReader(producerNamespace, schemaExporter);
        }

        internal abstract bool IsHeaderNamespace(string ns);

        internal virtual bool IsDefinedByEnvelope(XmlReader reader)
        {
            return false;
        }

        internal abstract IXRoadHeader CreateHeader();

        internal virtual void WriteSoapEnvelope(XmlWriter writer)
        {
            writer.WriteStartElement(PrefixConstants.SOAP_ENV, "Envelope", NamespaceConstants.SOAP_ENV);
            writer.WriteAttributeString(PrefixConstants.XMLNS, PrefixConstants.SOAP_ENV, NamespaceConstants.XMLNS, NamespaceConstants.SOAP_ENV);
            writer.WriteAttributeString(PrefixConstants.XMLNS, PrefixConstants.XSD, NamespaceConstants.XMLNS, NamespaceConstants.XSD);
            writer.WriteAttributeString(PrefixConstants.XMLNS, PrefixConstants.XSI, NamespaceConstants.XMLNS, NamespaceConstants.XSI);
            writer.WriteAttributeString(PrefixConstants.XMLNS, PrefixConstants.TARGET, NamespaceConstants.XMLNS, ProducerNamespace);
        }

        /// <summary>
        /// Serializes X-Road message protocol header elements.
        /// </summary>
        protected abstract void WriteXRoadHeader(XmlWriter writer, IXRoadHeader header);

        /// <summary>
        /// Serializes header of SOAP message.
        /// </summary>
        public void WriteSoapHeader(XmlWriter writer, IXRoadHeader header, IEnumerable<XElement> additionalHeaders = null)
        {
            writer.WriteStartElement("Header", NamespaceConstants.SOAP_ENV);

            WriteXRoadHeader(writer, header);

            foreach (var additionalHeader in additionalHeaders ?? Enumerable.Empty<XElement>())
                additionalHeader.WriteTo(writer);

            writer.WriteEndElement();
        }

        /// <summary>
        /// Generates new service description for current message protocol instance.
        /// </summary>
        public void WriteServiceDescription(Stream outputStream, uint? version = null)
        {
            new ProducerDefinition(this, schemaDefinitionReader, ContractAssembly, version).SaveTo(outputStream);
        }

        /// <summary>
        /// Associate runtime types with current message protocol instance.
        /// </summary>
        public void SetContractAssemblyOfType<TAssembly>()
        {
            SetContractAssembly(typeof(TAssembly).GetTypeInfo().Assembly);
        }

        /// <summary>
        /// Associate runtime types with current message protocol instance.
        /// </summary>
        public void SetContractAssembly(Assembly assembly, params uint[] supportedVersions)
        {
            SetContractAssembly(assembly, null, supportedVersions);
        }

        /// <summary>
        /// Associate runtime types with current message protocol instance.
        /// </summary>
        public void SetContractAssembly(Assembly assembly, IList<string> availableFilters, params uint[] supportedVersions)
        {
            if (ContractAssembly != null)
                throw new Exception($"This protocol instance (message protocol version `{Name}`) already has contract configured.");

            ContractAssembly = assembly;
            DefineMandatoryHeaderElements();

            if (supportedVersions == null || supportedVersions.Length == 0)
            {
                serializerCache = new SerializerCache(this, schemaDefinitionReader, assembly) { AvailableFilters = availableFilters };
                return;
            }

            versioningSerializerCaches = new Dictionary<uint, ISerializerCache>();
            foreach (var dtoVersion in supportedVersions)
                versioningSerializerCaches.Add(dtoVersion, new SerializerCache(this, schemaDefinitionReader, assembly, dtoVersion) { AvailableFilters = availableFilters });
        }

        /// <summary>
        /// Get runtime types lookup object.
        /// </summary>
        public ISerializerCache GetSerializerCache(uint? version = null)
        {
            if (serializerCache == null && versioningSerializerCaches == null)
                throw new Exception($"This protocol instance (message protocol version `{Name}`) is not configured with contract assembly.");

            if (serializerCache != null)
                return serializerCache;

            if (!version.HasValue)
                throw new Exception($"This protocol instance (message protocol version `{Name}`) requires specific version value.");

            ISerializerCache versioningSerializerCache;
            if (versioningSerializerCaches.TryGetValue(version.Value, out versioningSerializerCache))
                return versioningSerializerCache;

            throw new ArgumentException($"This protocol instance (message protocol version `{Name}`) does not support `v{version.Value}`.", nameof(version));
        }

        /// <summary>
        /// Configure required SOAP header elements.
        /// </summary>
        protected void AddMandatoryHeaderElement<THeader, T>(Expression<Func<THeader, T>> expression) where THeader : IXRoadHeader
        {
            var memberExpression = expression.Body as MemberExpression;
            if (memberExpression == null)
                throw new ArgumentException($"Only MemberExpression is allowed to use for SOAP header definition, but was {expression.Body.GetType().Name} ({GetType().Name}).", nameof(expression));

            var attribute = memberExpression.Member.GetSingleAttribute<XmlElementAttribute>() ?? GetElementAttributeFromInterface(memberExpression.Member.DeclaringType, memberExpression.Member as PropertyInfo);
            if (string.IsNullOrWhiteSpace(attribute?.ElementName))
                throw new ArgumentException($"Specified member `{memberExpression.Member.Name}` does not define any XML element.", nameof(expression));

            MandatoryHeaders.Add(XName.Get(attribute.ElementName, attribute.Namespace));
        }

        private static XmlElementAttribute GetElementAttributeFromInterface(Type declaringType, PropertyInfo propertyInfo)
        {
            if (propertyInfo == null)
                return null;

            var getMethod = propertyInfo.GetGetMethod();

            foreach (var iface in declaringType.GetTypeInfo().GetInterfaces())
            {
                var map = declaringType.GetTypeInfo().GetRuntimeInterfaceMap(iface);

                var index = -1;
                for (var i = 0; i < map.TargetMethods.Length; i++)
                    if (map.TargetMethods[i] == getMethod)
                    {
                        index = i;
                        break;
                    }

                if (index < 0)
                    continue;

                var ifaceProperty = iface.GetTypeInfo().GetProperties().SingleOrDefault(p => p.GetGetMethod() == map.InterfaceMethods[index]);

                var attribute = ifaceProperty.GetSingleAttribute<XmlElementAttribute>();
                if (attribute != null)
                    return attribute;
            }

            return null;
        }

        /// <summary>
        /// Writes single SOAP header element.
        /// </summary>
        protected void WriteHeaderElement(XmlWriter writer, string name, object value, XName typeName)
        {
            if (!MandatoryHeaders.Contains(name) && value == null)
                return;

            writer.WriteStartElement(name, schemaDefinitionReader.GetXRoadNamespace());

            Style.WriteExplicitType(writer, typeName);

            var stringValue = value as string;
            if (stringValue != null)
                writer.WriteStringWithMode(stringValue, StringSerializationMode);
            else writer.WriteValue(value);

            writer.WriteEndElement();
        }

        private class XNameComparer : IComparer<XName>
        {
            public int Compare(XName x, XName y)
            {
                var ns = string.Compare(x.NamespaceName, y.NamespaceName);
                return ns != 0 ? ns : string.Compare(x.LocalName, y.LocalName);
            }
        }
    }
}