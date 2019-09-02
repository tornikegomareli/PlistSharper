using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using PlistSharper.src.Models;
using PlistSharper.src.Utilities;

public static class XmlParser
{
    public static Dictionary<string, object> ParseDictionary(XmlNode node)
    {
        XmlNodeList children = node.ChildNodes;
        if (children.Count % 2 != 0)
        {
            throw new DataMisalignedException("Dictionary elements must have an even number of child nodes");
        }

        Dictionary<string, object> dict = new Dictionary<string, object>();

        for (int i = 0; i < children.Count; i += 2)
        {
            XmlNode keynode = children[i];
            XmlNode valnode = children[i + 1];

            if (keynode.Name != "key")
            {
                throw new ApplicationException("expected a key node");
            }

            object result = ParseNode(valnode);

            if (result != null)
            {
                dict.Add(keynode.InnerText, result);
            }
        }

        return dict;
    }


    public static List<object> ParseArray(XmlNode node)
    {
        List<object> array = new List<object>();

        foreach (XmlNode child in node.ChildNodes)
        {
            object result = ParseNode(child);
            if (result != null)
            {
                array.Add(result);
            }
        }

        return array;
    }

    public static object ParseNode(XmlNode node)
    {
        switch (node.Name)
        {
            case "dict":
                return ParseDictionary(node);
            case "array":
                return ParseArray(node);
            case "string":
                return node.InnerText;
            case "integer":
                //  int result;
                //int.TryParse(node.InnerText, System.Globalization.NumberFormatInfo.InvariantInfo, out result);
                return Convert.ToInt32(node.InnerText, System.Globalization.NumberFormatInfo.InvariantInfo);
            case "real":
                return Convert.ToDouble(node.InnerText, System.Globalization.NumberFormatInfo.InvariantInfo);
            case "false":
                return false;
            case "true":
                return true;
            case "null":
                return null;
            case "date":
                return XmlConvert.ToDateTime(node.InnerText, XmlDateTimeSerializationMode.Utc);
            case "data":
                return Convert.FromBase64String(node.InnerText);
        }

        throw new ApplicationException(String.Format("Plist Node `{0}' is not supported", node.Name));
    }

    public static void ParseTrailer(List<byte> trailer)
    {
        SharedDataSet.OffsetByteSize = BitConverter.ToInt32(Tools.RegulateNullBytes(trailer.GetRange(6, 1).ToArray(), 4), 0);
        SharedDataSet.ObjectReferenceSize= BitConverter.ToInt32(Tools.RegulateNullBytes(trailer.GetRange(7, 1).ToArray(), 4), 0);
        byte[] refCountBytes = trailer.GetRange(12, 4).ToArray();
        Array.Reverse(refCountBytes);
        SharedDataSet.ReferenceCount = BitConverter.ToInt32(refCountBytes, 0);
        byte[] offsetTableOffsetBytes = trailer.GetRange(24, 8).ToArray();
        Array.Reverse(offsetTableOffsetBytes);
        SharedDataSet.OffSetTableSize = BitConverter.ToInt64(offsetTableOffsetBytes, 0);
    }

    public static void ParseOffsetTable(List<byte> offsetTableBytes)
    {
        for (int i = 0; i < offsetTableBytes.Count; i += SharedDataSet.OffsetByteSize)
        {
            byte[] buffer = offsetTableBytes.GetRange(i, SharedDataSet.OffsetByteSize).ToArray();
            Array.Reverse(buffer);
            SharedDataSet.OffsetTable.Add(BitConverter.ToInt32(Tools.RegulateNullBytes(buffer, 4), 0));
        }
    }


