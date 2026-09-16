using UnityEngine;

namespace FTKModFramework.Core
{
    // Lives only on the spawned CEL. Serialized fields carry the original baseline when Unity
    // clones an already dressed avatar; repeated applications never multiply an applied scale.
    internal sealed class EnemyVisualScale : MonoBehaviour
    {
        [SerializeField] private bool _captured;
        [SerializeField] private Vector3 _originalScale;

        internal void Apply(float scale, float widthBoost, bool legacyAbsoluteScale)
        {
            if (!_captured)
            {
                _originalScale = transform.localScale;
                _captured = true;
            }
            Vector3 baseline = legacyAbsoluteScale ? Vector3.one : _originalScale;
            float xz = scale * (widthBoost > 0f ? widthBoost : 1f);
            transform.localScale = new Vector3(baseline.x * xz, baseline.y * scale, baseline.z * xz);
        }
    }
}
