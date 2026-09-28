using System;

namespace FTKModFramework.Core
{
    internal static class SkySpikeCameraMath
    {
        // Accumulate projected geometry once. A swept point is center + a*cos(angle) + b*sin(angle),
        // so its extrema cover every rotor angle without sampling or an inflated world-axis box.
        internal sealed class ViewportEnvelope
        {
            internal readonly double Tangent, Lower, Upper, Horizontal;
            internal double MaxLower = double.NegativeInfinity, MinUpper = double.PositiveInfinity;
            internal double MinimumDistance, MinZ = double.PositiveInfinity, MaxZ = double.NegativeInfinity;
            internal int Count;

            internal ViewportEnvelope(float fov, float aspect, float bottom, float top, float horizontalFraction)
            {
                Validate(fov, bottom, top);
                if (!SkySpikeAssetContract.Finite(aspect) || aspect <= 0 ||
                    !SkySpikeAssetContract.Finite(horizontalFraction) || horizontalFraction <= 0 || horizontalFraction > 1)
                    throw new ArgumentException("Invalid reveal viewport.");
                Tangent = Math.Tan(fov * Math.PI / 360.0);
                Lower = 2 * bottom - 1; Upper = 2 * top - 1;
                Horizontal = Tangent * aspect * horizontalFraction;
            }

            internal void Add(float x, float y, float z)
            { AddSweep(x, y, z, 0, 0, 0, 0, 0, 0); }

            internal void AddSweep(float x, float y, float z, float ax, float ay, float az, float bx, float by, float bz)
            {
                if (!SkySpikeAssetContract.Finite(x) || !SkySpikeAssetContract.Finite(y) || !SkySpikeAssetContract.Finite(z) ||
                    !SkySpikeAssetContract.Finite(ax) || !SkySpikeAssetContract.Finite(ay) || !SkySpikeAssetContract.Finite(az) ||
                    !SkySpikeAssetContract.Finite(bx) || !SkySpikeAssetContract.Finite(by) || !SkySpikeAssetContract.Finite(bz))
                    throw new ArgumentException("Nonfinite reveal geometry.");
                MaxLower = Math.Max(MaxLower, y + Upper * Tangent * z + Radius(ay + Upper * Tangent * az, by + Upper * Tangent * bz));
                MinUpper = Math.Min(MinUpper, y + Lower * Tangent * z - Radius(ay + Lower * Tangent * az, by + Lower * Tangent * bz));
                MinimumDistance = Math.Max(MinimumDistance, z + x / Horizontal + Radius(az + ax / Horizontal, bz + bx / Horizontal));
                MinimumDistance = Math.Max(MinimumDistance, z - x / Horizontal + Radius(az - ax / Horizontal, bz - bx / Horizontal));
                MaxZ = Math.Max(MaxZ, z + Radius(az, bz)); MinZ = Math.Min(MinZ, z - Radius(az, bz));
                Count++;
            }

            internal void OffsetInterval(float distance, out float lower, out float upper)
            {
                lower = Checked(MaxLower - Upper * Tangent * distance);
                upper = Checked(MinUpper - Lower * Tangent * distance);
            }

            static double Radius(double a, double b) { return Math.Sqrt(a * a + b * b); }
        }

        internal static bool TryFitReveal(ViewportEnvelope actors, ViewportEnvelope ship, float nearClip,
            out float distance, out float viewUp)
        {
            distance = 0; viewUp = 0;
            if (actors == null || ship == null || actors.Count == 0 || ship.Count == 0 ||
                !SkySpikeAssetContract.Finite(nearClip) || nearClip <= 0 || actors.Tangent != ship.Tangent) return false;
            ViewportEnvelope[] envelopes = { actors, ship };
            double d = 4;
            foreach (ViewportEnvelope a in envelopes)
            {
                d = Math.Max(d, Math.Max(a.MinimumDistance, a.MaxZ + nearClip));
                foreach (ViewportEnvelope b in envelopes)
                {
                    // Both bands must overlap so pulling back eventually creates a common aim interval.
                    double denominator = a.Tangent * (a.Upper - b.Lower);
                    if (denominator <= 0) return false;
                    d = Math.Max(d, (a.MaxLower - b.MinUpper) / denominator);
                }
            }
            d += .5; // Leave a small margin for animated poses and float projection error.
            if (double.IsNaN(d) || double.IsInfinity(d) || d > 250) return false;
            float al, au, sl, su;
            distance = (float)d;
            actors.OffsetInterval(distance, out al, out au); ship.OffsetInterval(distance, out sl, out su);
            float lower = Math.Max(al, sl), upper = Math.Min(au, su);
            if (lower > upper) { distance = 0; return false; }
            viewUp = lower + (upper - lower) * .5f;
            return SkySpikeAssetContract.Finite(viewUp);
        }

