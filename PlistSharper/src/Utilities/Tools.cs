using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using PlistSharper.src.Models;
using static XmlParser;

namespace PlistSharper.src.Utilities
{
    public static class Tools
    {
        public static void Compose(object value, XmlWriter writer)
        {

            if (value == null || value is string)
            {
                writer.WriteElementString("string", value as string);
            }
            else if (value is int || value is long)
            {
                writer.WriteElementString("integer", ((int)value).ToString(System.Globalization.NumberFormatInfo.InvariantInfo));
            }
            else if (value is System.Collections.Generic.Dictionary<string, object> ||
              value.GetType().ToString().StartsWith("System.Collections.Generic.Dictionary`2[System.String", StringComparison.Ordinal))
            {
                //Convert to Dictionary<string, object>
                if (!(value is Dictionary<string, object> dic))
                {
                    dic = new Dictionary<string, object>();
                    IDictionary idic = (IDictionary)value;
                    foreach (var key in idic.Keys)
                    {
                        dic.Add(key.ToString(), idic[key]);
                    }
                }
                WriteDictionaryValues(dic, writer);
            }
            else if (value is List<object>)
            {
                ComposeArray((List<object>)value, writer);
            }
            else if (value is byte[])
            {
                writer.WriteElementString("data", Convert.ToBase64String((Byte[])value));
            }
            else if (value is float || value is double)
            {
                writer.WriteElementString("real", ((double)value).ToString(System.Globalization.NumberFormatInfo.InvariantInfo));
            }
            else if (value is DateTime)
            {
                DateTime time = (DateTime)value;
                string theString = XmlConvert.ToString(time, XmlDateTimeSerializationMode.Utc);
                writer.WriteElementString("date", theString);//, "yyyy-MM-ddTHH:mm:ssZ"));
            }
            else if (value is bool)
            {
                writer.WriteElementString(value.ToString().ToLower(), "");
            }
            else
            {
                throw new Exception(String.Format("Value type '{0}' is unhandled", value.GetType().ToString()));
            }
        }


        public static void WriteDictionaryValues(Dictionary<string, object> dictionary, XmlWriter writer)
        {
            writer.WriteStartElement("dict");
            foreach (string key in dictionary.Keys)
            {
                object value = dictionary[key];
                writer.WriteElementString("key", key);
                Compose(value, writer);
            }
            writer.WriteEndElement();
        }


        public static void ComposeArray(List<object> value, XmlWriter writer)
        {
            writer.WriteStartElement("array");
            foreach (object obj in value)
            {
                Compose(obj, writer);
            }
            writer.WriteEndElement();
        }


        public static byte[] RegulateNullBytes(byte[] value)
        {
            return RegulateNullBytes(value, 1);
        }

        public static byte[] RegulateNullBytes(byte[] value, int minBytes)
        {
            Array.Reverse(value);
            List<byte> bytes = new List<byte>(value);
            for (int i = 0; i < bytes.Count; i++)
            {
                if (bytes[i] == 0 && bytes.Count > minBytes)
                {
                    bytes.Remove(bytes[i]);
                    i--;
                }
                else
                    break;
            }

            if (bytes.Count < minBytes)
            {
                int dist = minBytes - bytes.Count;
                for (int i = 0; i < dist; i++)
                    bytes.Insert(0, 0);
            }

            value = bytes.ToArray();
            Array.Reverse(value);
            return value;
        }

        public static byte[] ComposeBinaryArray(List<object> objects)
        {
            List<byte> buffer = new List<byte>();
            List<byte> header = new List<byte>();
            List<int> refs = new List<int>();

            for (int i = objects.Count - 1; i >= 0; i--)
            {
                ComposeBinary(objects[i]);
                SharedDataSet.OffsetTable.Add(SharedDataSet.OffsetTable.Count);
                refs.Add(SharedDataSet.ReferenceCount);
                SharedDataSet.ReferenceCount--;
            }

            if (objects.Count < 15)
            {
                header.Add(Convert.ToByte(0xA0 | Convert.ToByte(objects.Count)));
            }
            else
            {
                header.Add(0xA0 | 0xf);
                header.AddRange(PlistWriter.SharedInstance.WriteBinartInteger(objects.Count, false));
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

        public static byte[] ComposeBinary(object obj)
        {
            byte[] value;
            switch (obj.GetType().ToString())
            {
                case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                    value = PlistWriter.SharedInstance.WriteBinaryDictionary((Dictionary<string, object>)obj);
                    return value;

                case "System.Collections.Generic.List`1[System.Object]":
                    value = ComposeBinaryArray((List<object>)obj);
                    return value;

                case "System.Byte[]":
                    value = PlistWriter.SharedInstance.WriteBinaryByteArray((byte[])obj);
                    return value;

                case "System.Double":
                    value = PlistWriter.SharedInstance.WriteBinaryDouble((double)obj);
                    return value;

                case "System.Int32":
                    value = PlistWriter.SharedInstance.WriteBinartInteger((int)obj, true);
                    return value;

                case "System.String":
                    value = PlistWriter.SharedInstance.WriteBinaryString((string)obj, true);
                    return value;

                case "System.DateTime":
                    value = PlistWriter.SharedInstance.WriteBinaryDate((DateTime)obj);
                    return value;

                case "System.Boolean":
                    value = PlistWriter.SharedInstance.WriteBinaryBool((bool)obj);
                    return value;

                default:
                    return new byte[0];
            }
        } 
    }
}
