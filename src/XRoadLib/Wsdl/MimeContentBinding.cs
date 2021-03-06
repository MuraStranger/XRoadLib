using System.Xml;

namespace XRoadLib.Wsdl
{
    public class MimeContentBinding : ServiceDescriptionFormatExtension
    {
        public string Part { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;

        internal override void Write(XmlWriter writer)
        {
            writer.WriteStartElement(PrefixConstants.MIME, "content", NamespaceConstants.MIME);
            writer.WriteAttributeString("part", Part);
            writer.WriteAttributeString("type", Type);
            writer.WriteEndElement();
        }
    }
}