using System;

namespace FTKModFramework.Core
{
    // Container admission before Unity's decoder. Unity 2017 can report success for an
    // IHDR-only file. This validates critical PNG structure/CRCs, not compressed pixels.
    // PNG critical chunk contract: https://www.w3.org/TR/png/#5ChunkOrdering
    internal static class PngStructure
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly uint[] CrcTable = BuildCrcTable();

        internal static void Validate(byte[] bytes, int maxBytes, int maxDimension, out int width, out int height)
        {
            width = height = 0;
            if (bytes == null || bytes.Length < 45 || bytes.Length > maxBytes)
                throw new FormatException("PNG is truncated or exceeds its byte limit.");
            for (int i = 0; i < Signature.Length; i++)
                if (bytes[i] != Signature[i]) throw new FormatException("Invalid PNG signature.");
            bool header = false, palette = false, image = false, imageEnded = false;
            long imageBytes = 0;
            int color = -1, depth = -1;
            int offset = 8;
            while (offset < bytes.Length)
            {
                if (bytes.Length - offset < 12) throw new FormatException("Truncated PNG chunk.");
                uint rawLength = Read(bytes, offset);
                if (rawLength > (uint)(bytes.Length - offset - 12)) throw new FormatException("PNG chunk exceeds its container.");
                int length = (int)rawLength;
                int start = offset + 8;
                for (int i = offset + 4; i < start; i++)
                    if (!Letter(bytes[i])) throw new FormatException("Invalid PNG chunk type.");
                if ((bytes[offset + 6] & 32) != 0) throw new FormatException("Reserved PNG chunk bit is set.");
                uint crc = uint.MaxValue;
                for (int i = offset + 4; i < start + length; i++) crc = CrcTable[(crc ^ bytes[i]) & 255] ^ (crc >> 8);
                if ((crc ^ uint.MaxValue) != Read(bytes, start + length)) throw new FormatException("PNG chunk CRC mismatch.");
                uint type = Read(bytes, offset + 4);
                if (!header && type != 0x49484452) throw new FormatException("PNG must begin with IHDR.");
                if (type == 0x49484452) // IHDR
                {
                    if (header || length != 13) throw new FormatException("Invalid or repeated PNG header.");
                    uint w = Read(bytes, start), h = Read(bytes, start + 4);
                    if (w == 0 || h == 0 || w > maxDimension || h > maxDimension)
                        throw new FormatException("PNG dimensions exceed their limit.");
                    width = (int)w; height = (int)h; depth = bytes[start + 8]; color = bytes[start + 9];
                    bool allowedDepth = color == 0 ? depth == 1 || depth == 2 || depth == 4 || depth == 8 || depth == 16 :
                        color == 3 ? depth == 1 || depth == 2 || depth == 4 || depth == 8 :
                        (color == 2 || color == 4 || color == 6) && (depth == 8 || depth == 16);
                    if (!allowedDepth || bytes[start + 10] != 0 || bytes[start + 11] != 0 || bytes[start + 12] > 1)
                        throw new FormatException("Unsupported PNG header encoding.");
                    header = true;
                }
                else if (type == 0x504c5445) // PLTE
                {
                    if (palette || image || color == 0 || color == 4 || length == 0 || length > 768 || length % 3 != 0 ||
                        (color == 3 && length / 3 > (1 << depth))) throw new FormatException("Invalid PNG palette.");
                    palette = true;
                }
                else if (type == 0x49444154) // IDAT
                {
                    if (imageEnded || (color == 3 && !palette)) throw new FormatException("Invalid PNG image-data order.");
                    image = true; imageBytes += length;
                }
                else if (type == 0x49454e44) // IEND
                {
                    if (!image || imageBytes == 0 || length != 0 || start + length + 4 != bytes.Length)
                        throw new FormatException("PNG lacks image data or a terminal IEND.");
                    return;
                }
                else
                {
                    if ((bytes[offset + 4] & 32) == 0) throw new FormatException("Unknown critical PNG chunk.");
                    if (image) imageEnded = true;
                }
                offset = start + length + 4;
            }
            throw new FormatException("PNG is missing IEND.");
        }

        private static bool Letter(byte value) { return value >= 65 && value <= 90 || value >= 97 && value <= 122; }
        private static uint Read(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) |
                ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        }
        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xedb88320U ^ (value >> 1) : value >> 1;
                table[i] = value;
            }
            return table;
        }
    }
}
