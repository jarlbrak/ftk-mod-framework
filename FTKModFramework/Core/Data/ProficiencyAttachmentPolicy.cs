namespace FTKModFramework.Core.Data
{
    internal enum ProficiencyAttachmentMode { None, Attach, Replace, Invalid }

    internal static class ProficiencyAttachmentPolicy
    {
        internal static ProficiencyAttachmentMode Resolve(string kind, string[] proficiencies, bool replace)
        {
            if (replace)
                return kind == "weapon" && proficiencies != null
                    ? ProficiencyAttachmentMode.Replace : ProficiencyAttachmentMode.Invalid;
            if (proficiencies == null) return ProficiencyAttachmentMode.None;
            if (proficiencies.Length == 0)
                return kind == "weapon" ? ProficiencyAttachmentMode.None : ProficiencyAttachmentMode.Invalid;
            return ProficiencyAttachmentMode.Attach;
        }
    }
}
