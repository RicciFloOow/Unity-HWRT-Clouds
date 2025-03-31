using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public partial class VolumeSDFEditWindow : EditorWindow
    {
        #region ----Int----
        private readonly static int k_shaderProperty_Int_JumpStep = Shader.PropertyToID("_JumpStep");
        #endregion

        #region ----Vector----
        private readonly static int k_shaderProperty_Vec_VoxelTexSize = Shader.PropertyToID("_VoxelTexSize");
        private readonly static int k_shaderProperty_Vec_SDFTexSize = Shader.PropertyToID("_SDFTexSize");
        private readonly static int k_shaderProperty_Vec_SDFTexInvSize = Shader.PropertyToID("_SDFTexInvSize");
        #endregion

        #region ----Texs----
        private readonly static int k_shaderProperty_Tex_VoxelTex = Shader.PropertyToID("_VoxelTex");
        private readonly static int k_shaderProperty_Tex_SDFIntermediateTex = Shader.PropertyToID("_SDFIntermediateTex");
        private readonly static int k_shaderProperty_Tex_RW_VoxelTex = Shader.PropertyToID("RW_VoxelTex");
        private readonly static int k_shaderProperty_Tex_RW_SDFIntermediateTex = Shader.PropertyToID("RW_SDFIntermediateTex");
        private readonly static int k_shaderProperty_Tex_RW_SDFTex = Shader.PropertyToID("RW_SDFTex");
        #endregion
    }
#endif
}