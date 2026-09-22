using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FTKModFramework.Core;

internal static class Program
{
    static void Reject(byte[] bytes, string label, int dimension = 4096, int maximum = 16 * 1024 * 1024)
    {
        bool rejected = false;
        try { int w, h; PngStructure.Validate(bytes, maximum, dimension, out w, out h); }
        catch (FormatException) { rejected = true; }
        if (!rejected) throw new Exception("Accepted " + label);
        Console.WriteLine("PASS: rejects " + label);
    }
    static byte[] Join(params byte[][] chunks)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            foreach (byte[] chunk in chunks) stream.Write(chunk, 0, chunk.Length);
            return stream.ToArray();
        }
    }
    static byte[] Slice(byte[] bytes, int start, int count)
    {
        byte[] result = new byte[count]; Array.Copy(bytes, start, result, 0, count); return result;
    }
    static void Write(byte[] bytes, int offset, uint value)
    {
        bytes[offset]=(byte)(value>>24); bytes[offset+1]=(byte)(value>>16); bytes[offset+2]=(byte)(value>>8); bytes[offset+3]=(byte)value;
    }
    static byte[] Chunk(string type, byte[] value)
    {
        byte[] bytes = new byte[value.Length+12]; Write(bytes,0,(uint)value.Length);
        Array.Copy(Encoding.ASCII.GetBytes(type),0,bytes,4,4); Array.Copy(value,0,bytes,8,value.Length);
        // Independent bitwise reference CRC, unlike the production table implementation.
        uint crc=0xffffffff;
        for(int i=4;i<bytes.Length-4;i++) { crc^=bytes[i]; for(int b=0;b<8;b++) crc=(crc&1)!=0 ? (crc>>1)^0xedb88320 : crc>>1; }
        Write(bytes,bytes.Length-4,crc^0xffffffff); return bytes;
    }
    static void Main(string[] args)
    {
        string root=args.Length==0 ? "marketplace/packages/paladin/assets" : args[0];
        string[] files=Directory.GetFiles(root,"*.png"); if(files.Length==0)throw new Exception("No real package PNG fixtures");
        foreach(string path in files) { int w,h; PngStructure.Validate(File.ReadAllBytes(path),16*1024*1024,4096,out w,out h); if(w<=0||h<=0)throw new Exception("Lost dimensions"); }
        Console.WriteLine("PASS: "+files.Length+" actual Paladin PNGs including RGB/RGBA and ancillary chunks");
        byte[] original=File.ReadAllBytes(Path.Combine(root,"paladin-hammer-1h-novice-icon.png"));
        byte[] signature=Slice(original,0,8), header=Slice(original,8,25), end=Chunk("IEND",new byte[0]);
        byte[] image=Chunk("IDAT",new byte[]{120,156,1});
        Reject(Slice(original,0,33),"exact live 33-byte IHDR-only fixture");
        Reject(Slice(original,0,original.Length-1),"truncated IEND CRC");
        Reject(Slice(original,0,original.Length-12),"missing IEND");
        byte[] corrupt=(byte[])original.Clone();corrupt[32]^=1;Reject(corrupt,"CRC corruption");
        corrupt=(byte[])original.Clone();corrupt[7]^=1;Reject(corrupt,"full-signature corruption");
        Reject(Join(signature,header,end),"missing IDAT");
        Reject(Join(signature,header,Chunk("IDAT",new byte[0]),end),"only empty IDAT");
        Reject(Join(signature,image,header,end),"IDAT before IHDR");
        Reject(Join(signature,header,header,image,end),"repeated IHDR");
        Reject(Join(signature,header,image,Chunk("tEXt",new byte[]{65,0,66}),image,end),"nonconsecutive IDAT");
        Reject(Join(signature,header,Chunk("ABCD",new byte[0]),image,end),"unknown critical chunk");
        Reject(Join(signature,header,Chunk("aaab",new byte[0]),image,end),"reserved type bit");
        Reject(Join(original,new byte[]{0}),"trailing bytes after IEND");
        corrupt=(byte[])original.Clone();Write(corrupt,8,0xffffffff);Reject(corrupt,"oversized chunk without overflow");
        Reject(original,"dimension limit",128);Reject(original,"byte limit",4096,32);
        byte[] indexed=Slice(header,8,13);indexed[9]=3;
        Reject(Join(signature,Chunk("IHDR",indexed),image,end),"indexed PNG without palette");
        Reject(Join(signature,header,image,Chunk("PLTE",new byte[]{0,0,0}),end),"palette after image data");
        // Adjacent split IDAT is normal in large source assets; zero-length chunks are
        // permitted provided the total stream is nonempty. This tests container admission only.
        int width,height;
        PngStructure.Validate(Join(signature,header,Chunk("IDAT",new byte[0]),image,end),4096,4096,out width,out height);
        Console.WriteLine("PASS: consecutive IDAT chunks with a nonempty aggregate admitted");
    }
}
