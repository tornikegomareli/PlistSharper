using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using PlistSharper.src.Models;
using PlistSharper.src.Utilities;
using static XmlParser;

namespace PlistSharper.src
{
    public class PlistWriter
    {
        public static PlistWriter _instance;
        public static object syncRoot = new object();

        public static PlistWriter SharedInstance
        {
            get
            {
                if(_instance == null)
                {
                    lock(syncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new PlistWriter();
                        }
                            
                    }
                }

                return _instance;
            }
        }





        private PlistWriter()
        {
        }

        public byte[] WriteBinaryDictionary(Dictionary<string, object> dictionary)
        {
            List<byte> buffer = new List<byte>();
            List<byte> header = new List<byte>();
            List<int> refs = new List<int>();
            for (int i = dictionary.Count - 1; i >= 0; i--)
            {
                var o = new object[dictionary.Count];
                dictionary.Values.CopyTo(o, 0);
                Tools.ComposeBinary(o[i]);
                SharedDataSet.OffsetTable.Add(SharedDataSet.ObjectTable.Count);
                refs.Add(SharedDataSet.ReferenceCount);
                SharedDataSet.ReferenceCount--;
            }
            for (int i = dictionary.Count - 1; i >= 0; i--)
            {
                var o = new string[dictionary.Count];
                dictionary.Keys.CopyTo(o, 0);
                Tools.ComposeBinary(o[i]);//);
                SharedDataSet.OffsetTable.Add(SharedDataSet.OffsetTable.Count);
                refs.Add(SharedDataSet.ReferenceCount);
                SharedDataSet.ReferenceCount--;
            }

            if (dictionary.Count < 15)
            {
                header.Add(Convert.ToByte(0xD0 | Convert.ToByte(dictionary.Count)));
            }
            else
            {
                header.Add(0xD0 | 0xf);
                header.AddRange(WriteBinartInteger(dictionary.Count, false));
            }


            foreach (int val in refs)
            {
                byte[] refBuffer = Tools.RegulateNullBytes(BitConverter.GetBytes(val), SharedDataSet.ObjectReferenceSize);
                Array.Reverse(refBuffer);
                buffer.InsertRange(0, refBuffer);
            }

            buffer.InsertRange(0, header);


            SharedDataSet.ObjectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }


