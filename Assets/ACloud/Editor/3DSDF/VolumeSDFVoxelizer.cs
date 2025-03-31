//我们这里用正交相机, 通过光栅化的方法快速获得"体素化"后的网格3D图(注意, 这种方案并不是对所有网格都合适的, 不过对于云来说是足够的了)
//PS:最明显的问题就是
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public partial class VolumeSDFEditWindow : EditorWindow
    {
        #region ----Voxelizer Rendering Setting----
        private const float k_voxelizerSetting_CamNearPlane = 1.0f;
        private const float k_voxelizerSetting_VoxelBBMaxSize = 256;
        #endregion

        #region ----Voxelizer User Setting----
        private bool m_voxelizerSetting_AllowUserChange;

        //需要知道的是, 我们如果直接用网格的包围盒(即便对三边取最大值)做体素化然后生成SDF,
        //会导致纹理边缘处是网格的边缘, 这对采样影响很大
        //因此我们需要在包围盒上有一定的延伸
        private Vector3 m_voxelizerSetting_BBExtent;

        private void InitVoxelizerUserSetting()
        {
            m_voxelizerSetting_AllowUserChange = false;
            m_voxelizerSetting_BBExtent = Vector3.one;
        }
        #endregion

        #region ----Voxelizer Helper Funs----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Bounds MeshBoundsToVoxelBounds(Bounds meshBB, out Vector3 scale)
        {
            Vector3 meshSize = Vector3.Scale(meshBB.size, Vector3.one + m_voxelizerSetting_BBExtent);//延伸后的包围盒大小
            float meshMaxSize = Mathf.Max(meshSize.x, meshSize.y, meshSize.z);
            float m2mScale = k_voxelizerSetting_VoxelBBMaxSize / meshMaxSize;
            meshSize *= m2mScale;
            Vector3 pow2MeshSize = new Vector3(Mathf.NextPowerOfTwo(Mathf.CeilToInt(meshSize.x)), Mathf.NextPowerOfTwo(Mathf.CeilToInt(meshSize.y)), Mathf.NextPowerOfTwo(Mathf.CeilToInt(meshSize.z)));
            scale = m2mScale * Vector3.one;
            return new Bounds(meshBB.center, pow2MeshSize);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3Int VoxelBoundsToTexSize(VolumeSDFTexSize maxSDFTexSize, Bounds voxelBB)
        {
            int maxTexSize = (int)maxSDFTexSize;
            Vector3 bbSize = voxelBB.size;
            float bbMaxSize = Mathf.Max(bbSize.x, bbSize.y, bbSize.z);
            bbSize *= (1 / bbMaxSize) * maxTexSize;
            return new Vector3Int(Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(bbSize.x)), Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(bbSize.y)), Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(bbSize.z)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Matrix4x4 GetVoxelizerCameraProjectionMatrix(Bounds voxelBB, Vector3Int texSize, out float step)
        {
            Vector3 voxelSize = voxelBB.size;
            //
            step = voxelSize.z / texSize.z;
            float farPlane = k_voxelizerSetting_CamNearPlane + step;
            Matrix4x4 p = Matrix4x4.Ortho(-voxelSize.x * 0.5f, voxelSize.x * 0.5f, -voxelSize.y * 0.5f, voxelSize.y * 0.5f, k_voxelizerSetting_CamNearPlane, farPlane);
            return GL.GetGPUProjectionMatrix(p, false);
        }
        #endregion

        #region ----Mesh To Voxel(Rasterization)----
        private void ExecuteVoxelFillInnerXKernel(ref CommandBuffer cmd, ref RTHandle tex3D, ComputeShader cs)
        {
            int kernelIndex = cs.FindKernel("VoxelFillInnerXKernel");
            cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
            cmd.SetRenderTarget(tex3D);
            cmd.SetComputeVectorParam(cs, k_shaderProperty_Vec_VoxelTexSize, new Vector3(tex3D.Width, tex3D.Height, tex3D.VolumeDepth));
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_RW_VoxelTex, tex3D);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(tex3D.Width / (float)x), Mathf.CeilToInt(tex3D.Height / (float)y), Mathf.CeilToInt(tex3D.VolumeDepth / (float)z));
        }

        private void ExecuteVoxelFillInnerYKernel(ref CommandBuffer cmd, ref RTHandle tex3D, ComputeShader cs)
        {
            int kernelIndex = cs.FindKernel("VoxelFillInnerYKernel");
            cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
            cmd.SetRenderTarget(tex3D);
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_RW_VoxelTex, tex3D);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(tex3D.Width / (float)x), Mathf.CeilToInt(tex3D.Height / (float)y), Mathf.CeilToInt(tex3D.VolumeDepth / (float)z));
        }

        private void ExecuteVoxelFillInnerZKernel(ref CommandBuffer cmd, ref RTHandle tex3D, ComputeShader cs)
        {
            int kernelIndex = cs.FindKernel("VoxelFillInnerZKernel");
            cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
            cmd.SetRenderTarget(tex3D);
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_RW_VoxelTex, tex3D);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(tex3D.VolumeDepth / (float)x), Mathf.CeilToInt(tex3D.Height / (float)y), Mathf.CeilToInt(tex3D.Width / (float)z));
        }

        private void ConvertMeshToVoxelRasterization(ref RTHandle tex3D)
        {
            Bounds voxelBounds = MeshBoundsToVoxelBounds(m_selectMesh.bounds, out Vector3 meshScale);
            Vector3 voxelSize = voxelBounds.size;
            Vector3Int volumeTexSize = VoxelBoundsToTexSize(m_maxVolumeSDFTexSize, voxelBounds);
            Matrix4x4 projMat = GetVoxelizerCameraProjectionMatrix(voxelBounds, volumeTexSize, out float step);
            Matrix4x4 obj2WorldMat = Matrix4x4.TRS(-Vector3.Scale(voxelBounds.center, meshScale), Quaternion.identity, meshScale);
            //
            tex3D = new RTHandle(volumeTexSize, GraphicsFormat.R32_UInt, 0, true);//必定支持uav的格式(编辑器用opengl的话当我没说)
            RTHandle camColorHandle = new RTHandle(volumeTexSize.x, volumeTexSize.y, 0, GraphicsFormat.R32_UInt);
            //
            CommandBuffer mesh2VoxelCmdBuffer = new CommandBuffer()
            {
                name = "Raster Voxelizer Pass"
            };
            //
            for (int i = 0; i < volumeTexSize.z; i++)
            {
                mesh2VoxelCmdBuffer.SetRenderTarget(camColorHandle);
                mesh2VoxelCmdBuffer.ClearRenderTarget(false, true, Color.clear);
                Matrix4x4 viewMat = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(new Vector3(0, 0, -voxelSize.z * 0.5f - k_voxelizerSetting_CamNearPlane + i * step), Quaternion.identity, Vector3.one).inverse;
                mesh2VoxelCmdBuffer.SetViewProjectionMatrices(viewMat, projMat);
                mesh2VoxelCmdBuffer.DrawMesh(m_selectMesh, obj2WorldMat, m_rasterVoxelizerMat);
                //
                mesh2VoxelCmdBuffer.SetRenderTarget(tex3D);
                mesh2VoxelCmdBuffer.CopyTexture(camColorHandle, 0, tex3D, i);
            }
            //填充内部
            ExecuteVoxelFillInnerXKernel(ref mesh2VoxelCmdBuffer, ref tex3D, m_voxelFillerComputeShader);
            ExecuteVoxelFillInnerYKernel(ref mesh2VoxelCmdBuffer, ref tex3D, m_voxelFillerComputeShader);
            ExecuteVoxelFillInnerZKernel(ref mesh2VoxelCmdBuffer, ref tex3D, m_voxelFillerComputeShader);
            //
            Graphics.ExecuteCommandBuffer(mesh2VoxelCmdBuffer);
            //
            camColorHandle.Release();
        }
        #endregion
    }
#endif
}