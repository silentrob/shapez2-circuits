using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace Circuits.Kit
{
    // Development aid: saves every texture a material uses as a PNG, plus a list of its texture properties, so
    // a mesh made in code can pick colours from the game's palette (building meshes are coloured by where their
    // UVs point in a palette texture). Game textures are usually not CPU-readable, so each is copied through a
    // render texture first. Runs on the main thread, from a renderer.
    public static class TextureDump
    {
        public static void Dump(Material material, string folder, ILogger logger)
        {
            Directory.CreateDirectory(folder);
            var report = new List<string> { $"Material {material.name}, shader {material.shader.name}" };
            foreach (string property in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(property);
                if (texture == null)
                {
                    report.Add($"{property}: none");
                    continue;
                }

                string file = Path.Combine(folder, $"{property.TrimStart('_')}.png");
                File.WriteAllBytes(file, ReadablePng(texture));
                report.Add($"{property}: {texture.name} {texture.width}x{texture.height} -> {Path.GetFileName(file)}");
            }

            File.WriteAllLines(Path.Combine(folder, "material.txt"), report);
            logger.Info?.Log($"[Circuits] Saved {report.Count - 1} texture properties of {material.name} to {folder}");
        }

        private static byte[] ReadablePng(Texture texture)
        {
            RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                byte[] png = copy.EncodeToPNG();
                Object.Destroy(copy);
                return png;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
