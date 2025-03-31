using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public partial class VolumeSDFEditWindow : EditorWindow
    {
        #region ----Material----
        private Material m_rasterVoxelizerMat;

        private void SetupMaterials()
        {
            m_rasterVoxelizerMat = new Material(Shader.Find("ACloud/Editor/RasterVoxelizer"));
        }

        private void ReleaseMaterials()
        {
            if (m_rasterVoxelizerMat != null)
            {
                DestroyImmediate(m_rasterVoxelizerMat);
                m_rasterVoxelizerMat = null;
            }
        }
        #endregion

        #region ----Compute Shader----
        private ComputeShader m_voxelFillerComputeShader;
        private ComputeShader m_volumeSDFJFAComputeShader;

        private void InitComputeShaders()
        {
            if (m_voxelFillerComputeShader == null)
            {
                m_voxelFillerComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ACloud/Shader/Editor/3DSDF/VoxelFiller.compute");
            }
            if (m_volumeSDFJFAComputeShader == null)
            {
                m_volumeSDFJFAComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ACloud/Shader/Editor/3DSDF/VolumeSDF.compute");
            }
        }
        #endregion
    }
#endif
}