using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using PlistSharper.src.Enums;
using PlistSharper.src.Models;
using PlistSharper.src.Utilities;

namespace PlistSharper.src
{
    public class PlistReader
    {
        public static PlistReader _instance;
        public static object syncRoot = new object();

        private PlistReader()
        {

        }

        public static PlistReader SharedInstance
        {
            get
            {
                if(_instance == null)
                {
                    lock(syncRoot)
                    {
                        if(_instance == null)
                        {
                            _instance = new PlistReader();
                        }
                    }
                }

                return _instance;
            }
        }

        public object ReadPlist(string path)
        {
            using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return ReadPlist(f, PlistType.Auto);
            }
        }

        public object ReadPlistSource(string source)
        {
            return ReadPlistAsByteArray(System.Text.Encoding.UTF8.GetBytes(source));
        }

        public object ReadPlistAsByteArray(byte[] data)
        {
            return ReadPlist(new MemoryStream(data), PlistType.Auto);
        }

        public PlistType GetPlistType(Stream stream)
        {
            byte[] magicHeader = new byte[8];
            stream.Read(magicHeader, 0, 8);

            if (BitConverter.ToInt64(magicHeader, 0) == 3472403351741427810)
            {
                return PlistType.Binary;
            }
            else
            {
                return PlistType.Xml;
            }
        }

        public object ReadPlist(Stream stream, PlistType type)
        {
            if (type == PlistType.Auto)
            {
                type = GetPlistType(stream);
                stream.Seek(0, SeekOrigin.Begin);
            }

            if (type == PlistType.Binary)
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    byte[] data = reader.ReadBytes((int)reader.BaseStream.Length);
                    return ReadBinary(data);
                }
            }
            else
            {
                XmlDocument xml = new XmlDocument();
                xml.XmlResolver = null;
                xml.Load(stream);
                return ReadXml(xml);
            }
        }

        public static object ReadXml(XmlDocument xml)
        {
            var rootNode = xml.DocumentElement.ChildNodes[0];
            return XmlParser.ParseNode(rootNode);      
        }

        public static object ReadBinary(byte[] data)
        {
            SharedDataSet.OffsetTable.Clear();
            List<byte> offsetTableBytes = new List<byte>();
            SharedDataSet.ObjectTable.Clear();
            SharedDataSet.ReferenceCount = 0;
            SharedDataSet.ObjectReferenceSize = 0;
            SharedDataSet.OffsetByteSize = 0;
            SharedDataSet.OffSetTableSize = 0;

            List<byte> bList = new List<byte>(data);

            List<byte> trailer = bList.GetRange(bList.Count - 32, 32);
            XmlParser.ParseTrailer(trailer);

            SharedDataSet.ObjectTable = bList.GetRange(0, (int)SharedDataSet.OffSetTableSize);

            offsetTableBytes = bList.GetRange((int)SharedDataSet.OffSetTableSize, bList.Count - (int)SharedDataSet.OffSetTableSize - 32);

            XmlParser.ParseOffsetTable(offsetTableBytes);

            return XmlParser.ParseBinary(0);
        }
    }
} 