using UnityEngine;

namespace DLS.Graphics.World
{
    /// <summary>
    /// A CPU‐side pixel buffer you can write to, then blit as a Texture2D.
    /// Instantiate one per view/screen.
    /// </summary>
    public class PixelBuffer
    {
        public readonly int Width;
        public readonly int Height;

        private readonly Texture2D _texture;
        private readonly Color32[] _pixels;

        /// <summary>
        /// The Texture2D you can use in a Sprite, UI RawImage, or GUI.DrawTexture().
        /// </summary>
        public Texture2D Texture => _texture;

        /// <summary>
        /// Create a new buffer of size (width × height).
        /// </summary>
        public PixelBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            _pixels = new Color32[width * height];

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            Clear(new Color32(0, 0, 0, 255));
            Apply();
        }

        /// <summary>
        /// Fill the entire buffer with one color.
        /// </summary>
        public void Clear(Color32 color)
        {
            for (int i = 0; i < _pixels.Length; i++)
                _pixels[i] = color;
        }

        /// <summary>
        /// Set a single pixel in the buffer.
        /// </summary>
        public void SetPixel(int x, int y, Color32 color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            _pixels[y * Width + x] = color;
        }

        /// <summary>
        /// Bulk‑set all pixels at once (array length must equal width×height).
        /// </summary>
        public void SetPixels(Color32[] source)
        {
            if (source == null || source.Length != _pixels.Length)
                throw new System.ArgumentException("Source pixel array has wrong length");
            source.CopyTo(_pixels, 0);
        }

        /// <summawry>
        /// Pushes any changes in the pixel buffer up to the GPU Texture2D.
        /// Call once per frame after you finish writing pixels.
        /// </summary>
        public void Apply()
        {
            _texture.SetPixels32(_pixels);
            _texture.Apply();
        }

        /// <summary>
        /// Convenience: draw this buffer via immediate‐mode GUI.
        /// You can also just use <c>Texture</c> with a SpriteRenderer, RawImage, etc.
        /// </summary>
        public void DrawGUI(Rect screenRect)
        {
            GUI.DrawTexture(screenRect, _texture);
        }
    }
}
