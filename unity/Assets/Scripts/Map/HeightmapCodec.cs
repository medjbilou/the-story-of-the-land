#nullable enable
using System;
using System.IO;
using System.IO.Compression;

namespace StoryOfTheLand.Game.Map
{
    /// <summary>The packed heightmap, heightmap.r16z (issue #44): lossless, about half the size of the raw 16-bit grid, so the
    /// web build downloads 4 MB instead of 8 (StreamingAssets files are served outside Unity's build compression).
    /// Layout (tools/heightmap_codec.py writes it): one zlib stream (RFC 1950: a 2-byte header, raw deflate, the Adler-32 of
    /// the contents) of the grid's row deltas split into byte planes. Each row (row 0 = north) holds d[x] = h[x] - h[x-1]
    /// mod 65536 (d[0] = h[0]); the low bytes of all deltas come first, then all the high bytes. Decoding inflates with
    /// System.IO.Compression (IL2CPP backs DeflateStream with its bundled zlib on every target, WebGL included), checks the
    /// size and the Adler-32, then interleaves and sums each row: the original grid bit for bit.</summary>
    public static class HeightmapCodec
    {
        public const string FileName = "heightmap.r16z";

        /// <summary>The grid (row 0 = north, row-major) of a packed <paramref name="width"/> x <paramref name="height"/>
        /// heightmap. InvalidDataException (naming the file) on a stream that is not one, is corrupt or has the wrong size.</summary>
        public static ushort[] Decode(byte[] packed, int width, int height)
        {
            if (packed == null) throw new ArgumentNullException(nameof(packed));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), $"{width} x {height}");
            if (packed.Length < 6) throw new InvalidDataException($"{FileName}: {packed.Length} bytes is too short for a zlib stream");
            int cmf = packed[0], flg = packed[1];
            if ((cmf & 0x0F) != 8 || (cmf >> 4) > 7 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0)
                throw new InvalidDataException($"{FileName}: not a zlib stream (header {cmf:x2} {flg:x2})");
            int n = checked(width * height);
            var planes = new byte[checked(2 * n)];
            int got = 0;
            bool extra;
            try
            {
                using var body = new MemoryStream(packed, 2, packed.Length - 6, false);
                using var inflate = new DeflateStream(body, CompressionMode.Decompress);
                while (got < planes.Length)
                {
                    int r = inflate.Read(planes, got, planes.Length - got);
                    if (r == 0) break;
                    got += r;
                }
                extra = got == planes.Length && inflate.Read(new byte[1], 0, 1) != 0;
            }
            // .NET reports bad deflate data as InvalidDataException, Mono's native zlib (the editor, IL2CPP players) as IOException.
            catch (Exception e) when (e is InvalidDataException || e is IOException) { throw new InvalidDataException($"{FileName}: corrupt stream ({e.Message})", e); }
            if (got != planes.Length || extra)
                throw new InvalidDataException($"{FileName}: {(extra ? "more than " + planes.Length : got.ToString())} bytes after inflating, expected {planes.Length}");
            uint stored = (uint)(packed[packed.Length - 4] << 24 | packed[packed.Length - 3] << 16 | packed[packed.Length - 2] << 8 | packed[packed.Length - 1]);
            if (Adler32(planes) != stored) throw new InvalidDataException($"{FileName}: checksum mismatch (corrupt download?)");
            var raw = new ushort[n];
            for (int row = 0, i = 0; row < height; row++)
            {
                ushort h = 0;
                for (int x = 0; x < width; x++, i++) { h = (ushort)(h + (planes[i] | planes[n + i] << 8)); raw[i] = h; }
            }
            return raw;
        }

        /// <summary>Packs a row-major grid (the inverse of Decode; the editor and tests use it, the game only decodes).</summary>
        public static byte[] Encode(ushort[] raw, int width, int height)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            int n = checked(width * height);
            if (raw.Length != n) throw new ArgumentException($"{raw.Length} samples for {width} x {height}", nameof(raw));
            var planes = new byte[2 * n];
            for (int row = 0, i = 0; row < height; row++)
                for (int x = 0; x < width; x++, i++)
                {
                    var d = (ushort)(raw[i] - (x == 0 ? 0 : raw[i - 1]));
                    planes[i] = (byte)d; planes[n + i] = (byte)(d >> 8);
                }
            using var output = new MemoryStream();
            output.WriteByte(0x78); output.WriteByte(0xDA);   // deflate, 32 KB window, best compression: (0x78DA % 31 == 0)
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true)) deflate.Write(planes, 0, planes.Length);
            var a = Adler32(planes);
            output.WriteByte((byte)(a >> 24)); output.WriteByte((byte)(a >> 16)); output.WriteByte((byte)(a >> 8)); output.WriteByte((byte)a);
            return output.ToArray();
        }

        /// <summary>RFC 1950 Adler-32.</summary>
        public static uint Adler32(byte[] data)
        {
            const uint Mod = 65521; const int Nmax = 5552;   // the largest run whose sums cannot overflow 32 bits
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length;)
            {
                int end = Math.Min(data.Length, i + Nmax);
                for (; i < end; i++) { a += data[i]; b += a; }
                a %= Mod; b %= Mod;
            }
            return b << 16 | a;
        }
    }
}
