﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Xml.Linq;
using SDMX.Parsers;
using System.Xml;
using System.IO;
using OXM;
using System.Xml.Serialization;

namespace SDMX.Tests
{
    [TestFixture]
    public class StructureTests
    {
        [Test]
        [Ignore]
        public void bop_its_tot()
        {
            var message = StructureMessage.Load(@"c:\temp\bop_its_tot.dsd.xml");
            message.Save(@"c:\temp\bop_its_tot.dsd2.xml");
            message = StructureMessage.Load(@"c:\temp\bop_its_tot.dsd2.xml");
            message.Save(@"c:\temp\bop_its_tot.dsd3.xml");
        }

        [Test]
        [Ignore]
        public void SpeedTest()
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            const int smallLoopCount = 1;
            const int bigLoopCount = 1000;
            var list = new List<decimal>();
            var list2 = new List<TimeSpan>();
            for (int i = 0; i < bigLoopCount; i++)
            {
                var stopWatch = new System.Diagnostics.Stopwatch();
                stopWatch.Start();

                for (int j = 0; j < smallLoopCount; j++)
                {
                    using (var reader = XmlReader.Create(dsdPath))
                    {
                        while (reader.Read())
                        { }
                    }
                }

                stopWatch.Stop();
                long ticks = stopWatch.ElapsedTicks;
                stopWatch.Restart();
                for (int k = 0; k < smallLoopCount; k++)
                {
                    var message = StructureMessage.Load(dsdPath);
                }

                stopWatch.Stop();
                Console.Write((decimal)stopWatch.ElapsedTicks / ticks);
                list.Add((decimal)stopWatch.ElapsedTicks / ticks);
                list2.Add(stopWatch.Elapsed);
                Console.WriteLine(" : {0}", stopWatch.Elapsed);
            }

            Console.WriteLine("Average: {0}", list.Average());
            Console.WriteLine("Average Time: {0}", TimeSpan.FromTicks((long)list2.Select(i => i.Ticks).Average()));
        }

        [Test]
        [Ignore]
        public void LoadTest()
        {
            for (int i = 0; i < 300; i++)
            {
                var message = StructureMessage.Load(@"c:\temp\bop_its_tot.dsd.xml");
            }
        }

        [Test]
        public void StructureSampleTest()
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var message = StructureMessage.Load(dsdPath);

            //message.Save(@"c:\temp\StructureSample2.xml");

