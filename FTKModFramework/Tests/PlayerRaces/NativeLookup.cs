using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static class NativeLookup
{
    private sealed class Instruction { internal OpCode Op; internal int Token,Offset; }
    internal static void Verify(string path)
    {
        using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
        var type=md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t=>md.GetString(t.Name)=="uiQuickPlayerCreate");
        var method=type.GetMethods().Select(md.GetMethodDefinition).Single(m=>md.GetString(m.Name)=="SetClass");
        var bytes=pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
        var ops=typeof(OpCodes).GetFields(BindingFlags.Static|BindingFlags.Public).Where(f=>f.FieldType==typeof(OpCode))
            .Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>unchecked((ushort)o.Value));
        var code=new List<Instruction>();
        for(int pos=0;pos<bytes.Length;)
        {
            int offset=pos;ushort key=bytes[pos++];if(key==0xfe)key=(ushort)(0xfe00|bytes[pos++]);OpCode op=ops[key];
            int size=Size(op.OperandType,bytes,pos);int token=size==4?BitConverter.ToInt32(bytes,pos):0;
            code.Add(new Instruction{Op=op,Token=token,Offset=offset});pos+=size;
        }
        int matches=0;
        for(int i=0;i+3<code.Count;i++)
        {
            if(code[i].Op!=OpCodes.Ldfld || Field(md,code[i].Token)!="GridEditor.FTK_playerGameStart.m_Skinsets") continue;
            if(code[i+1].Op!=OpCodes.Ldarg_0 || code[i+2].Op!=OpCodes.Ldfld ||
                Field(md,code[i+2].Token)!="uiQuickPlayerCreate.m_SkinType" || code[i+3].Op!=OpCodes.Ldelem_I4)
                throw new Exception("Native SetClass skinset lookup pattern changed");
            matches++;
        }
        if(matches!=1)throw new Exception("Expected unique installed race lookup, got "+matches);
        Console.WriteLine("PASS installed SetClass IL lookup contract");
    }
    static int Size(OperandType type,byte[] bytes,int pos)
    {
        switch(type)
        {
            case OperandType.InlineNone:return 0;
            case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:return 1;
            case OperandType.InlineVar:return 2;
            case OperandType.InlineI8:case OperandType.InlineR:return 8;
            case OperandType.InlineSwitch:return 4+4*BitConverter.ToInt32(bytes,pos);
            default:return 4;
        }
    }
    static string TypeName(MetadataReader md,EntityHandle handle)
    {
        if(handle.Kind==HandleKind.TypeDefinition){var t=md.GetTypeDefinition((TypeDefinitionHandle)handle);return (md.GetString(t.Namespace)+"."+md.GetString(t.Name)).TrimStart('.');}
        if(handle.Kind==HandleKind.TypeReference){var t=md.GetTypeReference((TypeReferenceHandle)handle);return (md.GetString(t.Namespace)+"."+md.GetString(t.Name)).TrimStart('.');}
        return "unsupported";
    }
    static string Field(MetadataReader md,int token)
    {
        var h=MetadataTokens.EntityHandle(token);if(h.Kind!=HandleKind.FieldDefinition)return "unsupported";
        var f=md.GetFieldDefinition((FieldDefinitionHandle)h);return TypeName(md,f.GetDeclaringType())+"."+md.GetString(f.Name);
    }
    static string Method(MetadataReader md,int token)
    {
        var h=MetadataTokens.EntityHandle(token);
        if(h.Kind==HandleKind.MethodSpecification)h=md.GetMethodSpecification((MethodSpecificationHandle)h).Method;
        if(h.Kind==HandleKind.MethodDefinition){var m=md.GetMethodDefinition((MethodDefinitionHandle)h);return TypeName(md,m.GetDeclaringType())+"."+md.GetString(m.Name);}
        if(h.Kind==HandleKind.MemberReference){var m=md.GetMemberReference((MemberReferenceHandle)h);return TypeName(md,m.Parent)+"."+md.GetString(m.Name);}
        return "unsupported";
    }
}
