namespace UnityEngine
{
    public enum TextureFormat { ARGB32 = 5 }
    public enum TextureWrapMode { Clamp = 1 }
    public class Texture { public TextureWrapMode wrapMode { get; set; } }
    public class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat format, bool mipmap, bool linear) { } }
    public struct Rect { public float width { get { return 8; } } public float height { get { return 8; } } }
    public class RectTransform { public Rect rect { get { return new Rect(); } } }
}
namespace UnityEngine.UI
{
    public class Graphic { public UnityEngine.RectTransform rectTransform { get { return null; } } }
    public class RawImage : Graphic { public UnityEngine.Texture texture { get; set; } }
}
namespace GridEditor
{
    public class FTK_enemyCombat
    {
        public enum ID { None = -1, Enemy = 0 }
        public static ID GetEnum(string value) { return ID.None; }
    }
}
public class uiEnemyEncounterPortrait { public uiActiveTimePortrait m_Portrait; public void Initialize(string value) { } }
public class uiActiveTimePortrait { public UnityEngine.UI.RawImage m_RawImage; }
public class uiActiveTime { public static uiActiveTime Instance { get { return null; } } public int m_OffscreenEnemyPortraitAA; }
namespace FTKModFramework { internal static class Plugin { internal static class Log { internal static void LogWarning(string s) { } } } }
