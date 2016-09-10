using Optional;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XRoadLib.Serialization;

namespace MyNamespace
{
    public class Objekt : IXRoadXmlSerializable
    {
        public Option<Aadress> Aadress { get; set; }
        public Option<IList<long>> AlaLiigidKL { get; set; }
        public Option<IList<long>> AlaStaatusedKL { get; set; }
        public Option<DateTime?> AlgusKP { get; set; }
        public Option<IList<Fail>> Failid { get; set; }
        public Option<string> Kirjeldus { get; set; }
        public Option<string> KlientsysteemiID { get; set; }
        public Option<decimal?> Kogus { get; set; }
        public Option<IList<long>> LiigidKL { get; set; }
        public Option<string> Muutja { get; set; }
        public Option<DateTime?> MuutmiseKP { get; set; }
        public Option<string> Nimetus { get; set; }
        public Option<string> Number { get; set; }
        public Option<long> ObjektID { get; set; }
        public Option<long?> PakendiLiikKL { get; set; }
        public Option<string> PakendiNR { get; set; }
        public Option<decimal?> RahaVaartus { get; set; }
        public Option<long?> SeisundKL { get; set; }
        public Option<string> Sisestaja { get; set; }
        public Option<DateTime?> SisestamiseKP { get; set; }
        public Option<IList<long>> StaatusedKL { get; set; }
        public Option<string> Sulgeja { get; set; }
        public Option<DateTime?> SulgemiseKP { get; set; }
        public Option<IList<Toiming>> Toimingud { get; set; }
        public Option<long?> TyypKL { get; set; }
        public Option<long> ValuutaKL { get; set; }
        public Option<long?> YhikKL { get; set; }

        void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
        {
        }

        void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
        {
        }
    }
}