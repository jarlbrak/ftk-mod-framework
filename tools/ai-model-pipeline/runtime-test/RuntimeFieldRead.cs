using System;
using System.Reflection;

// Test-helper observation only. Never invokes properties or game query methods.
internal static class RuntimeFieldRead
{
    internal static object Read(object target, string name)
    {
        if (target == null) throw new ArgumentNullException("target");
        Type staticType = target as Type;
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
            (staticType == null ? BindingFlags.Instance : BindingFlags.Static);
        FieldInfo field = (staticType ?? target.GetType()).GetField(name, flags);
        if (field == null) throw new MissingFieldException((staticType ?? target.GetType()).FullName, name);
        return field.GetValue(staticType == null ? target : null);
    }
}