    public static object ParseBinary(int objRef)
    {
        byte header = SharedDataSet.ObjectTable[SharedDataSet.OffsetTable[objRef]];
        switch (header & 0xF0)
        {
            case 0:
                {
                    //If the byte is
                    //0 return null
                    //9 return true
                    //8 return false
                    return (SharedDataSet.ObjectTable[SharedDataSet.OffsetTable[objRef]] == 0) ? (object)null : ((SharedDataSet.ObjectTable[SharedDataSet.OffsetTable[objRef]] == 9) ? true : false);
                }
            case 0x10:
                {
                    return ParseBinaryInt(SharedDataSet.OffsetTable[objRef]);
                }
            case 0x20:
                {
                    return ParseBinaryReal(SharedDataSet.OffsetTable[objRef]);
                }
            case 0x30:
                {
                    return ParseBinaryDate(SharedDataSet.OffsetTable[objRef]);
                }
            case 0x40:
                {
                    return ParseBinaryByteArray(SharedDataSet.OffsetTable[objRef]);
                }
            case 0x50://String ASCII
                {
                    return ParseBinaryAsciiString(SharedDataSet.OffsetTable[objRef]);
                }
            case 0x60://String Unicode
                {
                    return ParseBinaryUnicodeString(SharedDataSet.OffsetTable[objRef]);
                }
            case 0xD0:
                {
                    return parseBinaryDictionary(objRef);
                }
            case 0xA0:
                {
                    return ParseBinaryArray(objRef);
                }
        }
        throw new Exception("This type is not supported");
    }

    public static object ParseBinaryDate(int headerPosition)
    {
        byte[] buffer = SharedDataSet.ObjectTable.GetRange(headerPosition + 1, 8).ToArray();
        Array.Reverse(buffer);
        double appleTime = BitConverter.ToDouble(buffer, 0);
        DateTime result = PlistDateConverter.ConvertFromAppleTimeStamp(appleTime);
        return result;
    } 

    public static object ParseBinaryInt(int headerPosition)
    {
        int output;
        return ParseBinaryInt(headerPosition, out output);
    }

    public static object ParseBinaryInt(int headerPosition, out int newHeaderPosition)
    {
        byte header = SharedDataSet.ObjectTable[headerPosition];
        int byteCount = (int)Math.Pow(2, header & 0xf);
        byte[] buffer = SharedDataSet.ObjectTable.GetRange(headerPosition + 1, byteCount).ToArray();
        Array.Reverse(buffer);
        //Add one to account for the header byte
        newHeaderPosition = headerPosition + byteCount + 1;
        return BitConverter.ToInt32(Tools.RegulateNullBytes(buffer, 4), 0);
    }

    public static object ParseBinaryReal(int headerPosition)
    {
        byte header = SharedDataSet.ObjectTable[headerPosition];
        int byteCount = (int)Math.Pow(2, header & 0xf);
        byte[] buffer = SharedDataSet.ObjectTable.GetRange(headerPosition + 1, byteCount).ToArray();
        Array.Reverse(buffer);

        return BitConverter.ToDouble(Tools.RegulateNullBytes(buffer, 8), 0);
    }

    public static object ParseBinaryAsciiString(int headerPosition)
    {
        int charStartPosition;
        int charCount = GetCount(headerPosition, out charStartPosition);

        var buffer = SharedDataSet.ObjectTable.GetRange(charStartPosition, charCount);
        return buffer.Count > 0 ? Encoding.ASCII.GetString(buffer.ToArray()) : string.Empty;
    }

    public static object ParseBinaryUnicodeString(int headerPosition)
    {
        int charStartPosition;
        int charCount = GetCount(headerPosition, out charStartPosition);
        charCount = charCount * 2;

        byte[] buffer = new byte[charCount];
        byte one, two;

        for (int i = 0; i < charCount; i += 2)
        {
            one = SharedDataSet.ObjectTable.GetRange(charStartPosition + i, 1)[0];
            two = SharedDataSet.ObjectTable.GetRange(charStartPosition + i + 1, 1)[0];

            if (BitConverter.IsLittleEndian)
            {
                buffer[i] = two;
                buffer[i + 1] = one;
            }
            else
            {
                buffer[i] = one;
                buffer[i + 1] = two;
            }
        }

        return Encoding.Unicode.GetString(buffer);
    }

    public static object ParseBinaryByteArray(int headerPosition)
    {
        int byteStartPosition;
        int byteCount = GetCount(headerPosition, out byteStartPosition);
        return SharedDataSet.ObjectTable.GetRange(byteStartPosition, byteCount).ToArray();
    
    }

    public static object parseBinaryDictionary(int objRef)
    {
        Dictionary<string, object> buffer = new Dictionary<string, object>();
        List<int> refs = new List<int>();
        int refCount = 0;

