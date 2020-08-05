using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ParseBCSV
{
    public class BCSVParse
    {
        public class Field
        {
            public uint Hash { get; set; }
            public uint Offset { get; set; }
        }

        public class DataEntry
        {
            public Dictionary<string, object> Fields;
        }

        private static readonly Dictionary<uint, string> Hashes = new Dictionary<uint, string>();
        private static readonly Dictionary<uint, string> Mm3Hashes = new Dictionary<uint, string>();

        public static Dictionary<uint, string> HashDict
        {
            get
            {
                if (Hashes.Count == 0)
                {
                    CalculateHashes();
                }

                return Hashes;
            }
        }

        public List<DataEntry> Entries = new List<DataEntry>();

        private static bool OnlyHexInString(string test)
        {
            return test.Length >= 5 && Regex.IsMatch(test, @"\A\b[0-9a-fA-F]+\b\Z");
        }

        private static string ReadZeroTerminatedString(ref BinaryReader stream, Encoding encoding)
        {
            var byteCount = encoding.GetByteCount("a");
            var byteList = new List<byte>();
            switch (byteCount)
            {
                case 1:
                    for (var index = stream.ReadByte(); index != (byte)0; index = stream.ReadByte())
                        byteList.Add(index);
                    break;
                case 2:
                    for (var index = (uint)stream.ReadUInt16(); index != 0U; index = (uint)stream.ReadUInt16())
                    {
                        var bytes = BitConverter.GetBytes(index);
                        byteList.Add(bytes[0]);
                        byteList.Add(bytes[1]);
                    }
                    break;
            }

            return encoding.GetString(byteList.ToArray());
        }

        public void Read(BinaryReader reader)
        {
            var numEntries = reader.ReadUInt32();
            var entrySize = reader.ReadUInt32();
            var numFields = reader.ReadUInt16();
            var flag1 = reader.ReadByte();
            reader.ReadByte();
            if (flag1 == 1)
            {
                reader.ReadUInt32();
                reader.ReadUInt32(); //Always 100000
                reader.ReadUInt32(); //0
                reader.ReadUInt32(); //0
            }

            var fields = new Field[numFields];
            for (var i = 0; i < numFields; i++)
            {
                fields[i] = new Field()
                {
                    Hash = reader.ReadUInt32(),
                    Offset = reader.ReadUInt32(),
                };
            }
            for (var i = 0; i < numEntries; i++)
            {
                var entry = new DataEntry();
                Entries.Add(entry);
                entry.Fields = new Dictionary<string, object>();

                var pos = reader.BaseStream.Position;
                for (var f = 0; f < fields.Length; f++)
                {
                    var type = DataType.String;
                    var size = entrySize - fields[f].Offset;
                    if (f < fields.Length - 1)
                    {
                        size = fields[f + 1].Offset - fields[f].Offset;
                    }
                    if (size == 1)
                        type = DataType.Byte;
                    if (size == 2)
                        type = DataType.Int16;
                    if (size == 4)
                        type = DataType.Int32;

                    reader.BaseStream.Seek(pos + fields[f].Offset, SeekOrigin.Begin);
                    object value = 0;
                    var name = fields[f].Hash.ToString("x");

                    var hashType = "";

                    if (HashDict.ContainsKey(fields[f].Hash))
                    {
                        name = HashDict[fields[f].Hash].Split(' ')[0];
                        hashType = HashDict[fields[f].Hash];
                    }

                    switch (type)
                    {
                        case DataType.Byte:
                            value = reader.ReadByte();
                            break;
                        case DataType.Float:
                            value = reader.ReadSingle();
                            break;
                        case DataType.Int16:
                            value = reader.ReadInt16();
                            break;
                        case DataType.Int32:
                            value = reader.ReadInt32();

                            var checkVal = BitConverter.ToUInt32(BitConverter.GetBytes((int) value), 0);

                            if (HashDict.ContainsKey(checkVal) && checkVal > 0)
                            {
                                value = HashDict[checkVal];
                                type = DataType.String;
                                break;
                            }

                            if (Mm3Hashes.ContainsKey(checkVal) && checkVal > 0)
                            {
                                value = Mm3Hashes[checkVal];
                                type = DataType.String;
                                break;
                            }

                            if ((name.Contains(".hshCstringRef") || name.Contains(".HashRef") || hashType.Contains("string")) && checkVal != 0 || name.Contains(".HashRef"))
                            {
                                value = checkVal.ToString("X");
                                type = DataType.String;
                                break;
                            }

                            if (value.ToString().Length > 6)
                            {
                                reader.BaseStream.Seek(-4, SeekOrigin.Current);
                                value = reader.ReadSingle();
                                type = DataType.Float;
                            }

                            if (value.ToString().Contains("E+") || value.ToString().Contains("E-"))
                            {
                                value = checkVal.ToString("X");
                                type = DataType.String;
                            }

                            break;
                        case DataType.String:
                            value = ReadZeroTerminatedString(ref reader, Encoding.UTF8);

                            if (!OnlyHexInString(value.ToString()) && !value.ToString().Contains("|"))
                            {
                                break;
                            }

                            var result = "";

                            var spl = value.ToString().Split('|');

                            foreach (var s in spl)
                            {
                                if (!OnlyHexInString(s))
                                {
                                    result += s + "|";
                                    continue;
                                }

                                var sHash = Convert.ToUInt32(s, 16);

                                if (HashDict.ContainsKey(sHash) && sHash > 0)
                                {
                                    result += HashDict[sHash] + "|";
                                    continue;
                                }

                                if (Mm3Hashes.ContainsKey(sHash) && sHash > 0)
                                {
                                    result += Mm3Hashes[sHash] + "|";
                                }
                            }

                            value = result.TrimEnd('|');
                            break;
                    }

                    if (HashDict.ContainsKey(fields[f].Hash))
                    {
                        name = HashDict[fields[f].Hash].Split(' ')[0];
                    }
                    else
                    {
                        if (type == DataType.String)
                        {
                            if (size > 4)
                            {
                                name += " string" + size;
                            }
                            else
                            {
                                name += " string";
                            }
                        }
                        else
                        {
                            switch (type)
                            {
                                case DataType.Byte:
                                    name += " u8";
                                    break;
                                case DataType.Int16:
                                    name += " u16";
                                    break;
                                case DataType.Int32:
                                    name += " u32";
                                    break;
                                case DataType.Float:
                                    name += " f32";
                                    break;
                            }
                        }
                    }

                    entry.Fields.Add(name.Replace(".hshCstringRef", ""), value);
                }
                reader.BaseStream.Seek(pos + entrySize, SeekOrigin.Begin);
            }
        }
        
        public enum DataType
        {
            Byte,
            Int16,
            Int32,
            Float,
            String,
        }

        private static void CalculateHashes()
        {
            var ass = System.Reflection.Assembly.GetExecutingAssembly();
            var names = ass.GetManifestResourceNames();
            foreach (var resName in names)
            {
                var res = ass.GetManifestResourceStream(resName);
                if (res == null) continue;

                using var reader = new StreamReader(res, Encoding.UTF8);
                var str = reader.ReadToEnd().Split("\r\n");
                foreach (var hashStr in str)
                {
                    CheckHash(hashStr);
                }
            }
        }

        private static void CheckHash(string hashStr)
        {
            var hash = Crc32.Compute(hashStr);
            if (!Hashes.ContainsKey(hash))
            {
                Hashes.Add(hash, hashStr);
            }

            var mm3Hash = MurMurHash3.Hash(hashStr);
            if (!Mm3Hashes.ContainsKey(mm3Hash))
            {
                Mm3Hashes.Add(mm3Hash, hashStr);
            }
        }
    }
}
