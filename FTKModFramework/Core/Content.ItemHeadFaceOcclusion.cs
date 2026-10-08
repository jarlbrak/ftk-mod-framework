using System;

namespace FTKModFramework.Core
{
    public enum HeadHairMode { Preserve, ClipStrictHead, HideRenderer }

    /// <summary>One inward plane in the worn helmet MeshFilter's local coordinates.</summary>
    public sealed class HeadPlane
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }
        public float Distance { get; private set; }
        public HeadPlane(float x, float y, float z, float distance)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(distance) ||
                Math.Abs(x) > 1000f || Math.Abs(y) > 1000f || Math.Abs(z) > 1000f ||
                Math.Abs(distance) > 1000f)
                throw new ArgumentException("Face plane coordinates must be finite and bounded");
            double squared = (double)x*x + (double)y*y + (double)z*z;
            if (squared < 0.998001d || squared > 1.002001d)
                throw new ArgumentException("Face plane normal must be unit length");
            X = x; Y = y; Z = z; Distance = distance;
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }

    /// <summary>Bounded original authored face volume; omitted profile value leaves the native face intact.</summary>
    public sealed class HeadFaceOcclusion
    {
        private readonly HeadPlane[] planes;
        public string BodyPath { get; private set; }
        public HeadHairMode UpperHair { get; private set; }
        public HeadHairMode LowerHair { get; private set; }
        public HeadPlane[] Planes { get { return (HeadPlane[])planes.Clone(); } }

        public HeadFaceOcclusion(string bodyPath, HeadPlane[] planes,
            HeadHairMode upperHair, HeadHairMode lowerHair)
        {
            if (!ValidPath(bodyPath) || planes == null || planes.Length < 4 || planes.Length > 16 ||
                upperHair == HeadHairMode.HideRenderer || !Enum.IsDefined(typeof(HeadHairMode), upperHair) ||
                !Enum.IsDefined(typeof(HeadHairMode), lowerHair))
                throw new ArgumentException("Face volume requires exact body path, 4..16 planes and supported hair modes");
            foreach (HeadPlane plane in planes) if (plane == null) throw new ArgumentException("Null face plane");
            BodyPath = bodyPath;
            this.planes = (HeadPlane[])planes.Clone();
            UpperHair = upperHair; LowerHair = lowerHair;
        }

        private static bool ValidPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('\\') >= 0) return false;
            foreach (string part in path.Split('/'))
                if (part.Length == 0 || part == "." || part == "..") return false;
            return true;
        }
    }
}
