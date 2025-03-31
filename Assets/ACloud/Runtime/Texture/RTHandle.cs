using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace ACloud
{
    public class RTHandle
    {
        private RenderTexture m_RT;
        private RenderTargetIdentifier m_RTIdentifier;

        public static implicit operator RenderTexture(RTHandle handle)
        {
            return handle.m_RT;
        }

        public static implicit operator RenderTargetIdentifier(RTHandle handle)
        {
            return handle.m_RTIdentifier;
        }

        public RenderBuffer ColorBuffer => m_RT.colorBuffer;

        public RenderBuffer DepthBuffer => m_RT.depthBuffer;
        public int Width => m_RT.width;

        public int Height => m_RT.height;

        public int VolumeDepth => m_RT.volumeDepth;

        public Vector4 TexSize => new Vector4(m_RT.width, m_RT.height, 1f / m_RT.width, 1f / m_RT.height);

        public RTHandle(int width, int height, int depth, GraphicsFormat format, int mipCount = 0, bool enableRandomWrite = false)
        {
            if (mipCount <= 1)
            {
                m_RT = new RenderTexture(width, height, depth, format);
            }
            else
            {
                //需要长宽都是2的幂次，mipmap才能有效生效
                width = Mathf.ClosestPowerOfTwo(width);
                height = Mathf.ClosestPowerOfTwo(height);
                m_RT = new RenderTexture(width, height, depth, format, mipCount);
                m_RT.useMipMap = true;
                m_RT.autoGenerateMips = false;
            }
            m_RT.enableRandomWrite = enableRandomWrite;
            m_RTIdentifier = new RenderTargetIdentifier(m_RT);
        }

        public RTHandle(int width, int height, int depth, RenderTextureFormat format, int mipCount = 0, bool enableRandomWrite = false)
        {
            if (mipCount <= 1)
            {
                m_RT = new RenderTexture(width, height, depth, format);
            }
            else
            {
                //需要长宽都是2的幂次，mipmap才能有效生效
                width = Mathf.ClosestPowerOfTwo(width);
                height = Mathf.ClosestPowerOfTwo(height);
                m_RT = new RenderTexture(width, height, depth, format, mipCount);
                m_RT.useMipMap = true;
                m_RT.autoGenerateMips = false;
            }
            m_RT.enableRandomWrite = enableRandomWrite;
            m_RTIdentifier = new RenderTargetIdentifier(m_RT);
        }

        public RTHandle(int width, int height, GraphicsFormat colorFormat, GraphicsFormat depthFormat, int mipCount = 0, bool enableRandomWrite = false)
        {

            if (mipCount <= 1)
            {
                m_RT = new RenderTexture(width, height, colorFormat, depthFormat);
            }
            else
            {
                //需要长宽都是2的幂次，mipmap才能有效生效
                width = Mathf.ClosestPowerOfTwo(width);
                height = Mathf.ClosestPowerOfTwo(height);
                m_RT = new RenderTexture(width, height, colorFormat, depthFormat, mipCount);
                m_RT.useMipMap = true;
                m_RT.autoGenerateMips = false;
            }
            m_RT.enableRandomWrite = enableRandomWrite;
            m_RTIdentifier = new RenderTargetIdentifier(m_RT);
        }

        public RTHandle(int width, int height, int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite)
        {
            m_RT = new RenderTexture(width, height, depth, format, readWrite);
            m_RTIdentifier = new RenderTargetIdentifier(m_RT);
        }

        public RTHandle(int size, GraphicsFormat colorFormat)
        {
            //cubemap
            size = Mathf.NextPowerOfTwo(size);
            m_RT = new RenderTexture(size, size, colorFormat, GraphicsFormat.None);
            m_RT.dimension = TextureDimension.Cube;
            m_RTIdentifier = new RenderTargetIdentifier(m_RT);
        }

        public RTHandle(Vector3Int size3D, GraphicsFormat colorFormat, int mipCount = 0, bool enableRandomWrite = false)
        {
            m_RT = new RenderTexture(size3D.x, size3D.y, colorFormat, GraphicsFormat.None, mipCount);
            m_RT.dimension = TextureDimension.Tex3D;
            m_RT.volumeDepth = size3D.z;
            if (mipCount > 1)
            {
                //我们假设size3D都是2的幂次
                m_RT.useMipMap = true;
                m_RT.autoGenerateMips = false;
            }
            m_RT.enableRandomWrite = enableRandomWrite;
            m_RTIdentifier = new RenderTargetIdentifier(m_RT);
        }

        public void Release()
        {
            m_RT?.Release();
            m_RT = null;
            m_RTIdentifier = BuiltinRenderTextureType.None;
        }
    }
}