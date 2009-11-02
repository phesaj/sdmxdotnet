﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using Common;

namespace OXM
{
    public abstract class RoolElementMap<T> : ClassMap<T>
    {
        public abstract XName Name { get; }
        bool alreadybeenRead = false;

        private Dictionary<string, XNamespace> namespaces = new Dictionary<string, XNamespace>();

        public RoolElementMap()
        {            
            Namespace = Name.Namespace;
            _rootMap = this;
        }

        protected void RegisterNamespace(string prefix, XNamespace ns)
        {
            namespaces.Add(prefix, ns);
        }

        //internal void VerifyNamespace(XNamespace ns)
        //{
        //    if (namespaces.Values.Where(x => x == ns).Count() > 0)
        //    {
        //        throw new OXMException("Namespace '{0}' is not registered. Must be registered with the root element.");
        //    }
        //}

        public void WriteXml(XmlWriter writer, T obj)
        {
            writer.WriteStartElement(Name.LocalName, Name.NamespaceName);
            foreach (var item in namespaces)
            {
                writer.WriteAttributeString("xmlns", item.Key, null, item.Value.NamespaceName);

            }
            base.WriteXml(writer, obj);
            writer.WriteEndElement();
        }

        public T ReadXml(XmlReader reader)
        {
            if (alreadybeenRead)
            {
                throw new OXMException("Read operation has already been performed using this object map. Create a new map object to read again.");
            }
            else
            {
                alreadybeenRead = true;
            }
            
            if (reader.ReadState == ReadState.Initial)
            {
                reader.Read();
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.ReadNextElement();
            }

            if (!reader.NameEquals(Name))
            {
                throw new OXMException("The first element name is '{0}:{1}' and the expected name is '{2}'."
                    , reader.NamespaceURI, reader.Name, Name);
            }

            return base.ReadXml(reader);
        }
    }
}