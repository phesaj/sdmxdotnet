using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Data;
using System.Xml.Linq;
using System.Xml;
using SDMX.Parsers;
using System.Diagnostics;

namespace SDMX.Tests
{
    [TestFixture]
    public partial class DataReaderTests
    {
        [Test]
        public void ReadCompact()
        {
            string dataPath = Utility.GetPath("lib\\CompactSample.xml");
            TestRead(dataPath, 26);
        }

        [Test]
        public void ReadCompact_NoHeader()
        {
            string dataPath = Utility.GetPath("lib\\CompactSample.xml");
            TestRead_NoHeader(dataPath, 26);
        }

        [Test]
        public void ReadCompact_SkipHeader()
        {
            string dataPath = Utility.GetPath("lib\\CompactSample.xml");
            TestRead_SkipHeader(dataPath, 26);
        }

        [Test]
        public void ReadCompact_read_header_at_end()
        {
            string dataPath = Utility.GetPath("lib\\CompactSample.xml");
            TestRead_ReadHeaerAtEnd(dataPath, 26);
        }

        [Test]
        public void ReadGeneric()
        {
            string dataPath = Utility.GetPath("lib\\GenericSample.xml");
            TestRead(dataPath, 13);
        }

        [Test]
        public void ReadGeneric_NoHeader()
        {
            string dataPath = Utility.GetPath("lib\\GenericSample.xml");
            TestRead_NoHeader(dataPath, 13);
        }

        [Test]
        public void ReadGeneric_SkipHeader()
        {
            string dataPath = Utility.GetPath("lib\\GenericSample.xml");
            TestRead_SkipHeader(dataPath, 13);
        }

        [Test]
        public void ReadGeneric_read_header_at_end()
        {
            string dataPath = Utility.GetPath("lib\\GenericSample.xml");
            TestRead_ReadHeaerAtEnd(dataPath, 13);
        }

        void TestRead(string dataPath, int count)
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var dsd = StructureMessage.Load(dsdPath);
            var keyFamily = dsd.KeyFamilies[0];

            int counter = 0;
            using (var reader = DataReader.Create(dataPath, keyFamily))
            {
                var header = reader.ReadHeader();

                Assert.IsNotNull(header);

                while (reader.Read())
                {
                    Assert.AreEqual(17, reader.Count());
                    //foreach (var item in reader)
                    //    Console.Write("{0}={1},", item.Key, item.Value);
                    //Console.WriteLine();
                    counter++;
                }
            }

            Assert.AreEqual(count, counter);
        }

        void TestRead_NoHeader(string dataPath, int count)
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var dsd = StructureMessage.Load(dsdPath);
            var keyFamily = dsd.KeyFamilies[0];

            var doc = XDocument.Load(dataPath);
            doc.Descendants().Where(i => i.Name.LocalName == "Header").Single().Remove();

            int counter = 0;
            using (var reader = DataReader.Create(doc.CreateReader(), keyFamily))
            {
                var header = reader.ReadHeader();

                Assert.IsNull(header);

                while (reader.Read())
                {
                    Assert.AreEqual(17, reader.Count());
                    //foreach (var item in reader)
                    //    Console.Write("{0}={1},", item.Key, item.Value);
                    //Console.WriteLine();
                    counter++;
                }
            }

