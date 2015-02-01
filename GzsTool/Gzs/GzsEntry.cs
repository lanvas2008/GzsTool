﻿using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using GzsTool.Common;
using GzsTool.Utility;

namespace GzsTool.Gzs
{
    [XmlType("Entry", Namespace = "Gzs")]
    public class GzsEntry
    {
        [XmlAttribute("Hash")]
        public ulong Hash { get; set; }

        [XmlIgnore]
        public bool FileNameFound { get; set; }

        [XmlIgnore]
        public uint Offset { get; set; }

        [XmlIgnore]
        public uint Size { get; set; }

        [XmlAttribute("FilePath")]
        public string FilePath { get; set; }

        public bool ShouldSerializeHash()
        {
            return FileNameFound == false;
        }

        public static GzsEntry ReadGzsEntry(Stream input)
        {
            GzsEntry gzsEntry = new GzsEntry();
            gzsEntry.Read(input);
            return gzsEntry;
        }

        public void Read(Stream input)
        {
            BinaryReader reader = new BinaryReader(input, Encoding.Default, true);
            Hash = reader.ReadUInt64();
            Offset = reader.ReadUInt32();
            Size = reader.ReadUInt32();
            string filePath;
            FileNameFound = TryGetFilePath(out filePath);
            FilePath = filePath;
        }

        private byte[] ReadData(Stream input)
        {
            BinaryReader reader = new BinaryReader(input, Encoding.Default, true);
            uint dataOffset = 16*Offset;
            input.Seek(dataOffset, SeekOrigin.Begin);
            byte[] data = reader.ReadBytes((int) Size);
            data = Encryption.DeEncryptQar(data, Offset);
            const uint keyConstant = 0xA0F8EFE6;
            uint peekData = BitConverter.ToUInt32(data, 0);
            if (peekData == keyConstant)
            {
                uint key = BitConverter.ToUInt32(data, 4);
                Size -= 8;
                byte[] data2 = new byte[data.Length - 8];
                Array.Copy(data, 8, data2, 0, data.Length - 8);
                data = Encryption.DeEncrypt(data2, key);
            }
            return data;
        }

        private string GetRelativeFilePath()
        {
            string filePath = FilePath;
            if (filePath.StartsWith("/"))
                filePath = filePath.Substring(1, filePath.Length - 1);
            filePath = filePath.Replace("/", "\\");
            return filePath;
        }

        private string GetAbsoluteFilePath(string directory)
        {
            string filePath = GetRelativeFilePath();
            return Path.Combine(directory, filePath);
        }

        private bool TryGetFilePath(out string filePath)
        {
            int fileExtensionId = (int) (Hash >> 52 & 0xFFFF);
            bool fileNameFound = Hashing.TryGetFileNameFromHash(Hash, fileExtensionId, out filePath);
            return fileNameFound;
        }

        public void WriteData(Stream output, string inputDirectory)
        {
            Offset = (uint) output.Position/16;

            string inputFilePath = GetAbsoluteFilePath(inputDirectory);
            byte[] data;
            using (FileStream input = new FileStream(inputFilePath, FileMode.Open))
            {
                data = new byte[input.Length];
                input.Read(data, 0, data.Length);
            }
            data = Encryption.DeEncryptQar(data, Offset);
            // TODO: Encrypt the data if a key is set for the entry.

            Size = (uint) data.Length;
            output.Write(data, 0, data.Length);
        }

        public void Write(Stream output)
        {
            BinaryWriter writer = new BinaryWriter(output, Encoding.Default, true);
            writer.Write(Hash);
            writer.Write(Offset);
            writer.Write(Size);
        }

        public void CalculateHash()
        {
            if (Hash == 0)
                Hash = Hashing.HashFileNameWithExtension(FilePath); //
        }

        public FileDataContainer Export(Stream input)
        {
            FileDataContainer container = new FileDataContainer
            {
                Data = ReadData(input),
                FileName = GetRelativeFilePath()
            };
            return container;
        }
    }
}
