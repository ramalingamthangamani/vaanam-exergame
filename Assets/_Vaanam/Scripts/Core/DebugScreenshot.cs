using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Vaanam.Core
{
    /// <summary>
    /// Saves a PNG of what the player sees, including the UI, by rendering the
    /// main camera into a texture. It does not depend on the editor's Game view
    /// being visible or focused, so it works from the editor command queue even
    /// while another window is in front. F12 in play mode saves one to
    /// the persistent data folder. Handy for sending a picture of a bug.
    /// </summary>
    public class DebugScreenshot : MonoBehaviour
    {
        public static void Capture(string path, int width = 1920, int height = 1080)
        {
            var go = new GameObject("DebugScreenshot");
            var shot = go.AddComponent<DebugScreenshot>();
            shot.StartCoroutine(shot.Run(path, width, height));
        }

        private IEnumerator Run(string path, int width, int height)
        {
            yield return null;   // let this frame's game logic finish first
            try
            {
                Camera cam = Camera.main;
                if (cam == null) throw new System.Exception("no main camera");

                // Overlay canvases cannot render into a texture; switch them to camera
                // space for this one render and put them back afterwards.
                var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                var restore = new List<(Canvas c, RenderMode mode, Camera cam, float dist)>();
                foreach (var c in canvases)
                {
                    if (c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isRootCanvas) continue;
                    restore.Add((c, c.renderMode, c.worldCamera, c.planeDistance));
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.05f;
                }
                Canvas.ForceUpdateCanvases();

                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                var prevTarget = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prevTarget;

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                foreach (var r in restore) { r.c.renderMode = r.mode; r.c.worldCamera = r.cam; r.c.planeDistance = r.dist; }
                Canvas.ForceUpdateCanvases();

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Destroy(tex); rt.Release(); Destroy(rt);
                Debug.Log("[Vaanam] Screenshot saved: " + path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Vaanam] Screenshot failed: " + e.Message);
            }
            Destroy(gameObject);
        }
    }
}
