//注释来源: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/TextureFormat.html
//以及 https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Experimental.Rendering.GraphicsFormat.html

namespace ACloud
{
    public enum NoiseTexFormat
    {
        /// <summary>
        /// Four channel (RGBA) texture format, 8-bits unsigned integer per channel.
        /// </summary>
        RGBA32,
        /// <summary>
        /// A four-component, 16-bit packed unsigned normalized format that has a 4-bit R component in bits 12..15, a 4-bit G component in bits 8..11, a 4-bit B component in bits 4..7, and a 4-bit A component in bits 0..3.
        /// </summary>
        RGBA4444
    }

    public enum NoiseTexFormatWithCompressed
    {
        /// <summary>
        /// Four channel (RGBA) texture format, 8-bits unsigned integer per channel.
        /// </summary>
        RGBA32,
        /// <summary>
        /// A four-component, 16-bit packed unsigned normalized format that has a 4-bit R component in bits 12..15, a 4-bit G component in bits 8..11, a 4-bit B component in bits 4..7, and a 4-bit A component in bits 0..3.
        /// </summary>
        RGBA4444,
        /// <summary>
        /// Compressed one channel (R) texture format.
        /// </summary>
        BC4,
        /// <summary>
        /// DXT5 (BC3) format compresses RGBA textures to 8 bits per pixel.
        /// </summary>
        BC3
    }
}