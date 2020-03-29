using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CapFrameX.Webservice.Data.Extensions
{
	public static class ByteArrayExtensions
	{
        public static byte[] Compress(this byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
                {
                    gzip.Write(raw, 0, raw.Length);
                }
                return memory.ToArray();
            }
        }

        public static byte[] Decompress(this byte[] compressed)
        {
            using (MemoryStream memory = new MemoryStream(compressed))
            {
                byte[] raw = new byte[] { };
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Decompress, true))
                {
                    using (var rawStream = new MemoryStream())
                    {
                        gzip.CopyTo(rawStream);
                        return rawStream.ToArray();
                    }
                }
            }
        }
    }
}
