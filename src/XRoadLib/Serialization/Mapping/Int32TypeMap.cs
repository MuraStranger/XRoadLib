﻿using System.Xml;
using XRoadLib.Extensions;
using XRoadLib.Schema;
using XRoadLib.Serialization.Template;

namespace XRoadLib.Serialization.Mapping
{
    public class Int32TypeMap : TypeMap
    {
        public Int32TypeMap(TypeDefinition typeDefinition)
            : base(typeDefinition)
        { }

        public override object Deserialize(XmlReader reader, IXmlTemplateNode templateNode, ContentDefinition content, XRoadMessage message)
        {
            if (reader.IsEmptyElement)
                return MoveNextAndReturn(reader, HandleEmptyElement(content, message));

            var value = reader.ReadElementContentAsString();
            if (string.IsNullOrEmpty(value))
                return HandleEmptyElement(content, message);

            return XmlConvert.ToInt32(value);
        }

        public override void Serialize(XmlWriter writer, IXmlTemplateNode templateNode, object value, ContentDefinition content, XRoadMessage message)
        {
            message.Style.WriteType(writer, Definition, content);

            writer.WriteValue(value);
        }

        private static int? HandleEmptyElement(ContentDefinition content, XRoadMessage message)
        {
            return message.HandleEmptyElementOfValueType<int>(content, () => throw new InvalidQueryException("'' is not a valid value for 'int'"));
        }
    }
}