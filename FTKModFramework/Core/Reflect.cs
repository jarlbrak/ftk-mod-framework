using System;
using System.Reflection;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Small reflection helpers. The framework deliberately drives the game's GridEditor
    /// data tables via reflection so it stays robust to per-DB type differences and to
    /// fields we have not explicitly mapped yet.
    /// </summary>
    internal static class Reflect
    {
        public const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        public static FieldInfo Field(Type t, string name)
        {
            for (Type cur = t; cur != null; cur = cur.BaseType)
            {
                FieldInfo f = cur.GetField(name, All | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        public static object GetField(object obj, string name)
        {
            FieldInfo f = Field(obj.GetType(), name);
            return f != null ? f.GetValue(obj) : null;
        }

        public static void SetField(object obj, string name, object value)
        {
            FieldInfo f = Field(obj.GetType(), name);
            if (f != null) f.SetValue(obj, value);
        }

        public static object Invoke(object obj, string name, params object[] args)
        {
            MethodInfo m = null;
            for (Type cur = obj.GetType(); cur != null && m == null; cur = cur.BaseType)
                m = cur.GetMethod(name, All | BindingFlags.DeclaredOnly);
            return m != null ? m.Invoke(obj, args) : null;
        }

        /// <summary>
        /// Shallow-copy every instance field from <paramref name="src"/> onto <paramref name="dst"/>,
        /// walking the whole type hierarchy (so inherited private fields are copied too).
        /// Fields named in <paramref name="skip"/> are left untouched.
        /// </summary>
        public static void CopyFields(object src, object dst, params string[] skip)
        {
            if (src == null || dst == null) return;
            for (Type cur = src.GetType(); cur != null && cur != typeof(object); cur = cur.BaseType)
            {
                foreach (FieldInfo f in cur.GetFields(All | BindingFlags.DeclaredOnly))
                {
                    if (f.IsStatic || f.IsLiteral || f.IsInitOnly) continue;
                    if (Array.IndexOf(skip, f.Name) >= 0) continue;
                    f.SetValue(dst, f.GetValue(src));
                }
            }
        }
    }
}