            var doc = new XDocument();
            using (var writer = doc.CreateWriter())
                message.Write(writer);
            using (var reader = doc.CreateReader())
                Assert.IsTrue(MessageValidator.ValidateXml(reader));
        }

        [Test]
        public void Invalid_structure_missing_codelist()
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var doc = XDocument.Load(dsdPath);

            var series = doc.Descendants().Where(i => i.Name.LocalName == "CodeList" && i.Attribute("id").Value == "CL_FREQ").First();
            series.Remove();

            var errorList = new List<ValidationMessage>();
            var message = StructureMessage.Read(doc.CreateReader(), v => errorList.Add(v));

            Assert.AreEqual(1, errorList.Count);
        }

        [Test]
        public void Invalid_structure_missing_concept()
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var doc = XDocument.Load(dsdPath);

            var series = doc.Descendants().Where(i => i.Name.LocalName == "Concept" && i.Attribute("id").Value == "TIME").First();
            series.Remove();

            var errorList = new List<ValidationMessage>();
            var message = StructureMessage.Read(doc.CreateReader(), v => errorList.Add(v));

            Assert.AreEqual(1, errorList.Count);
        }

        [Test]
        public void Invalid_structure_duplicate_codelist()
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var doc = XDocument.Load(dsdPath);           

            var series = doc.Descendants().Where(i => i.Name.LocalName == "CodeList" && i.Attribute("id").Value == "CL_FREQ").First();
            var copy = new XElement(series);
            series.AddAfterSelf(copy);

            var errorList = new List<ValidationMessage>();
            var message = StructureMessage.Read(doc.CreateReader(), v => errorList.Add(v));

            Assert.AreEqual(1, errorList.Count);
        }

        [Test]
        public void Invalid_structure_duplicate_concept()
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var doc = XDocument.Load(dsdPath);

            var series = doc.Descendants().Where(i => i.Name.LocalName == "Concept" && i.Attribute("id").Value == "TIME").First();
            var copy = new XElement(series);
            series.AddAfterSelf(copy);

            var errorList = new List<ValidationMessage>();
            var message = StructureMessage.Read(doc.CreateReader(), v => errorList.Add(v));

            Assert.AreEqual(1, errorList.Count);
        }

        [Test]
        public void LoadWriteLoad()
        {
            string dsdPath = Utility.GetPath("lib\\StructureSample.xml");
            var message = StructureMessage.Load(dsdPath);

            var doc = new XDocument();
            using (var writer = doc.CreateWriter())
                message.Write(writer);

            // Console.Write(doc);

            using (var reader = doc.CreateReader())
                Assert.IsTrue(MessageValidator.ValidateXml(reader, w => Console.WriteLine(w), e => Console.WriteLine(e)));

            StructureMessage message2 = null;
            using (var reader = doc.CreateReader())
                message2 = StructureMessage.Read(reader);
        }
     
        [Test]
        [Ignore]
        public void HeirarchicalCodeListSample()
        {
            string dsdPath = Utility.GetPath("lib\\HeirarchicalCodeListSample.xml");
            var sample = XDocument.Load(dsdPath);

            using (var reader = sample.CreateReader())
                Assert.IsTrue(MessageValidator.ValidateXml(reader));

            StructureMessageMap map = new StructureMessageMap();

            StructureMessage message = StructureMessage.Load(dsdPath);

            message.Save(Utility.GetPath("lib\\HeirarchicalCodeListSample2.xml"));         

            var doc = XDocument.Load(Utility.GetPath("lib\\HeirarchicalCodeListSample2.xml"));

            using (var reader = doc.CreateReader())
                Assert.IsTrue(MessageValidator.ValidateXml(reader));
        }

        [Test]
        [Ignore]
        public void WBDSD_HierarchicalCodeList()
        {
            // load the DSD
            string dsdPath = Utility.GetPath("lib\\DSD_WB.xml");

            var dsd = StructureMessage.Load(dsdPath);
            
            // create hierarchical code list and add it to the DSD
            var hlist = new HierarchicalCodeList("MDG_Regions", "MDG");
            hlist.Name["en"] = "MDG Regions";
            dsd.HierarchicalCodeLists.Add(hlist);

            // get REF_AREA code list from the DSD and add it to the hierarchical code list
            var refAreaCodeList = dsd.CodeLists.Where(codeList => codeList.Id == "CL_REF_AREA_MDG").Single();
            hlist.AddCodeList(refAreaCodeList, "REF_AREA_MDG");

            // get parent country code
            var MDG_DEVELOPED = refAreaCodeList.Where(code => code.Id == "MDG_DEVELOPED").Single();

            // child country codes
            string[] ids = new[] { "ALB", "AND", "AUS", "AUT", "BEL", "BIH", "BMU", "BGR", "HRV", "CAN", "CZE", "DNK", "EST", "FRO", "FIN", "FRA", "DEU", "GRC", "HUN", "ISL", "IRL", "ITA", "JPN", "LVA", "LIE", "LTU", "LUX", "MKD", "MLT", "MCO", "MNE", "NLD", "NZL", "NOR", "POL", "PRT", "ROU", "SVK", "SMR", "SVN", "SRB", "ESP", "SWE", "CHE", "GBR", "USA" };
            var countryCodes = (from c in refAreaCodeList
                                join cid in ids on c.Id.ToString() equals cid
                                select c).ToList();

            // create hirarchy with parent code and child code and add it to
            // the hierarchical code list
            var hierarchy = new Hierarchy("Developed_Countries", new CodeRef(MDG_DEVELOPED, countryCodes));
            hierarchy.Name["en"] = "Developed Countries";
            hlist.Add(hierarchy);

            // create another hierarchy and add it to the hierarchical code list
            var MDG_NAFR = refAreaCodeList.Where(code => code.Id == "MDG_NAFR").Single();

            ids = new[] { "DZA", "EGY", "LBY", "MAR", "TUN" };

            countryCodes = (from c in refAreaCodeList
                            join cid in ids on c.Id.ToString() equals cid
                            select c).ToList();

            hierarchy = new Hierarchy("MDG_NAFR", new CodeRef(MDG_NAFR, () => countryCodes));
            hierarchy.Name["en"] = "MDG Northern Africa Countries";
            hlist.Add(hierarchy);

            // save the DSD
            dsd.Save(Utility.GetPath("lib\\DSD_WB2.xml"));

            string messageText = dsd.ToString();

            var doc = XDocument.Parse(messageText); 
            using (var reader = doc.CreateReader())
                Assert.IsTrue(MessageValidator.ValidateXml(reader));
        }
        
        [Test]
        public void HierarchicalList_InvalidAlias()
        {
            var hList = new HierarchicalCodeList(new InternationalString("en", "Europe"), "HCL_Europe", "SDMX");
            var countryCL = GetCountryCodeList();
            var regionsCL = GetRegionsCodeList();

            // don't add the country code list to the hierarchy in order to throw the exception
            //hList.AddCodeList(countryCL, "countryCLAlias");
            hList.AddCodeList(regionsCL, "RegionsAlias");

            Hierarchy hierarchy = new Hierarchy("id",
                new CodeRef(regionsCL.Get("Europe"),
                    new CodeRef(countryCL.Get("Germany")),
                    new CodeRef(countryCL.Get("UK"))));

            Assert.Throws<SDMXException>(() => hList.Add(hierarchy));
        }



        [Test]
        public void Create_HierarchicalList()
        {
            var countryCL = GetCountryCodeList();
            var regionsCL = GetRegionsCodeList();

            var hList = new HierarchicalCodeList(new InternationalString("en", "Europe"), "HCL_Europe", "SDMX");
            hList.AddCodeList(countryCL, "countryCLAlias");
            hList.AddCodeList(regionsCL, "RegionsAlias");

            Hierarchy hierarchy = new Hierarchy("id",
               new CodeRef(regionsCL.Get("Europe"),
                   new CodeRef(countryCL.Get("Germany")),
                   new CodeRef(countryCL.Get("UK"))));

            hList.Add(hierarchy);

            Assert.IsNotNull(hList);
            Assert.AreEqual("Europe", hList.Name["en"]);
            //Assert.AreEqual( Id("HCL_Europe"), hList.Id);
            //Assert.AreEqual(new Id("SDMX"), hList.AgencyId);
            //Assert.AreEqual(2, hList.CodeListRefs.Count());
            //var codeListRef = hList.CodeListRefs.ElementAt(0);
            //Assert.AreEqual(new Id("CL_Country"), codeListRef.Id);
            //Assert.AreEqual(new Id("SDMX"), codeListRef.AgencyId);
            //Assert.AreEqual(new Id("countryCLAlias"), codeListRef.Alias);

            //codeListRef = hList.CodeListRefs.ElementAt(1);
            //Assert.AreEqual(new Id("CL_Regions"), codeListRef.Id);
            //Assert.AreEqual(new Id("SDMX"), codeListRef.AgencyId);
            //Assert.AreEqual(new Id("RegionsAlias"), codeListRef.Alias);

            Assert.AreEqual(1, hList.Count());
            var hirchy = hList.ElementAt(0);
            //Assert.AreEqual(new Id("id"), hirchy.Id);
            Assert.AreEqual(3, hirchy.GetCodeRefs().Count());

            foreach (var codeRef in hirchy.GetCodeRefs())
            {
                Assert.IsNotNull(codeRef.CodeListRef);
                Assert.IsNotNull(codeRef.CodeListRef.Alias);
                Assert.IsTrue(codeRef.CodeListRef.Alias == "countryCLAlias" || codeRef.CodeListRef.Alias == "RegionsAlias");
            }

            var europeCode = hirchy.GetCodeRefs().Where(i => i.CodeId == "Europe").Single();
            Assert.IsNull(europeCode.Parent);
            var germany = hirchy.GetCodeRefs().Where(i => i.CodeId == "Germany").Single();
            var uk = hirchy.GetCodeRefs().Where(i => i.CodeId == "UK").Single();
            Assert.AreSame(germany.Parent, europeCode);
            Assert.AreSame(uk.Parent, europeCode);
        }

        [Test]
        public void Hierarchy_GetCodeRefs()
        {
            var countryCL = GetCountryCodeList();
            var regionsCL = GetRegionsCodeList();

            Hierarchy hierarchy = new Hierarchy("id",
                new CodeRef(regionsCL.Get("Europe"),
                    new CodeRef(countryCL.Get("Germany")),
                    new CodeRef(countryCL.Get("UK"))));

            var codeRefs = hierarchy.GetCodeRefs();

            Assert.AreEqual(3, codeRefs.Count());
        }

        private static CodeList GetCountryCodeList()
        {
            var codeList = new CodeList(new InternationalString("en", "Countries"), "CL_Country", "SDMX");
            codeList.Add(new Code("Japan"));
            codeList.Add(new Code("USA"));
            codeList.Add(new Code("UK"));
            codeList.Add(new Code("Germany"));
            codeList.Add(new Code("Brazil"));
            return codeList;
        }

        private static CodeList GetRegionsCodeList()
        {
            var codeList = new CodeList(new InternationalString("en", "Regions"), "CL_Regions", "SDMX");
            codeList.Add(new Code("Europe"));
            codeList.Add(new Code("North_America"));
            codeList.Add(new Code("Asia"));
            codeList.Add(new Code("Africa"));
            return codeList;
        }
    }
}