            Assert.AreEqual(count, counter);
        }

        void TestRead_SkipHeader(string dataPath, int count)
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var dsd = StructureMessage.Load(dsdPath);
            var keyFamily = dsd.KeyFamilies[0];

            int counter = 0;
            using (var reader = DataReader.Create(dataPath, keyFamily))
            {
                while (reader.Read())
                {
                    Assert.AreEqual(17, reader.Count());
                    //foreach (var item in reader)
                    //    Console.Write("{0}={1},", item.Key, item.Value);
                    //Console.WriteLine();
                    counter++;
                }
            }

            Assert.AreEqual(count, counter);
        }

        void TestRead_ReadHeaerAtEnd(string dataPath, int count)
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var dsd = StructureMessage.Load(dsdPath);
            var keyFamily = dsd.KeyFamilies[0];

            int counter = 0;
            using (var reader = DataReader.Create(dataPath, keyFamily))
            {
                while (reader.Read())
                {
                    Assert.AreEqual(17, reader.Count());
                    //foreach (var item in reader)
                    //    Console.Write("{0}={1},", item.Key, item.Value);
                    //Console.WriteLine();
                    counter++;
                }

                var header = reader.ReadHeader();
                Assert.IsNull(header);
            }

            Assert.AreEqual(count, counter);
        }


        [Test]
        public void read_compact_invalid_obs_value()
        {
            string dataPath = Utility.GetPath("lib\\CompactSample.xml");

            var doc = XDocument.Load(dataPath);
            doc.Descendants().Where(i => i.Name.LocalName == "Obs").First().Attribute("OBS_VALUE").Value = "abc";
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var dsd = StructureMessage.Load(dsdPath);
            var keyFamily = dsd.KeyFamilies[0];

            int counter = 0;
            using (var reader = DataReader.Create(doc.CreateReader(), keyFamily))
            {
                reader.ThrowExceptionIfNotValid = false;
                while (reader.Read())
                {
                    if (!reader.IsValid)
                    {
                        Assert.AreEqual(1, reader.Errors.Count);
                        Assert.IsTrue(reader.Errors[0] is ParseError);
                        //Debug.WriteLine(reader.Errors[0].Message);
                        counter++;
                    }
                }
            }

            Assert.AreEqual(1, counter);
        }

        [Test]
        public void read_compact_invalid_missing_dim()
        {
            string dataPath = Utility.GetPath("lib\\CompactSample.xml");

            var doc = XDocument.Load(dataPath);
            var series = doc.Descendants().Where(i => i.Name.LocalName == "Series").First();
            series.RemoveAttributes();
            series.SetAttributeValue("FREQxx", "M");
            series.SetAttributeValue("COLLECTION", "B");
            series.SetAttributeValue("TIME_FORMAT", "P1M");
            series.SetAttributeValue("VIS_CTY", "MX");
            series.SetAttributeValue("JD_TYPE", "P");
            series.SetAttributeValue("JD_CATEGORY", "A");

            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var dsd = StructureMessage.Load(dsdPath);
            var keyFamily = dsd.KeyFamilies[0];

            int counter = 0;
            using (var reader = DataReader.Create(doc.CreateReader(), keyFamily))
            {
                reader.ThrowExceptionIfNotValid = false;
                while (reader.Read())
                {
                    if (!reader.IsValid)
                    {
                        Assert.AreEqual(2, reader.Errors.Count);
                        Assert.IsTrue(reader.Errors[0] is ValidationError);
                        Assert.IsTrue(reader.Errors[1] is MandatoryComponentMissing);
                        //Debug.WriteLine(reader.Errors[0].Message);
                        //Debug.WriteLine(reader.Errors[1].Message);
                        counter++;
                    }
                }
            }

            Assert.AreEqual(12, counter);
        }

        [Test]
        public void read_compact_invalid_dim_value()
        {
            string dataPath = Utility.GetPath("lib\\CompactSample.xml");

            var doc = XDocument.Load(dataPath);
            var series = doc.Descendants().Where(i => i.Name.LocalName == "Series").First();
            series.Attribute("FREQ").Value = "InvalidValue";

            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var dsd = StructureMessage.Load(dsdPath);
            var keyFamily = dsd.KeyFamilies[0];

            int counter = 0;
            using (var reader = DataReader.Create(doc.CreateReader(), keyFamily))
            {
                reader.ThrowExceptionIfNotValid = false;
                while (reader.Read())
                {
                    if (!reader.IsValid)
                    {
                        Assert.AreEqual(1, reader.Errors.Count);
                        Assert.IsTrue(reader.Errors[0] is ParseError);
                        //Debug.WriteLine(reader.Errors[0].Message);
                        counter++;
                    }
                }
            }

            Assert.AreEqual(12, counter);
        }
    }
}
