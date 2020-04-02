using System.Collections.Generic;
using System.Xml.Serialization;

namespace Xrm.DataManager.Framework
{
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
}
