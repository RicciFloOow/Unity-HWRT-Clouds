//ref: https://www.comp.nus.edu.sg/~tants/jfa/rong-guodong-phd-thesis.pdf
//在这里, 我们用1+JFA将体素化的模型信息(生成的Tex3D)转为SDF图(3D的)
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public partial class VolumeSDFEditWindow : EditorWindow
    {
        #region ----Voxel To SDF----
        private void ExecuteSDFInitTexKernel(ref CommandBuffer cmd, ref RTHandle sdfHandle, ComputeShader cs, RTHandle voxelHandle)
        {
            int kernelIndex = cs.FindKernel("InitTexKernel");
            cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
            cmd.SetRenderTarget(sdfHandle);
            cmd.SetComputeVectorParam(cs, k_shaderProperty_Vec_SDFTexSize, new Vector3(sdfHandle.Width, sdfHandle.Height, sdfHandle.VolumeDepth));
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_VoxelTex, voxelHandle);
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_RW_SDFIntermediateTex, sdfHandle);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(sdfHandle.Width / (float)x), Mathf.CeilToInt(sdfHandle.Height / (float)y), Mathf.CeilToInt(sdfHandle.VolumeDepth / (float)z));
        }

        private void ExecuteSDFJFAKernel(ref CommandBuffer cmd, ref RTHandle inputHandle, ref RTHandle outputHandle, ComputeShader cs, int step)
        {
            int kernelIndex = cs.FindKernel("JFAKernel");
            cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
            cmd.SetRenderTarget(outputHandle);
            cmd.SetComputeIntParam(cs, k_shaderProperty_Int_JumpStep, step);
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_SDFIntermediateTex, inputHandle);
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_RW_SDFIntermediateTex, outputHandle);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(inputHandle.Width / (float)x), Mathf.CeilToInt(inputHandle.Height / (float)y), Mathf.CeilToInt(inputHandle.VolumeDepth / (float)z));
        }

        private void ExecuteSDFGenerateSDFDistanceKernel(ref CommandBuffer cmd, ref RTHandle sdfHandle, ComputeShader cs, RTHandle inputHandle)
        {
            int kernelIndex = cs.FindKernel("GenerateSDFDistanceKernel");
            cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
            cmd.SetRenderTarget(sdfHandle);
            cmd.SetComputeVectorParam(cs, k_shaderProperty_Vec_SDFTexInvSize, new Vector3(1f / inputHandle.Width, 1f / inputHandle.Height, 1f / inputHandle.VolumeDepth));
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_SDFIntermediateTex, inputHandle);
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_RW_SDFTex, sdfHandle);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(inputHandle.Width / (float)x), Mathf.CeilToInt(inputHandle.Height / (float)y), Mathf.CeilToInt(inputHandle.VolumeDepth / (float)z));
        }

        private void ConvertVoxelToSDFJFA(ref RTHandle voxelHandle, ref RTHandle sdfHandle)
        {
            Vector3Int tex3DSize = new Vector3Int(voxelHandle.Width, voxelHandle.Height, voxelHandle.VolumeDepth);
            RTHandle pingpongHandle0 = new RTHandle(tex3DSize, GraphicsFormat.R32G32B32A32_UInt, 0, true);
            RTHandle pingpongHandle1 = new RTHandle(tex3DSize, GraphicsFormat.R32G32B32A32_UInt, 0, true);
            sdfHandle = new RTHandle(tex3DSize, GraphicsFormat.R32_SFloat, 0, true);
            //
            int pingpong = 0;
            int maxSize = Mathf.Max(tex3DSize.x, tex3DSize.y, tex3DSize.z);
            int totalSteps = Mathf.CeilToInt(Mathf.Log(maxSize, 2));
            //
            CommandBuffer voxel2SDFCmdBuffer = new CommandBuffer()
            {
                name = "Voxel To SDF Pass"
            };
            ExecuteSDFInitTexKernel(ref voxel2SDFCmdBuffer, ref pingpongHandle0, m_volumeSDFJFAComputeShader, voxelHandle);
            ExecuteSDFJFAKernel(ref voxel2SDFCmdBuffer, ref pingpongHandle0, ref pingpongHandle1, m_volumeSDFJFAComputeShader, 1);
            for (int i = 0; i < totalSteps; i++)
            {
                if (pingpong == 0)
                {
                    ExecuteSDFJFAKernel(ref voxel2SDFCmdBuffer, ref pingpongHandle1, ref pingpongHandle0, m_volumeSDFJFAComputeShader, Mathf.CeilToInt(maxSize / Mathf.Pow(2, i + 1)));
                }
                else
                {
                    ExecuteSDFJFAKernel(ref voxel2SDFCmdBuffer, ref pingpongHandle0, ref pingpongHandle1, m_volumeSDFJFAComputeShader, Mathf.CeilToInt(maxSize / Mathf.Pow(2, i + 1)));
                }
                pingpong = (pingpong + 1) % 2;
            }
            if (pingpong == 0)
            {
                ExecuteSDFGenerateSDFDistanceKernel(ref voxel2SDFCmdBuffer, ref sdfHandle, m_volumeSDFJFAComputeShader, pingpongHandle1);
            }
            else
            {
                ExecuteSDFGenerateSDFDistanceKernel(ref voxel2SDFCmdBuffer, ref sdfHandle, m_volumeSDFJFAComputeShader, pingpongHandle0);
            }
            Graphics.ExecuteCommandBuffer(voxel2SDFCmdBuffer);
            //
            pingpongHandle0.Release();
            pingpongHandle1.Release();
        }

        #endregion
    }
#endif
}