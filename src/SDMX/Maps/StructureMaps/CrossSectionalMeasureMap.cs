using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OXM;
using Common;

namespace SDMX.Parsers
{
    internal class XMeasureMap : CompoenentMap<XMeasure>
    {
        XMeasure _measure;
        
        internal XMeasureMap(StructureMessage message)
            : base(message)
        {
            AttributesOrder("conceptRef",
                            "codelist",
                            "measureDimension",
                            "code");

            ElementsOrder("TextFormat", "Annotations");

            Map(o => o.Dimension).ToAttribute("measureDimension", true)
                .Set(v => _measure.Dimension = v)
                .Converter(new IDConverter());

            Map(o => o.Code).ToAttribute("code", true)
                .Set(v => _measure.Code = v)
                .Converter(new IDConverter());
        }      

        protected override XMeasure Create(Concept conecpt)
        {
            _measure = new XMeasure(conecpt);
            return _measure;
        }

        protected override void AddAnnotation(Annotation annotation)
        {
            _measure.Annotations.Add(annotation);
        }

        protected override XMeasure Return()
        {
            return _measure;
        }
    }
}
