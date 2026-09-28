using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FTKModFramework.Core
{
    // Own only transform carriers on a custom ship instance. Meshes, materials and gameplay state stay untouched.
    internal sealed class SkySpikeRotors : MonoBehaviour
    {
        Transform[] _carriers;
        Vector3[] _axes;
        float[] _speeds, _angles;

        internal static void Attach(GameObject root, SkySpikeAssetContract contract)
        {
            if (contract == null || contract.Rotors.Length == 0) return;
            List<Transform>[] groups = new List<Transform>[contract.Rotors.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = new List<Transform>();
                foreach (Transform part in root.transform)
                    if (SkySpikeAssetContract.MatchesNode(part.name, contract.Rotors[i].Node)) groups[i].Add(part);
                if (groups[i].Count == 0) throw new InvalidDataException("Rotor has no rendered parts: " + contract.Rotors[i].Node);
            }
            SkySpikeRotors motion = root.AddComponent<SkySpikeRotors>();
            motion._carriers = new Transform[groups.Length];
            motion._axes = new Vector3[groups.Length];
            motion._speeds = new float[groups.Length];
            motion._angles = new float[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                SkySpikeAssetContract.Rotor rotor = contract.Rotors[i];
                Vector3 pivot = new Vector3(rotor.Pivot[0], rotor.Pivot[1], rotor.Pivot[2]);
                GameObject carrier = new GameObject("SkySpike rotor " + rotor.Node);
                carrier.layer = root.layer;
                carrier.transform.SetParent(root.transform, false);
                carrier.transform.localPosition = pivot;
                foreach (Transform part in groups[i])
                {
                    Vector3 original = part.localPosition;
                    part.SetParent(carrier.transform, false);
                    part.localPosition = original - pivot;
                }
                motion._carriers[i] = carrier.transform;
                motion._axes[i] = new Vector3(rotor.Axis[0], rotor.Axis[1], rotor.Axis[2]);
                motion._speeds[i] = rotor.DegreesPerSecond;
                Plugin.Log.LogInfo("[SkySpike] rotor '" + rotor.Node + "' on '" + root.name + "': " + groups[i].Count +
                    " parts, pivot " + pivot + ", axis " + motion._axes[i] + ", degrees/sec " + rotor.DegreesPerSecond + ".");
            }
        }

        void Update()
        {
            if (_carriers == null) return;
            for (int i = 0; i < _carriers.Length; i++)
                if (_carriers[i] != null)
                {
                    _angles[i] = SkySpikeRotorMath.Advance(_angles[i], _speeds[i], Time.deltaTime);
                    _carriers[i].localRotation = Quaternion.AngleAxis(_angles[i], _axes[i]);
                }
        }

        void OnDisable()
        {
            // Pooled combat placement measures the authored rest pose before the instance is shown again.
            if (_carriers == null) return;
            for (int i = 0; i < _carriers.Length; i++)
            {
                _angles[i] = 0f;
                if (_carriers[i] != null) _carriers[i].localRotation = Quaternion.identity;
            }
        }
    }
}
