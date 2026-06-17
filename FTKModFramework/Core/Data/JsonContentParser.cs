using System;
using Newtonsoft.Json;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// Fault-tolerant Newtonsoft wrapper. Parsing NEVER throws out of the loader (FR-2): a malformed
    /// content file produces a validation error that names the file and is skipped; other files and
    /// mods still load.
    ///
    /// Uses ONLY the game's shipped <c>Newtonsoft.Json.dll</c> (referenced <c>Private=false</c>) — no new
    /// third-party runtime dependency (NFR-1). JSON scalars land in <c>ContentEntry.Fields</c> as their
    /// natural CLR boxes (<c>long</c>/<c>double</c>/<c>bool</c>/<c>string</c>); <c>OverrideEngine</c>
    /// coerces them to each field's primitive via <c>Convert.ChangeType</c>.
    /// </summary>
    internal static class JsonContentParser
    {
        // No MissingMemberHandling.Error: unknown JSON keys are ignored, not fatal — the flat DTO is
        // deliberately permissive and field-level handling happens in OverrideEngine. (The game's bundled
        // Newtonsoft predates FloatParseHandling; its default boxes JSON numbers as long/double, which is
        // exactly what OverrideEngine's Convert.ChangeType coercion expects.)
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        /// <summary>Strict deserialize; throws on malformed input (callers wrap in try/catch).</summary>
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        /// <summary>
        /// Parse one content file into a <see cref="ContentFile"/>. On malformed JSON, records a
        /// file-naming error on <paramref name="report"/> and returns null (never throws).
        /// </summary>
        public static ContentFile ParseFile(string path, ValidationReport report)
        {
            string json;
            try
            {
                json = System.IO.File.ReadAllText(path);
            }
            catch (Exception e)
            {
                report.Error("content file '" + path + "': could not be read: " + e.Message);
                return null;
            }

            try
            {
                ContentFile file = Deserialize<ContentFile>(json);
                if (file == null)
                {
                    report.Error("content file '" + path + "': parsed to null (empty or 'null' content).");
                    return null;
                }
                file.SourcePath = path;
                return file;
            }
            catch (Exception e)
            {
                report.Error("content file '" + path + "': malformed JSON: " + e.Message);
                return null;
            }
        }
    }
}
