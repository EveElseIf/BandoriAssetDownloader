using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Downloader
{
    internal class AssetsBundleInfo
    {
        public string Version { get; set; } = string.Empty;
        public string VersionCode { get; set; } = string.Empty;
        public IEnumerable<Bundle> Bundles { get; set; } = Enumerable.Empty<Bundle>();
    }
    internal class Bundle
    {
        public string Name { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public BundleCategory Category { get; set; }
        public uint Size { get; set; }
        public uint Preserved { get; set; }
    }
    internal enum BundleCategory
    {
        StartApp,
        AsNeeded
    }
    internal class AssetsBundleInfoParser
    {
        // https://github.com/rzubek/mini-leb128/blob/master/LEB128.cs
        public static ulong ReadLEB128Unsigned(byte[] bytes, out int read)
        {
            read = 0;

            ulong value = 0;
            int shift = 0;
            bool more = true;

            while (more)
            {
                var next = bytes[read];
                if (next < 0) { throw new InvalidOperationException("Unexpected end of stream"); }

                byte b = (byte)next;
                read += 1;

                more = (b & 0x80) != 0;   // extract msb
                ulong chunk = b & 0x7fUL; // extract lower 7 bits
                value |= chunk << shift;
                shift += 7;
            }

            return value;
        }
        public static ulong ReadLEB128Unsigned(Stream stream, out int read)
        {
            read = 0;

            ulong value = 0;
            int shift = 0;
            bool more = true;

            while (more)
            {
                var next = stream.ReadByte();
                if (next < 0) { throw new InvalidOperationException("Unexpected end of stream"); }

                byte b = (byte)next;
                read += 1;

                more = (b & 0x80) != 0;   // extract msb
                ulong chunk = b & 0x7fUL; // extract lower 7 bits
                value |= chunk << shift;
                shift += 7;
            }

            return value;
        }

        public static async Task<AssetsBundleInfo> ParseAsync(Stream s,string? versionCode)
        {
            var ms = new MemoryStream();
            await s.CopyToAsync(ms);

            ms.Seek(0, SeekOrigin.Begin);
            var temp = new byte[512];

            ms.Seek(2, SeekOrigin.Current); // 0x0A 0x09
            ms.Read(temp, 0, 9); // Version string
            var version = Encoding.ASCII.GetString(temp, 0, 9);
            ms.Seek(1, SeekOrigin.Current); // 0x12

            var bundles = new List<Bundle>();
            while (ms.Position < ms.Length)
            {
                var chunkSize = ReadLEB128Unsigned(ms, out int read);
                ms.Seek(1, SeekOrigin.Current); // 1(0x0A)
                ms.Read(temp, 0, (int)chunkSize);

                var i = 0;
                var l = temp[i]; // name length
                i++;
                var name = Encoding.ASCII.GetString(temp, i, l);
                i += l + 1; // Name + 1(0x12)
                _ = ReadLEB128Unsigned(temp[i..], out read);
                i += read; // LEB
                i++; // 0x10
                l = temp[i]; // name length
                i++;
                i += l + 1; // name length + 1(0x12), the same name so don't need to read
                l = temp[i]; // hash length, always is 64
                i++;
                var hash = Encoding.ASCII.GetString(temp, i, l);
                i += l + 1; // hash length + 1(0x1A)
                l = temp[i]; // version length, useless
                i++;
                i += l + 1; // category version + 1(0x22)
                l = temp[i]; // category length
                i++;
                var categoryString = Encoding.ASCII.GetString(temp, i, l);
                i += l + 1; // category length + 1(0x28)
                var preserved = ReadLEB128Unsigned(temp[i..], out read); // preserved
                i += read + 1; // preserved length + 1(0x38)
                var size = ReadLEB128Unsigned(temp[i..], out read); // file size
                var bundle = new Bundle()
                {
                    Name = name,
                    Hash = hash,
                    Size = (uint)size,
                    Preserved = (uint)preserved,
                    Category = categoryString == "StartApp" ? BundleCategory.StartApp : BundleCategory.AsNeeded
                };
                bundles.Add(bundle);
            }

            return new()
            {
                Version = version,
                VersionCode = versionCode ?? "",
                Bundles = bundles
            };
        }
    }
}
