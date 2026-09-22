using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static class NativeAssignment
{
    private sealed class Instruction { internal OpCode Op; internal int Token,Offset; }
    internal static void Verify(string path)
    {
        using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
        var type=md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t=>md.GetString(t.Name)=="CharacterDummy");
        var method=type.GetMethods().Select(md.GetMethodDefinition).Single(m=>md.GetString(m.Name)=="CreateAvatar");
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
        for(int i=5;i+4<code.Count;i++)
        {
            if(code[i].Op!=OpCodes.Stfld || Field(md,code[i].Token)!="CharacterDummy.m_EventListener")continue;
            if(code[i-5].Op!=OpCodes.Ldarg_0 || code[i-4].Op!=OpCodes.Ldarg_0 || code[i-3].Op!=OpCodes.Ldfld ||
                Field(md,code[i-3].Token)!="CharacterDummy.m_CharacterOverworld" || code[i-2].Op!=OpCodes.Ldfld ||
                Field(md,code[i-2].Token)!="CharacterOverworld.m_Avatar" || code[i-1].Op!=OpCodes.Call ||
                Method(md,code[i-1].Token)!="UnityEngine.Object.Instantiate")throw new Exception("Native clone assignment pattern changed");
            if(code[i+1].Op!=OpCodes.Ldarg_0 || code[i+2].Op!=OpCodes.Ldfld || Field(md,code[i+2].Token)!="CharacterDummy.m_EventListener" ||
                code[i+3].Op!=OpCodes.Ldc_I4_1 || code[i+4].Op!=OpCodes.Callvirt || Method(md,code[i+4].Token)!="CharacterEventListener.SetVisible")
                throw new Exception("Native post-assignment SetVisible boundary changed");
            matches++;
            Console.WriteLine("Native clone assignment IL_"+code[i].Offset.ToString("x4")+" precedes SetVisible IL_"+code[i+4].Offset.ToString("x4"));
        }
        if(matches!=1)throw new Exception("Expected unique installed clone assignment, got "+matches);
        Console.WriteLine("PASS installed native IL clone/assignment/SetVisible contract");
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
