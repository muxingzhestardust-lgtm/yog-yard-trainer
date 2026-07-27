using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

class SigProvider : ISignatureTypeProvider<string, object>
{
    public string GetArrayType(string elementType, ArrayShape shape) { return elementType + "[]"; }
    public string GetByReferenceType(string elementType) { return elementType + "&"; }
    public string GetFunctionPointerType(MethodSignature<string> signature) { return "fnptr"; }
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) { return genericType + "<" + string.Join(",", typeArguments) + ">"; }
    public string GetGenericMethodParameter(object genericContext, int index) { return "!!" + index; }
    public string GetGenericTypeParameter(object genericContext, int index) { return "!" + index; }
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) { return unmodifiedType; }
    public string GetPinnedType(string elementType) { return elementType; }
    public string GetPointerType(string elementType) { return elementType + "*"; }
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) { return typeCode.ToString(); }
    public string GetSZArrayType(string elementType) { return elementType + "[]"; }
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        TypeDefinition td = reader.GetTypeDefinition(handle);
        string ns = reader.GetString(td.Namespace);
        string n = reader.GetString(td.Name);
        return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        TypeReference tr = reader.GetTypeReference(handle);
        string ns = reader.GetString(tr.Namespace);
        string n = reader.GetString(tr.Name);
        return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
    }
    public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}

class Program
{
    static void Main(string[] args)
    {
        string path = args[0];
        string outPath = args[1];
        using (FileStream fs = File.OpenRead(path))
        using (PEReader pe = new PEReader(fs))
        using (StreamWriter sw = new StreamWriter(outPath, false, new UTF8Encoding(false), 1 << 20))
        {
            MetadataReader md = pe.GetMetadataReader();
            SigProvider prov = new SigProvider();
            foreach (TypeDefinitionHandle tdh in md.TypeDefinitions)
            {
                TypeDefinition td = md.GetTypeDefinition(tdh);
                string ns = md.GetString(td.Namespace);
                string name = md.GetString(td.Name);
                string full;
                TypeDefinitionHandle decl = td.GetDeclaringType();
                if (!decl.IsNil)
                {
                    TypeDefinition d = md.GetTypeDefinition(decl);
                    string dns = md.GetString(d.Namespace);
                    string dn = md.GetString(d.Name);
                    full = (string.IsNullOrEmpty(dns) ? dn : dns + "." + dn) + "/" + name;
                }
                else
                {
                    full = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                }
                string baseName = "";
                try
                {
                    if (!td.BaseType.IsNil)
                    {
                        if (td.BaseType.Kind == HandleKind.TypeReference)
                        {
                            TypeReference tr = md.GetTypeReference((TypeReferenceHandle)td.BaseType);
                            baseName = md.GetString(tr.Name);
                        }
                        else if (td.BaseType.Kind == HandleKind.TypeDefinition)
                        {
                            TypeDefinition b = md.GetTypeDefinition((TypeDefinitionHandle)td.BaseType);
                            baseName = md.GetString(b.Name);
                        }
                    }
                }
                catch { }
                sw.Write("T ");
                sw.Write(full);
                sw.Write(" : ");
                sw.WriteLine(baseName);
                foreach (FieldDefinitionHandle fh in td.GetFields())
                {
                    FieldDefinition f = md.GetFieldDefinition(fh);
                    string ft = "?";
                    try { ft = f.DecodeSignature(prov, null); } catch { }
                    sw.Write("F ");
                    sw.Write(full);
                    sw.Write(" :: ");
                    sw.Write(ft);
                    sw.Write(' ');
                    sw.WriteLine(md.GetString(f.Name));
                }
                foreach (PropertyDefinitionHandle ph in td.GetProperties())
                {
                    PropertyDefinition p = md.GetPropertyDefinition(ph);
                    string pt = "?";
                    try
                    {
                        MethodSignature<string> psig = p.DecodeSignature(prov, null);
                        pt = psig.ReturnType;
                    }
                    catch { }
                    sw.Write("P ");
                    sw.Write(full);
                    sw.Write(" :: ");
                    sw.Write(pt);
                    sw.Write(' ');
                    sw.WriteLine(md.GetString(p.Name));
                }
                foreach (MethodDefinitionHandle mh in td.GetMethods())
                {
                    MethodDefinition m = md.GetMethodDefinition(mh);
                    string ret = "?";
                    string pars = "?";
                    try
                    {
                        MethodSignature<string> sig = m.DecodeSignature(prov, null);
                        ret = sig.ReturnType;
                        pars = string.Join(",", sig.ParameterTypes);
                    }
                    catch { }
                    bool isStatic = (m.Attributes & MethodAttributes.Static) != 0;
                    sw.Write("M ");
                    sw.Write(full);
                    sw.Write(" :: ");
                    if (isStatic) sw.Write("static ");
                    sw.Write(ret);
                    sw.Write(' ');
                    sw.Write(md.GetString(m.Name));
                    sw.Write('(');
                    sw.Write(pars);
                    sw.WriteLine(")");
                }
            }
        }
        Console.WriteLine("done");
    }
}
