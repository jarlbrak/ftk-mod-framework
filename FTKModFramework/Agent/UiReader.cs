using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FTKModFramework.Agent
{
    // Observation only: coordinates are pixels from the bottom-left, matching native Unity input.
    internal static class UiReader
    {
        internal static string PathOf(GameObject go)
        {
            if (go == null) return null;
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
            return path;
        }

        private static object ControlValue(Selectable control)
        {
            Slider slider = control as Slider;
            if (slider != null) return slider.value;
            Scrollbar scrollbar = control as Scrollbar;
            if (scrollbar != null) return scrollbar.value;
            Toggle toggle = control as Toggle;
            if (toggle != null) return toggle.isOn;
            Dropdown dropdown = control as Dropdown;
            if (dropdown != null) return dropdown.value;
            return null;
        }

        private static object Vector(Vector3 value)
        {
            return new Dictionary<string, object> { {"x", value.x}, {"y", value.y}, {"z", value.z} };
        }

        private static object Bindings()
        {
            List<object> bindings = new List<object>();
            if (FTKInput.Instance == null || FTKInput.Instance.m_RemappableKeys == null) return bindings;
            foreach (var mapping in FTKInput.Instance.m_RemappableKeys)
                bindings.Add(new Dictionary<string, object> {
                    {"action", mapping.m_ActionName}, {"positiveKeys", mapping.m_PosKeys},
                    {"negativeKeys", mapping.m_NegKeys}, {"positiveModifiers", mapping.m_PosMods},
                    {"negativeModifiers", mapping.m_NegMods}
                });
            return bindings;
        }

        internal static object Read()
        {
            EventSystem system = EventSystem.current;
            List<object> controls = new List<object>();
            foreach (Selectable control in UnityEngine.Object.FindObjectsOfType<Selectable>())
            {
                if (!control.isActiveAndEnabled) continue;
                RectTransform rect = control.transform as RectTransform;
                Canvas canvas = control.GetComponentInParent<Canvas>();
                if (rect == null || canvas == null || !canvas.isActiveAndEnabled) continue;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                List<object> labels = new List<object>();
                foreach (Text label in control.GetComponentsInChildren<Text>())
                    if (label.isActiveAndEnabled && !string.IsNullOrEmpty(label.text)) labels.Add(label.text);
                List<RaycastResult> hits = new List<RaycastResult>();
                if (system != null)
                    system.RaycastAll(new PointerEventData(system) { position = center }, hits);
                GameObject hit = hits.Count > 0 ? hits[0].gameObject : null;
                bool reachable = hit != null && (hit == control.gameObject || hit.transform.IsChildOf(control.transform));
                controls.Add(new Dictionary<string, object> {
                    {"id", control.GetInstanceID()}, {"path", PathOf(control.gameObject)},
                    {"type", control.GetType().Name}, {"labels", labels},
                    {"interactable", control.IsInteractable()}, {"centerHit", reachable},
                    {"value", ControlValue(control)},
                    {"x", center.x}, {"y", center.y}, {"topHit", PathOf(hit)}
                });
                if (controls.Count >= 512) break;
            }
            List<object> cameras = new List<object>();
            foreach (Camera camera in Camera.allCameras)
                cameras.Add(new Dictionary<string, object> {
                    {"path", PathOf(camera.gameObject)}, {"position", Vector(camera.transform.position)},
                    {"rotation", Vector(camera.transform.eulerAngles)}, {"fieldOfView", camera.fieldOfView},
                    {"orthographicSize", camera.orthographicSize}
                });
            List<object> texts = new List<object>();
            foreach (Text label in UnityEngine.Object.FindObjectsOfType<Text>())
            {
                if (!label.isActiveAndEnabled || string.IsNullOrEmpty(label.text)) continue;
                texts.Add(new Dictionary<string, object> { {"path", PathOf(label.gameObject)}, {"text", label.text} });
                if (texts.Count >= 512) break;
            }
            return new Dictionary<string, object> {
                {"bindings", Bindings()}, {"cameras", cameras}, {"texts", texts}, {"textsTruncated", texts.Count >= 512},
                {"frame", Time.frameCount}, {"width", Screen.width}, {"height", Screen.height},
                {"coordinateOrigin", "bottom-left"}, {"focused", Application.isFocused},
                {"selected", system == null ? null : PathOf(system.currentSelectedGameObject)},
                {"module", system == null || system.currentInputModule == null ? null : system.currentInputModule.GetType().FullName},
                {"controls", controls}, {"truncated", controls.Count >= 512}
            };
        }
    }
}
