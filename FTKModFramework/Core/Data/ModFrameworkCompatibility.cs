using System;

namespace FTKModFramework.Core.Data
{
    /// <summary>Manifest versions use three canonical decimal Int32 components.</summary>
    internal static class ModFrameworkCompatibility
    {
        internal static bool TryParse(string value, out Version version)
        {
            version = null;
            if (value == null) return false;
            string[] parts = value.Split('.');
            if (parts.Length != 3) return false;
            int[] numbers = new int[3];
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0 || (part.Length > 1 && part[0] == '0')) return false;
                foreach (char c in part) if (c < '0' || c > '9') return false;
                if (!int.TryParse(part, out numbers[i])) return false;
            }
            version = new Version(numbers[0], numbers[1], numbers[2]);
            return true;
        }

        internal static string Reason(string declared, string running)
        {
            Version required, current;
            if (!TryParse(declared, out required))
                return "Unverified: the mod author must declare frameworkVersion as X.Y.Z.";
            if (!TryParse(running, out current))
                return "The running framework version could not be verified.";
            if (current.Major != required.Major)
                return "Requires framework " + declared + " within major " + required.Major + ". The author must confirm this framework major.";
            if (current.CompareTo(required) < 0)
                return "Requires framework " + declared + " or newer within the same major.";
            return null;
        }
    }
}
