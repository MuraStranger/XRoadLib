#if NETSTANDARD1_5

using System.Xml;

namespace XRoadLib.Xml.Schema
{
    public abstract class XmlSchemaContentModel : XmlSchemaAnnotated
    {
        public XmlSchemaContent Content { get; set; }

        protected override void WriteElements(XmlWriter writer)
        {
            base.WriteElements(writer);
            Content?.Write(writer);
        }
    }
}

#endif