        int refStartPosition;
        refCount = GetCount(SharedDataSet.OffsetTable[objRef], out refStartPosition);


        if (refCount < 15)
            refStartPosition = SharedDataSet.OffsetTable[objRef] + 1;
        else
            refStartPosition = SharedDataSet.OffsetTable[objRef] + 2 + Tools.RegulateNullBytes(BitConverter.GetBytes(refCount), 1).Length;

        for (int i = refStartPosition; i < refStartPosition + refCount * 2 * SharedDataSet.ObjectReferenceSize; i += SharedDataSet.ObjectReferenceSize)
        {
            byte[] refBuffer = SharedDataSet.ObjectTable.GetRange(i, SharedDataSet.ObjectReferenceSize).ToArray();
            Array.Reverse(refBuffer);
            refs.Add(BitConverter.ToInt32(Tools.RegulateNullBytes(refBuffer, 4), 0));
        }

        for (int i = 0; i < refCount; i++)
        {
            buffer.Add((string)ParseBinary(refs[i]), ParseBinary(refs[i + refCount]));
        }

        return buffer;
    }

    public static int GetCount(int bytePosition, out int newBytePosition)
    {
        byte headerByte = SharedDataSet.ObjectTable[bytePosition];
        byte headerByteTrail = Convert.ToByte(headerByte & 0xf);
        int count;
        if (headerByteTrail < 15)
        {
            count = headerByteTrail;
            newBytePosition = bytePosition + 1;
        }
        else
            count = (int)ParseBinaryInt(bytePosition + 1, out newBytePosition);
        return count;
    }

    public static object ParseBinaryArray(int objRef)
    {
        List<object> buffer = new List<object>();
        List<int> refs = new List<int>();
        int refCount = 0;

        int refStartPosition;
        refCount = GetCount(SharedDataSet.OffsetTable[objRef], out refStartPosition);


        if (refCount < 15)
            refStartPosition = SharedDataSet.OffsetTable[objRef] + 1;
        else
            //The following integer has a header aswell so we increase the refStartPosition by two to account for that.
            refStartPosition = SharedDataSet.OffsetTable[objRef] + 2 + Tools.RegulateNullBytes(BitConverter.GetBytes(refCount), 1).Length;

        for (int i = refStartPosition; i < refStartPosition + refCount * SharedDataSet.ObjectReferenceSize; i += SharedDataSet.ObjectReferenceSize)
        {
            byte[] refBuffer = SharedDataSet.ObjectTable.GetRange(i, SharedDataSet.ObjectReferenceSize).ToArray();
            Array.Reverse(refBuffer);
            refs.Add(BitConverter.ToInt32(Tools.RegulateNullBytes(refBuffer, 4), 0));
        }

        for (int i = 0; i < refCount; i++)
        {
            buffer.Add(ParseBinary(refs[i]));
        }

        return buffer;
    
    }


    public static class PlistDateConverter
    {
        public static long timeDifference = 978307200;

        public static long GetAppleTime(long unixTime)
        {
            return unixTime - timeDifference;
        }

        public static long GetUnixTime(long appleTime)
        {
            return appleTime + timeDifference;
        }

        public static DateTime ConvertFromAppleTimeStamp(double timestamp)
        {
            DateTime origin = new DateTime(2001, 1, 1, 0, 0, 0, 0);
            return origin.AddSeconds(timestamp);
        }

        public static double ConvertToAppleTimeStamp(DateTime date)
        {
            DateTime begin = new DateTime(2001, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date - begin;
            return Math.Floor(diff.TotalSeconds);
        }
    }

    public static int CountObject(object value)
    {
        int count = 0;
        switch (value.GetType().ToString())
        {
            case "System.Collections.Generic.Dictionary`2[System.String,System.Object]":
                Dictionary<string, object> dict = (Dictionary<string, object>)value;
                foreach (string key in dict.Keys)
                {
                    count += CountObject(dict[key]);
                }
                count += dict.Keys.Count;
                count++;
                break;
            case "System.Collections.Generic.List`1[System.Object]":
                List<object> list = (List<object>)value;
                foreach (object obj in list)
                {
                    count += CountObject(obj);
                }
                count++;
                break;
            default:
                count++;
                break;
        }
        return count;
    }
}

