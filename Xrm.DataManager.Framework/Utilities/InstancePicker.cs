using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Xrm.DataManager.Framework
{
    public static class InstancePicker
    {
        /// <summary>
        /// Return available instances
        /// </summary>
        /// <returns></returns>
        public static List<Instance> GetInstances()
        {
            var instanceList = new InstanceList();

            var serializer = new XmlSerializer(typeof(InstanceList), new XmlRootAttribute("Instances"));
            using (Stream reader = new FileStream("Instances.xml", FileMode.Open))
            {
                // Call the Deserialize method to restore the object's state.
                instanceList = (InstanceList)serializer.Deserialize(reader);
            }

            return instanceList.Items;
        }
    }

    [XmlRoot("Instances")]
    public class InstanceList
    {
        public InstanceList()
        {
            Items = new List<Instance>();
        }
        [XmlElement("Instance")]
        public List<Instance> Items
        {
            get; set;
        }
    }

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