        internal static bool SameRotation(float ax, float ay, float az, float aw, float bx, float by, float bz, float bw)
        {
            // acos(dot(q,q)) can report a nonzero angle for the exact same float quaternion.
            // Ownership must survive a transform round-trip without mistaking its own pose for a writer.
            double dx = ax - bx, dy = ay - by, dz = az - bz, dw = aw - bw;
            double sx = ax + bx, sy = ay + by, sz = az + bz, sw = aw + bw;
            return Math.Min(dx * dx + dy * dy + dz * dz + dw * dw,
                sx * sx + sy * sy + sz * sz + sw * sw) < 1e-12;
        }

        internal const float TransitionDuration = .75f;

        internal static float EaseDistance(float current, float target, float elapsed)
        {
            if (!SkySpikeAssetContract.Finite(current) || !SkySpikeAssetContract.Finite(target) ||
                !SkySpikeAssetContract.Finite(elapsed) || current < 0f || target < 0f || elapsed < 0f)
                throw new ArgumentException("Invalid camera distance transition.");
            return Checked(target + (current - target) * Math.Exp(-elapsed * 2.8));
        }

        internal static float ReturnBlend(float elapsed)
        {
            if (!SkySpikeAssetContract.Finite(elapsed)) throw new ArgumentException("Invalid camera transition time.");
            float t = Math.Max(0f, Math.Min(1f, elapsed / TransitionDuration));
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        // Coordinates are relative to the content center in the camera basis, with Z toward the viewer.
        internal static float FitDistance(float x, float y, float z, float verticalFov, float aspect, float safeFraction)
        {
            return FitViewportDistance(x, y, z, verticalFov, aspect, (1f - safeFraction) * .5f,
                (1f + safeFraction) * .5f, safeFraction);
        }

        internal static float FitViewportDistance(float x, float y, float z, float verticalFov, float aspect,
            float viewportBottom, float viewportTop, float horizontalFraction)
        {
            Validate(verticalFov, viewportBottom, viewportTop);
            if (!SkySpikeAssetContract.Finite(x) || !SkySpikeAssetContract.Finite(y) || !SkySpikeAssetContract.Finite(z) ||
                !SkySpikeAssetContract.Finite(aspect) || aspect <= 0f || !SkySpikeAssetContract.Finite(horizontalFraction) ||
                horizontalFraction <= 0f || horizontalFraction > 1f)
                throw new ArgumentException("Invalid camera framing inputs.");
            double tangent = Math.Tan(verticalFov * Math.PI / 360.0);
            double lower = 2.0 * viewportBottom - 1.0, upper = 2.0 * viewportTop - 1.0;
            double middle = (lower + upper) * .5;
            // Translate along view-up by -distance*tangent*middle without changing rotation. Solving
            // both projection inequalities retains the requested band even for corners at different depths.
            double above = (y + upper * tangent * z) / (tangent * (upper - middle));
            double below = (-y - lower * tangent * z) / (tangent * (middle - lower));
            double horizontal = z + Math.Abs(x) / (tangent * aspect * horizontalFraction);
            return Checked(Math.Max(z + .1, Math.Max(horizontal, Math.Max(above, below))));
        }

        internal static float ViewUpOffset(float distance, float verticalFov, float viewportBottom, float viewportTop)
        {
            Validate(verticalFov, viewportBottom, viewportTop);
            if (!SkySpikeAssetContract.Finite(distance) || distance < 0f) throw new ArgumentException("Invalid camera distance.");
            return Checked(-distance * Math.Tan(verticalFov * Math.PI / 360.0) * (viewportBottom + viewportTop - 1.0));
        }

        internal static float FarClipForDepthRange(float nearClip, float nativeFarClip, float nearestDepth, float farthestDepth)
        {
            return FarClipForPresentation(nearClip, nativeFarClip, nearestDepth, farthestDepth, false);
        }

        internal static float FarClipForPresentation(float nearClip, float nativeFarClip, float nearestDepth, float farthestDepth,
            bool returningFromNative)
        {
            if (!SkySpikeAssetContract.Finite(nearClip) || !SkySpikeAssetContract.Finite(nativeFarClip) ||
                !SkySpikeAssetContract.Finite(nearestDepth) || !SkySpikeAssetContract.Finite(farthestDepth) ||
                nearClip <= 0f || nativeFarClip <= nearClip || farthestDepth < nearestDepth ||
                (!returningFromNative && nearestDepth <= nearClip))
                throw new ArgumentException("Camera content lies outside a valid depth range.");
            // A valid native close-up may exclude other fighters behind its near plane. During the bounded
            // return preserve that native clipping, extending only the far plane for points ahead of it.
            return Checked(Math.Max(nativeFarClip, (double)farthestDepth + 2.0));
        }

        static void Validate(float verticalFov, float bottom, float top)
        {
            if (!SkySpikeAssetContract.Finite(verticalFov) || verticalFov <= 1f || verticalFov >= 179f ||
                !SkySpikeAssetContract.Finite(bottom) || !SkySpikeAssetContract.Finite(top) || bottom < 0f || top > 1f || bottom >= top)
                throw new ArgumentException("Invalid camera viewport.");
        }

        static float Checked(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > float.MaxValue)
                throw new ArgumentException("Camera framing overflow.");
            return (float)value;
        }
    }
}