        public void writeXml(object value, string path)
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.Write(WriteXml(value));
            }
        }

        public  void writeXml(object value, Stream stream)
        {
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(WriteXml(value));
            }
        }

        public string WriteXml(object value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
                xmlWriterSettings.Encoding = new System.Text.UTF8Encoding(false);
                xmlWriterSettings.ConformanceLevel = ConformanceLevel.Document;
                xmlWriterSettings.Indent = true;

                using (XmlWriter xmlWriter = XmlWriter.Create(ms, xmlWriterSettings))
                {
                    xmlWriter.WriteStartDocument();
                    //xmlWriter.WriteComment("DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " + "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\"");
                    xmlWriter.WriteDocType("plist", "-//Apple Computer//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);
                    xmlWriter.WriteStartElement("plist");
                    xmlWriter.WriteAttributeString("version", "1.0");
                    Tools.Compose(value, xmlWriter);
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteEndDocument();
                    xmlWriter.Flush();
                    xmlWriter.Close();
                    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }

        public void WriteBinary(object value, string path)
        {
            using (BinaryWriter writer = new BinaryWriter(new FileStream(path, FileMode.Create)))
            {
                writer.Write(WriteBinary(value));
            }
        }

        public void writeBinary(object value, Stream stream)
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(WriteBinary(value));
            }
        }

        public byte[] WriteBinary(object value)
        {
            SharedDataSet.OffsetTable.Clear();
            SharedDataSet.ObjectTable.Clear();
            SharedDataSet.ReferenceCount = 0;
            SharedDataSet.ObjectReferenceSize = 0;
            SharedDataSet.OffsetByteSize = 0;
            SharedDataSet.OffSetTableSize = 0;

            //Do not count the root node, subtract by 1
            int totalRefs = XmlParser.CountObject(value) - 1;

            SharedDataSet.ReferenceCount = totalRefs;

            SharedDataSet.ObjectReferenceSize = Tools.RegulateNullBytes(BitConverter.GetBytes(SharedDataSet.ReferenceCount)).Length;

            Tools.ComposeBinary(value);

            WriteBinaryString("bplist00", false);

            SharedDataSet.OffSetTableSize = (long)SharedDataSet.ObjectTable.Count;

            SharedDataSet.OffsetTable.Add(SharedDataSet.ObjectTable.Count - 8);

            SharedDataSet.OffsetByteSize = Tools.RegulateNullBytes(BitConverter.GetBytes(SharedDataSet.OffsetTable[SharedDataSet.OffsetTable.Count - 1])).Length;

            List<byte> offsetBytes = new List<byte>();

            SharedDataSet.OffsetTable.Reverse();

            for (int i = 0; i < SharedDataSet.OffsetTable.Count; i++)
            {
                SharedDataSet.OffsetTable[i] = SharedDataSet.OffsetTable.Count - SharedDataSet.OffsetTable[i];
                byte[] buffer = Tools.RegulateNullBytes(BitConverter.GetBytes(SharedDataSet.OffsetTable[i]), SharedDataSet.OffsetByteSize);
                Array.Reverse(buffer);
                offsetBytes.AddRange(buffer);
            }

            SharedDataSet.ObjectTable.AddRange(offsetBytes);

            SharedDataSet.ObjectTable.AddRange(new byte[6]);
            SharedDataSet.ObjectTable.Add(Convert.ToByte(SharedDataSet.OffsetByteSize));
            SharedDataSet.ObjectTable.Add(Convert.ToByte(SharedDataSet.ObjectReferenceSize));

            var a = BitConverter.GetBytes((long)totalRefs + 1);
            Array.Reverse(a);
            SharedDataSet.ObjectTable.AddRange(a);

            SharedDataSet.ObjectTable.AddRange(BitConverter.GetBytes((long)0));
            a = BitConverter.GetBytes(SharedDataSet.OffSetTableSize);
            Array.Reverse(a);
            SharedDataSet.ObjectTable.AddRange(a);

            return SharedDataSet.ObjectTable.ToArray();
        }

        public  byte[] WriteBinaryDate(DateTime obj)
        {
            List<byte> buffer = new List<byte>(Tools.RegulateNullBytes(BitConverter.GetBytes(PlistDateConverter.ConvertToAppleTimeStamp(obj)), 8));
            buffer.Reverse();
            buffer.Insert(0, 0x33);
            SharedDataSet.ObjectTable.InsertRange(0, buffer);
            return buffer.ToArray();
        }

        public  byte[] WriteBinaryBool(bool obj)
        {
            List<byte> buffer = new List<byte>(new byte[1] { (bool)obj ? (byte)9 : (byte)8 });
            SharedDataSet.ObjectTable.InsertRange(0, buffer);
            return buffer.ToArray();
        }

        public  byte[] WriteBinartInteger(int value, bool write)
        {
            List<byte> buffer = new List<byte>(BitConverter.GetBytes((long)value));
            buffer = new List<byte>(Tools.RegulateNullBytes(buffer.ToArray()));
            while (buffer.Count != Math.Pow(2, Math.Log(buffer.Count) / Math.Log(2)))
                buffer.Add(0);
            int header = 0x10 | (int)(Math.Log(buffer.Count) / Math.Log(2));

            buffer.Reverse();

            buffer.Insert(0, Convert.ToByte(header));

            if (write)
                SharedDataSet.ObjectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        public byte[] WriteBinaryDouble(double value)
        {
            List<byte> buffer = new List<byte>(Tools.RegulateNullBytes(BitConverter.GetBytes(value), 4));
            while (buffer.Count != Math.Pow(2, Math.Log(buffer.Count) / Math.Log(2)))
                buffer.Add(0);
            int header = 0x20 | (int)(Math.Log(buffer.Count) / Math.Log(2));

            buffer.Reverse();

            buffer.Insert(0, Convert.ToByte(header));

            SharedDataSet.ObjectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        public byte[] WriteBinaryByteArray(byte[] value)
        {
            List<byte> buffer = new List<byte>(value);
            List<byte> header = new List<byte>();
            if (value.Length < 15)
            {
                header.Add(Convert.ToByte(0x40 | Convert.ToByte(value.Length)));
            }
            else
            {
                header.Add(0x40 | 0xf);
                header.AddRange(WriteBinartInteger(buffer.Count, false));
            }

            buffer.InsertRange(0, header);

            SharedDataSet.ObjectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

        public byte[] WriteBinaryString(string value, bool head)
        {
            List<byte> buffer = new List<byte>();
            List<byte> header = new List<byte>();
            foreach (char chr in value.ToCharArray())
                buffer.Add(Convert.ToByte(chr));

            if (head)
            {
                if (value.Length < 15)
                {
                    header.Add(Convert.ToByte(0x50 | Convert.ToByte(value.Length)));
                }
                else
                {
                    header.Add(0x50 | 0xf);
                    header.AddRange(WriteBinartInteger(buffer.Count, false));
                }
            }

            buffer.InsertRange(0, header);

            SharedDataSet.ObjectTable.InsertRange(0, buffer);

            return buffer.ToArray();
        }

    }
}
