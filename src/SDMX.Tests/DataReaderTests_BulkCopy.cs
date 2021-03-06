using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using NUnit.Framework;
using System.Xml.Linq;
using System.Linq;
using System.Xml;
using System.Collections.Generic;

namespace SDMX.Tests
{
    public partial class DataReaderTests : TestBase
    {
        [Test]
        [Ignore]
        public void BulkCopy()
        {
            var structure = StructureMessage.Load(Utility.GetPath("lib\\StructureSample.xml"));
            string dataPath = Utility.GetPath("lib\\GenericSample.xml");
            var keyFamily = structure.KeyFamilies[0];

            using (var reader = DataReader.Create(dataPath, keyFamily))
            {
                reader.Map("TIME", "TIME", i => i.ToString());

                var table = ((IDataReader)reader).GetSchemaTable();

                CreateTable(table);

                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(_connectionString))
                {
                    bulkCopy.DestinationTableName = table.TableName;
                    bulkCopy.WriteToServer(reader);
                }

                int count = 0;
                ExecuteReader("select count(*) from dbo." + table.TableName, r => count = (int)r[0]);
                Assert.AreNotEqual(0, count);
            }
        }

        [Test]
        [Ignore]
        public void LoadTest()
        {
            var list = new List<string>() 
                { 
                    "aei_ps_alt.sdmx.zip",
                    "apri_ap_him.sdmx.zip",
                    //"apro_cpb_cerea.sdmx.zip", // has errors
                    //"apro_cpp_crop.sdmx.zip",  // has errors
                    "avia_ac_fatal.sdmx.zip",
                    "avia_ac_number.sdmx.zip",
                    "avia_ec_enterp.sdmx.zip",
                    //"avia_paexcc.sdmx.zip", // takes long time
                    "avia_tf_alc.sdmx.zip"
                };

            ZipFile zf = null;

            Stream dsdStream = null;
            Stream dataStream = null;
            foreach (var item in list)
            {
                using (FileStream fs = File.OpenRead(Utility.GetPath("lib\\" + item)))
                {
                    zf = new ZipFile(fs);

                    foreach (ZipEntry zipEntry in zf)
                    {
                        if (zipEntry.Name.Contains("dsd"))
                            dsdStream = zf.GetInputStream(zipEntry);
                        else
                            dataStream = zf.GetInputStream(zipEntry);
                    }

                    Console.WriteLine(item);
                    Insert(dataStream, dsdStream);
                }
            }
        }

        void Insert(Stream dataStream, Stream dsdStream)
        {
            var doc = XDocument.Load(dsdStream);
            doc.Descendants().Where(i => i.Name.LocalName == "TextFormat" && i.Parent.Name.LocalName == "TimeDimension").Single().Remove();

            var mngr = new XmlNamespaceManager(new NameTable());
            mngr.AddNamespace("structure", "http://www.SDMX.org/resources/SDMXML/schemas/v2_0/structure");
            mngr.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instanc");
            mngr.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");

            XElement xElementParse = ParseElement("<structure:Concept id='TIME_FORMAT'><structure:Name xml:lang='en'>Time format</structure:Name></structure:Concept>", mngr);
            doc.Descendants().Where(i => i.Name.LocalName == "ConceptScheme").Single().Add(xElementParse);


            using (var xReader = doc.CreateReader())
            {
                var structure = StructureMessage.Read(xReader);
                var keyFamily = structure.KeyFamilies[0];

                using (var reader = DataReader.Create(dataStream, keyFamily))
                {
                    reader.Map("TIME_PERIOD", "TIME_PERIOD", i => ((TimePeriod)i).Year);

                    var table = ((IDataReader)reader).GetSchemaTable();

                    CreateTable(table);

                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(_connectionString))
                    {
                        bulkCopy.BatchSize = 10000;
                        bulkCopy.DestinationTableName = table.TableName;
                        bulkCopy.WriteToServer(reader);
                    }

                    int count = 0;
                    ExecuteReader("select count(*) from dbo." + table.TableName, r => count = (int)r[0]);
                    Assert.AreNotEqual(0, count);
                }
            }
        }

        public static XElement ParseElement(string strXml, XmlNamespaceManager mngr)
        {
            XmlParserContext parserContext = new XmlParserContext(null, mngr, null, XmlSpace.None);
            XmlTextReader txtReader = new XmlTextReader(strXml, XmlNodeType.Element, parserContext);
            return XElement.Load(txtReader);
        }
    }
}
