﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Xml;

namespace LicenseConsumerProofOfConcept
{
    public static class LicenseFileVerifier
    {
        private const byte fileVersion = 0;
        /* File version 0:
         * [0..0] File version
         * [1..4] signature length (sl)
         * [5..sl] signature
         * [sl+1..end] zipped license
         */

        private const string PUBLIC_KEY = @"
<RSAKeyValue>
  <Modulus>sJKuRsD1Uk6c4rtzOGfuhel1sBGY1J0HxEAWROa21c7yy8zPJxvn6mySsCUYamhBEailK4zyz9He/A48F1GV8R2jR7SlG6ppW/O9ZTUeGL74DQTI8EggY+PfTa9xFSH2Bk5UsgqdsNRk1cOGv67WlJoPL9Vn4JkBFJ6gcHAsfds=</Modulus>
  <Exponent>AQAB</Exponent>
</RSAKeyValue>";


        public static bool TryOpenLicenseFile(FileInfo licenseFile, out XDocument xDoc)
        {
            xDoc = null;

            try
            {
                using (var fileStream = licenseFile.OpenRead())
                {
                    var fileFileVersion = fileStream.ReadByte();

                    var rawSignatureLength = new byte[4];
                    fileStream.Read(rawSignatureLength, 0, 4);
                    var signatureLength = BitConverter.ToInt32(rawSignatureLength, 0);

                    var signature = new byte[signatureLength];
                    fileStream.Read(signature, 0, signatureLength);

                    var streamedXDoc = DecompressZipStream(fileStream);
                    var xmlString = UTF8Encoding.UTF8.GetString(streamedXDoc);
                    xDoc = XDocument.Load(new MemoryStream(streamedXDoc));

                    return fileFileVersion == fileVersion
                           && VerifySignature(streamedXDoc, signature);
                }
            }
            catch
            {
                return false;
            }
        }

        private static byte[] DecompressZipStream(Stream zipStream)
        {
            using (var decompressedStream = new MemoryStream())
            {
                using (var decompressionStream = new DeflateStream(zipStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(decompressedStream);
                }
                return decompressedStream.ToArray();
            }
        }

        private static bool VerifySignature(byte[] dataToVerify, byte[] signature)
        {
            using (var rsa = new RSACryptoServiceProvider())
            using (var sha = SHA256.Create())
            {
                return rsa.VerifyHash(sha.ComputeHash(dataToVerify), "SHA256", signature);
            }
        }

        private static byte[] GetStreamedXdoc(XDocument xDoc)
        {
            var memoryStream = new MemoryStream();
            using (var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
            {
                xDoc.WriteTo(xmlWriter);
            }
            memoryStream.Position = 0;
            return memoryStream.ToArray();
        }
    }
}
