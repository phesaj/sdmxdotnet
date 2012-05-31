using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace OXM
{
    public class SingleConverter : SimpleTypeConverter<float>
    {
        public override string ToXml(float value)
        {
            return XmlConvert.ToString(value);
        }

        public override float ToObj(string value)
        {
            return XmlConvert.ToSingle(value);
        }

        public override bool CanConvertToObj(string s)
        {
            float result = 0;
            return float.TryParse(s, out result);
        }
    }

    public class NullableSingleConverter : NullabeConverter<float>
    {
        protected override SimpleTypeConverter<float> Converter
        {
            get
            {
                return new SingleConverter();
            }
        }
    }
}
