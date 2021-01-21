using System;
using System.Xml.Serialization;

namespace Xrm.DataManager.Framework
{
    [Obsolete("You must use connection string with oAuth.")]
    [Serializable()]
    public class Instance
    {
        [XmlAttribute("Name")]
        public string Name
        {
            get; set;
        }

        [XmlAttribute("UniqueName")]
        public string UniqueName
        {
            get; set;
        }

        [XmlAttribute("DisplayName")]
        public string DisplayName
        {
            get; set;
        }

        [XmlAttribute("ConnectionString")]
        public string ConnectionString
        {
            get; set;
        }

        [XmlAttribute("Url")]
        public string Url
        {
            get; set;
        }
    }
}
