using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public partial class VolumeSDFEditWindow : EditorWindow
    {
        #region ----Command----
        public static void InitWindow()
        {
            CurrentWin = GetWindowWithRect<VolumeSDFEditWindow>(new Rect(0, 0, k_GUI_WindowWidth, k_GUI_WindowHeight));
            CurrentWin.titleContent = new GUIContent("3DSDFGenerator", "生成模型的3DSDF");
            CurrentWin.Focus();
            //
            s_disableOperateParameters = false;
        }

        [MenuItem("ACloud/SDF/3DSDFGenerator")]
        public static void OpenWindowFromMenu()
        {
            InitWindow();
        }
        #endregion

        #region ----Static Properties----
        public static VolumeSDFEditWindow CurrentWin { get; private set; }

        private static bool s_disableOperateParameters;
        #endregion

        #region ----GUI Properties----
        private string m_setting_ExportFileName;
        private string m_setting_ExportPath;

        private Vector2 m_scrollPosition;

        private VolumeSDFTexSize m_maxVolumeSDFTexSize;
        private Mesh m_selectMesh;

        private void SetInitProperties()
        {
            m_setting_ExportPath = "Assets/ExportRes/3DSDF";
            m_maxVolumeSDFTexSize = VolumeSDFTexSize.Size64;
            m_selectMesh = null;
            //
            InitVoxelizerUserSetting();
        }
        #endregion

        #region ----GUI Constants----
        private const float k_GUI_WindowWidth = 300;
        private const float k_GUI_WindowHeight = 240;
        private const float k_GUI_EditPanel_ScrollViewWidth = 290;
        #endregion

        #region ----Generate Volume SDF----
        private void GenerateAndExportVolumeSDF()
        {
            RTHandle voxelTex = null;
            RTHandle sdfTex = null;
            //TODO:提供其他体素化的方案
            ConvertMeshToVoxelRasterization(ref voxelTex);
            //
            //TODO:提供其它SDF的方案
            ConvertVoxelToSDFJFA(ref voxelTex, ref sdfTex);
            //
            //TODO:可选格式
            //TODO:mipmap, 这理论上得对Voxel的mipmap生成SDF, 而不是直接对SDF做mipmap
            //TODO:封装导出3D纹理的功能(包括可选格式、是否生成mipmap等)
            int width = voxelTex.Width;
            int height = voxelTex.Height;
            int volumeDepth = voxelTex.VolumeDepth;
            Texture3D tex3D = null;
            Rect intRect = new Rect(0, 0, width, height);
            Texture2D intermediateTex2D = new Texture2D(width, height, TextureFormat.RFloat, false);
            RTHandle intermediateHandle = new RTHandle(width, height, 0, GraphicsFormat.R32_SFloat);
            EditGraphicsUtility.AllocateTexture3D(ref tex3D, width, height, volumeDepth, TextureFormat.RFloat);
            for (int i = 0; i < volumeDepth; i++)
            {
                Graphics.CopyTexture(sdfTex, i, intermediateHandle, 0);
                //
                RenderTexture.active = intermediateHandle;
                intermediateTex2D.ReadPixels(intRect, 0, 0);
                Graphics.CopyTexture(intermediateTex2D, 0, tex3D, i);
                tex3D.Apply(false);
            }
            RenderTexture.active = null;
            //
            string fileName = string.IsNullOrEmpty(m_setting_ExportFileName) ? "VolumeSDF" : m_setting_ExportFileName;
            string exportTexPath = m_setting_ExportPath + "/" + fileName + ".asset";
            string fullPath = Path.GetDirectoryName(Path.GetFullPath(exportTexPath)).Replace('/', '\\');
            System.IO.Directory.CreateDirectory(fullPath);
            AssetDatabase.CreateAsset(tex3D, exportTexPath);
            AssetDatabase.Refresh();
            //释放资源
            voxelTex.Release();
            sdfTex.Release();
            tex3D = null;
            intermediateHandle.Release();
            DestroyImmediate(intermediateTex2D);
#pragma warning disable IDE0059
            intermediateTex2D = null;
#pragma warning restore IDE0059
            EditorUtility.UnloadUnusedAssetsImmediate();
            //
            s_disableOperateParameters = false;
            //
            Debug.Log("导出至:" + exportTexPath);
        }
        #endregion

        #region ----Setting GUI----
        private void DrawSetting()
        {
            GUILayout.Space(5);
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition, GUILayout.Width(k_GUI_EditPanel_ScrollViewWidth));
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginDisabledGroup(s_disableOperateParameters);
            //
            m_selectMesh = EditorGUILayout.ObjectField("目标网格", m_selectMesh, typeof(Mesh), true) as Mesh;
            GUILayout.Space(5);
            m_maxVolumeSDFTexSize = (VolumeSDFTexSize)EditorGUILayout.EnumPopup("SDF Tex Max Size:", m_maxVolumeSDFTexSize);
            //
            {
                //Voxel User Setting
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Space(5);
                m_voxelizerSetting_AllowUserChange = EditorGUILayout.Toggle("设置Voxelizer参数:", m_voxelizerSetting_AllowUserChange);
                if (m_voxelizerSetting_AllowUserChange)
                {
                    m_voxelizerSetting_BBExtent.x = EditorGUILayout.Slider("包围盒延伸X:", m_voxelizerSetting_BBExtent.x, 0, 2);
                    m_voxelizerSetting_BBExtent.y = EditorGUILayout.Slider("包围盒延伸Y:", m_voxelizerSetting_BBExtent.y, 0, 2);
                    m_voxelizerSetting_BBExtent.z = EditorGUILayout.Slider("包围盒延伸Z:", m_voxelizerSetting_BBExtent.z, 0, 2);
                    //TODO:延伸的值小于一定值做警告或者直接限制
                }
                GUILayout.Space(5);
                EditorGUILayout.EndVertical();
            }
            //
            {
                bool noMeshSelected = m_selectMesh == null;
                EditorGUI.BeginDisabledGroup(noMeshSelected);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Space(5);
                //
                EditorGUILayout.Space(5);
                m_setting_ExportFileName = EditorGUILayout.TextField("导出纹理名:", m_setting_ExportFileName);
                m_setting_ExportPath = EditorGUILayout.TextField("纹理导出路径:", m_setting_ExportPath);
                EditorGUILayout.Space(5);
                if (GUILayout.Button(new GUIContent("生成并导出纹理")))
                {
                    //TODO:进度条 https://docs.unity3d.com/ScriptReference/EditorUtility.DisplayProgressBar.html
                    s_disableOperateParameters = true;
                    GenerateAndExportVolumeSDF();
                }
                //
                GUILayout.Space(5);
                EditorGUILayout.EndVertical();
                EditorGUI.EndDisabledGroup();
            }
            //
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        #endregion

        #region ----Unity----
        private void OnEnable()
        {
            SetupMaterials();
            InitComputeShaders();
            SetInitProperties();
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            DrawSetting();
            GUILayout.EndHorizontal();
        }

        private void OnDisable()
        {
            ReleaseMaterials();
        }
        #endregion
    }
#endif
}