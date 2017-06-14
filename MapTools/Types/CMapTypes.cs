﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MapTools.Types
{
    public class CMapTypes
    {
        public object extensions { get; set; } //UNKNOWN
        public Dictionary<string,CBaseArchetypeDef> archetypes { get; set; }
        public string name { get; set; }
        public object dependencies { get; set; } //UNKNOWN
        public object compositeEntityTypes { get; set; } //UNKNOWN

        public CMapTypes(string filename)
        {
            archetypes = new Dictionary<string, CBaseArchetypeDef>();
            name = filename;
        }

        public CMapTypes(XElement node)
        {
            archetypes = new Dictionary<string, CBaseArchetypeDef>();
            if (node.Element("archetypes").Elements() != null && node.Element("archetypes").Elements().Count() > 0)
            {
                foreach (XElement arc in node.Element("archetypes").Elements())
                {
                    if (arc.Attribute("type").Value == "CBaseArchetypeDef")
                    {
                        CBaseArchetypeDef a = new CBaseArchetypeDef(arc);
                        archetypes.Add(a.name, a);
                    }  
                    else
                        Console.WriteLine("Skipped unsupported archetype: " + arc.Attribute("type").Value);
                }
            }
            name = node.Element("name").Value;
            dependencies = node.Element("dependencies");
            compositeEntityTypes = node.Element("compositeEntityTypes");
        }

        public void UpdatelodDist()
        {
            foreach (CBaseArchetypeDef arc in archetypes.Values)
            {
                arc.lodDist = 100 + (1.5f * arc.bsRadius);
                arc.hdTextureDist = 100 + arc.bsRadius;
            }
        }

        public XElement WriteXML()
        {
            //CMapTypes
            XElement CMapTypesField = new XElement("CMapTypes");

            //extensions
            XElement extensionsField = new XElement("extensions");
            CMapTypesField.Add(extensionsField);

            //archetypes
            XElement archetypesField = new XElement("archetypes");
            CMapTypesField.Add(archetypesField);

            if (archetypes != null && archetypes.Count > 0)
            {
                foreach (KeyValuePair<string,CBaseArchetypeDef> archetype in archetypes)
                    archetypesField.Add(archetype.Value.WriteXML());
            }

            //name
            XElement nameField = new XElement("name");
            nameField.Value = name;
            CMapTypesField.Add(nameField);

            //dependencies
            XElement dependenciesField = new XElement("dependencies");
            CMapTypesField.Add(dependenciesField);

            //compositeEntityTypes
            XElement compositeEntityTypesField = new XElement("compositeEntityTypes");
            CMapTypesField.Add(compositeEntityTypesField);

            return CMapTypesField;
        }
    }
}
