using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace RfidProgrammer
{
    public static class XmlHelper
    {
        // based on https://stackoverflow.com/a/44410537/5516047
        public static T ReadXmlFile<T>(string path, Type explicitType = null)
        {
            if (explicitType == null)
                explicitType = typeof(T);
            T result = default(T);
            if (File.Exists(path))
            {
                using (XmlReader reader = XmlReader.Create(path))
                {
                    DataContractSerializer serializer = new DataContractSerializer(explicitType);
                    result = (T)serializer.ReadObject(reader);
                }
            }
            return result;
        }

        public static void WriteXmlFile(string path, object obj)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            
            using (XmlWriter writer = XmlWriter.Create(path,
                                                       new XmlWriterSettings
                                                       {
                                                           Indent = true,
                                                           IndentChars = "  ",
                                                           Encoding = Encoding.UTF8,
                                                           CloseOutput = true
                                                       }))
            {
                DataContractSerializer serializer = new DataContractSerializer(obj.GetType());
                serializer.WriteObject(writer, obj);
            }
        }
    }
}
