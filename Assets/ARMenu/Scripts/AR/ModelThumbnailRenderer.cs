using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ARMenu
{
    /// <summary>
    /// Renders a "studio shot" of an existing 3D food model into a texture, used as the dish image
    /// when no photo is provided. Works on a temporary clone far away from the AR scene, so the
    /// original models, their components and their tracking are never touched.
    /// </summary>
    public static class ModelThumbnailRenderer
    {
        const int IsolationLayer = 31;
        static readonly Vector3 StudioPosition = new Vector3(0f, -5000f, 0f);

        public static Texture2D Render(GameObject source, int size, Color background, float yaw = -32f, float pitch = 30f)
        {
            if (source == null) return null;

            var holder = new GameObject("ARMenu Thumbnail Studio");
            holder.SetActive(false);
            holder.transform.position = StudioPosition;
            GameObject camGo = null, keyGo = null, rimGo = null;
            RenderTexture rt = null;
            Texture2D result = null;

            try
            {
                var clone = Object.Instantiate(source, holder.transform, false);
                clone.transform.localPosition = Vector3.zero;
                clone.SetActive(true);

                // Strip behaviours (Lean Touch etc.) before activation so nothing runs on the clone.
                foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                foreach (var col in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IsolationLayer;
                // The Vuforia handler hides renderers while the marker is not tracked – re-enable them on the clone.
                foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                holder.SetActive(true);

                if (!TryGetBounds(clone, out var bounds)) return null;

                camGo = new GameObject("ARMenu Thumbnail Camera");
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(background.r, background.g, background.b, 0f);
                cam.cullingMask = 1 << IsolationLayer;
                cam.fieldOfView = 26f;
                cam.allowHDR = false;
                cam.allowMSAA = true;
                var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
                camData.renderShadows = false;
                camData.renderPostProcessing = false;

                var radius = Mathf.Max(0.0001f, bounds.extents.magnitude);
                var rotation = Quaternion.Euler(pitch, yaw, 0f);
                var distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.04f;
                cam.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
                cam.nearClipPlane = Mathf.Max(0.0001f, distance - radius * 2f);
                cam.farClipPlane = distance + radius * 2f;

                keyGo = CreateLight("Key", Quaternion.Euler(40f, yaw + 35f, 0f), new Color(1f, 0.93f, 0.82f), 0.45f);
                rimGo = CreateLight("Rim", Quaternion.Euler(15f, yaw + 180f, 0f), new Color(1f, 0.82f, 0.6f), 0.35f);

                var desc = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 24)
                {
                    sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                    msaaSamples = 4
                };
                rt = RenderTexture.GetTemporary(desc);

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                }

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                result = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = "ARMenu Thumbnail " + source.name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 2,
                    hideFlags = HideFlags.DontSave
                };
                result.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                result.Apply(true, false);
                RenderTexture.active = previous;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ARMenu] Could not render thumbnail for '{source.name}': {e.Message}");
                if (result) DestroySafe(result);
                result = null;
            }
            finally
            {
                if (rt) RenderTexture.ReleaseTemporary(rt);
                DestroySafe(camGo);
                DestroySafe(keyGo);
                DestroySafe(rimGo);
                DestroySafe(holder);
            }
            return result;
        }

        static GameObject CreateLight(string name, Quaternion rotation, Color color, float intensity)
        {
            var go = new GameObject("ARMenu Thumbnail " + name + " Light");
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << IsolationLayer;
            return go;
        }

        static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has && bounds.size.sqrMagnitude > 0f;
        }

        static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (o is GameObject go) go.SetActive(false); // Destroy() is deferred; hide immediately.
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
