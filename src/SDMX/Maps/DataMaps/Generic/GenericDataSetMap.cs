using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OXM;

namespace SDMX.Parsers
{
    internal class GenericDataSetMap : ClassMap<DataSet>
    {
        DataSet _dataSet;

        public GenericDataSetMap(KeyFamily keyFamily)
        {
            _dataSet = new DataSet(keyFamily);

            Map(o => o.KeyFamily.Id).ToSimpleElement(Namespaces.Generic + "KeyFamilyRef", true)
                .Set(id => VerifyKeyFamilyRef(id))
                .Converter(new IdConverter());

            //MapContainer(Namespaces.Generic + "Attributes", false)
            //    .MapCollection(o => GetKeyValues(o.Attributes)).ToElement("Value", false)
            //        .Set(v => _dataSet.Attributes[v.Concept].Parse(v.Value))
            //        .ClassMap(() => new KeyValueMap());

            MapCollection(o => o.Series).ToElement(Namespaces.Generic + "Series", false)
                .Set(v => _dataSet.Series.Add(v))
                .ClassMap(() => new GenericSeriesMap(_dataSet));
        }   

        private void VerifyKeyFamilyRef(Id id)
        {
            if (id != _dataSet.KeyFamily.Id)
            {
                throw new SDMXException("KeyFamilyRef '{0}' is not the same as the dataset key family id '{1}'", id, _dataSet.KeyFamily.Id);
            }
        }

        protected override DataSet Return()
        {
            return _dataSet;
        }
    }
}
