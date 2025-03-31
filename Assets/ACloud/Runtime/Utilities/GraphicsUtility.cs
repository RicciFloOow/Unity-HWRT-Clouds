using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace ACloud
{
    public static class GraphicsUtility
    {
        #region ----Constants----
        /// <summary>
        /// 我们认为在几何上应该被视为同一点的最大距离的平方
        /// </summary>
        public const float K_SamePointSquareDistance = 10e-16f;
        #endregion

        #region ----Texture----
        /// <summary>
        /// 全屏Blit, 需要着色器的vs是覆盖全屏的三角形的
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="pass"></param>
        /// <param name="material"></param>
        public static void CustumBlit(ref CommandBuffer cmd, RTHandle source, RTHandle target, int sourceId, int pass, Material material)
        {
            MaterialPropertyBlock _matPropertyBlock = new MaterialPropertyBlock();
            _matPropertyBlock.SetTexture(sourceId, source);
            //
            cmd.SetRenderTarget(target);
            cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, _matPropertyBlock);
        }
        #endregion

        #region ----Compute Buffer----
        public static void AllocateComputeBuffer(ref ComputeBuffer cb, int count, int stride, ComputeBufferType cbt = ComputeBufferType.Structured, ComputeBufferMode cbm = ComputeBufferMode.Immutable)
        {
            if (cb == null || cb.count != count || cb.stride != stride)
            {
                cb?.Release();
                cb = new ComputeBuffer(count, stride, cbt, cbm);
            }
        }
        #endregion

        #region ----Graphics Buffer----
        public static void AllocateGraphicsBuffer(ref GraphicsBuffer gb, int count, int stride, GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags flags = GraphicsBuffer.UsageFlags.None)
        {
            if (gb == null || gb.count != count || gb.stride != stride)
            {
                gb?.Release();
                gb = new GraphicsBuffer(target, flags, count, stride);
            }
        }
        #endregion

        #region ----Camera Matrix----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 CameraVPInverseMatrix(Camera cam, bool inverseZ = false, bool renderIntoTexture = true)
        {
            Matrix4x4 v = Matrix4x4.Scale(new Vector3(1, 1, inverseZ ? -1 : 1)) * cam.transform.worldToLocalMatrix;
            Matrix4x4 p = GL.GetGPUProjectionMatrix(cam.nonJitteredProjectionMatrix, renderIntoTexture);
            return (p * v).inverse;
        }
        #endregion
    }
}