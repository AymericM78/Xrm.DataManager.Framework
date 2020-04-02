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
}
