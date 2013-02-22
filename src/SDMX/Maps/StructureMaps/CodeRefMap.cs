using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OXM;
using Common;
using System.Xml.Linq;

namespace SDMX.Parsers
{
    internal class CodeRefMap : ClassMap<CodeRef>
    {
        CodeRef codeRef = new CodeRef();

        public CodeRefMap(HierarchicalCodeList hierarchicalCodeList)
        {                   
            // URN is generaged so we never need to set it manually         
            Map<Uri>(o => null).ToSimpleElement("URN", false)
                //.Set(v => urn = v)
                .Converter(new UriConverter());

            Map(o => o.CodeListRef.Alias).ToSimpleElement("CodelistAliasRef", true)
                .Set(v => codeRef.CodeListRef = GetCodeListRef(hierarchicalCodeList, v))
                .Converter(new IdConverter());

            Map(o => o.CodeId).ToSimpleElement("CodeID", true)
                .Set(v => codeRef.CodeId = v)
                .Converter(new IdConverter());

            MapCollection(o => o.Children).ToElement("CodeRef", false)
                .Set(v => codeRef.Add(v))
                .ClassMap(() => new CodeRefMap(hierarchicalCodeList));

            // ignored
            Map(o => o.LevelRef).ToSimpleElement("LevelRef", false)
                .Converter(new IdConverter());

            // ignored
            Map(o => o.NodeAliasId).ToSimpleElement("NodeAliasID", false)
                .Converter(new IdConverter());

            Map(o => o.Version).ToSimpleElement("Version", false)
                .Set(v => codeRef.Version = v)
                .Converter(new StringConverter());

            Map(o => o.ValidFrom).ToSimpleElement("ValidFrom", false)
                .Set(v => codeRef.ValidFrom = v)
                .Converter(new TimePeriodConverter());

            Map(o => o.ValidTo).ToSimpleElement("ValidTo", false)
                .Set(v => codeRef.ValidTo = v)
                .Converter(new TimePeriodConverter());
        }

        private CodeListRef GetCodeListRef(HierarchicalCodeList hierarchicalCodeList, Id alias)
        {
            return hierarchicalCodeList.CodeListRefs.Where(c => c.Alias == alias).Single();
        }

        protected override CodeRef Return()
        {
            return codeRef;
        }
    }
}
