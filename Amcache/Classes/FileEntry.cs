﻿using System;
using System.IO;

namespace Amcache.Classes
{
    public class FileEntry
    {
        public FileEntry(string productName, string programID, string sha1, string fullPath, DateTimeOffset? lastMod2,
            string volumeID, DateTimeOffset volumeLastWrite, string fileID, DateTimeOffset lastWrite, int unknown5,
            string compName, int? langId,
            string fileVerString, string peHash, string fileVerNum, string fileDesc, long unknown1, long unknown2,
            int unknown3, int unknown4, string switchback, int? fileSize, DateTimeOffset? compTime, int? peHeaderSize,
            DateTimeOffset? lm, DateTimeOffset? created, int? pecheck, int unknown6, string keyName)
        {
            PEHeaderChecksum = pecheck;
            LastModified = lm;
            Created = created;
            PEHeaderSize = peHeaderSize;
            CompileTime = compTime;
            SwitchBackContext = switchback;
            FileSize = fileSize;
            FileDescription = fileDesc;
            ProductName = productName;
            ProgramID = programID;

            SHA1 = string.Empty;
            if (sha1.Length > 4)
            {
                SHA1 = sha1.Substring(4).ToLowerInvariant();
            }

            FullPath = fullPath;

            FileExtension = Path.GetExtension(fullPath);

            LastModified2 = lastMod2;
            FileID = fileID;
            FileIDLastWriteTimestamp = lastWrite;
            VolumeID = volumeID;
            VolumeIDLastWriteTimestamp = volumeLastWrite;
            Unknown1 = unknown1;
            Unknown2 = unknown2;
            Unknown3 = unknown3;
            Unknown4 = unknown4;
            Unknown5 = unknown5;
            Unknown6 = unknown6;
            CompanyName = compName;
            LanguageID = langId;
            FileVersionString = fileVerString;
            FileVersionNumber = fileVerNum;
            PEHeaderHash = peHash;

            var tempKey = keyName.PadLeft(8, '0');

            var seq1 = tempKey.Substring(0, 4);
            var seq2 = tempKey.Substring(2, 2);
            var seq = seq1.TrimEnd('0');

            if (seq.Length == 0)
            {
                seq = "0";
            }


            MFTSequenceNumber = Convert.ToInt32(seq, 16);
            var ent = tempKey.Substring(4);
            MFTEntryNumber = Convert.ToInt32(ent, 16);
        }

        public int MFTEntryNumber { get; }
        public int MFTSequenceNumber { get; }
        public string ProductName { get; }
        public string CompanyName { get; }
        public string FileVersionString { get; }
        public string FileVersionNumber { get; }
        public string FileDescription { get; }
        public string FullPath { get; }
        public string FileExtension { get; }
        public string PEHeaderHash { get; }
        public string ProgramID { get; }
        public string SHA1 { get; }
        public string FileID { get; }
        public string VolumeID { get; }
        public string SwitchBackContext { get; }
        public string ProgramName { get; set; }
        public long Unknown1 { get; }
        public long Unknown2 { get; }
        public int Unknown3 { get; }
        public int Unknown4 { get; }
        public int Unknown5 { get; }
        public int Unknown6 { get; }
        public int? LanguageID { get; }
        public int? FileSize { get; }
        public int? PEHeaderSize { get; }
        public int? PEHeaderChecksum { get; }
        public DateTimeOffset VolumeIDLastWriteTimestamp { get; }
        public DateTimeOffset FileIDLastWriteTimestamp { get; }
        public DateTimeOffset? CompileTime { get; }
        public DateTimeOffset? LastModified { get; }
        public DateTimeOffset? LastModified2 { get; }
        public DateTimeOffset? Created { get; }

        public static string Reverse(string s)
        {
            var charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
    }
}