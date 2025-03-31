using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace ACloud.Editor
{
#if UNITY_EDITOR
    public class Noise3DEditWindow : EditorWindow
    {
        #region ----Command----
        public static void InitWindow()
        {
            CurrentWin = GetWindowWithRect<Noise3DEditWindow>(new Rect(0, 0, k_GUI_WindowWidth, k_GUI_WindowHeight));
            CurrentWin.titleContent = new GUIContent("Noise3DGenerator", "生成未压缩的3D纹理");
            CurrentWin.Focus();
            //
            s_disableOperateParameters = false;
        }

        [MenuItem("ACloud/Noise/Noise3DGenerator")]
        public static void OpenWindowFromMenu()
        {
            InitWindow();
        }
        #endregion

        #region ----Static Properties----
        public static Noise3DEditWindow CurrentWin { get; private set; }

        private static bool s_disableOperateParameters;
        #endregion

        #region ----GUI Properties----
        private string m_setting_ExportFileName;
        private string m_setting_ExportPath;

        private Vector2 m_scrollPosition;

        private bool m_isNeedRepaint;
        private bool m_isNeedMipmap;

        private NoisePreviewChannel m_previewChannel;
        private int m_previewVolumeDepth;

        private NoiseTexSize m_baseSetting_Size_Width;
        private NoiseTexSize m_baseSetting_Size_Height;
        private NoiseTexSize m_baseSetting_Size_VolumeDepth;
        private Noise3DMethods m_baseSetting_Method_ChannelX;
        private Noise3DMethods m_baseSetting_Method_ChannelY;
        private Noise3DMethods m_baseSetting_Method_ChannelZ;
        private Noise3DMethods m_baseSetting_Method_ChannelW;

        private Vector4 m_ChannelX_NoiseParam0;
        private Vector4 m_ChannelX_NoiseParam1;
        private Vector4 m_ChannelX_NoiseParam2;
        private Vector4 m_ChannelY_NoiseParam0;
        private Vector4 m_ChannelY_NoiseParam1;
        private Vector4 m_ChannelY_NoiseParam2;
        private Vector4 m_ChannelZ_NoiseParam0;
        private Vector4 m_ChannelZ_NoiseParam1;
        private Vector4 m_ChannelZ_NoiseParam2;
        private Vector4 m_ChannelW_NoiseParam0;
        private Vector4 m_ChannelW_NoiseParam1;
        private Vector4 m_ChannelW_NoiseParam2;

        private void SetInitProperties()
        {
            m_setting_ExportPath = "Assets/ExportRes/Noise3D";
            m_isNeedRepaint = true;
            m_isNeedMipmap = false;
            m_previewChannel = NoisePreviewChannel.X;
            //
            m_baseSetting_Size_Width = NoiseTexSize.Size128;
            m_baseSetting_Size_Height = NoiseTexSize.Size128;
            m_baseSetting_Size_VolumeDepth = NoiseTexSize.Size128;
            //
            m_baseSetting_Method_ChannelX = Noise3DMethods.PerlinWorley;
            m_baseSetting_Method_ChannelY = Noise3DMethods.Worley;
            m_baseSetting_Method_ChannelZ = Noise3DMethods.Worley;
            m_baseSetting_Method_ChannelW = Noise3DMethods.Worley;
            //
            m_ChannelX_NoiseParam0 = new Vector4(1, 0, 0, 0);
            m_ChannelX_NoiseParam1 = new Vector4(1, 0, 0, 0);
            m_ChannelX_NoiseParam2 = new Vector4(10, 4, 4, 0.5f);
            m_ChannelY_NoiseParam0 = new Vector4(1, 0, 0, 0);
            m_ChannelY_NoiseParam1 = new Vector4(1, 0, 0, 0);
            m_ChannelY_NoiseParam2 = new Vector4(10, 4, 4, 0.5f);
            m_ChannelZ_NoiseParam0 = new Vector4(1, 0, 0, 0);
            m_ChannelZ_NoiseParam1 = new Vector4(1, 0, 0, 0);
            m_ChannelZ_NoiseParam2 = new Vector4(10, 4, 4, 0.5f);
            m_ChannelW_NoiseParam0 = new Vector4(1, 0, 0, 0);
            m_ChannelW_NoiseParam1 = new Vector4(1, 0, 0, 0);
            m_ChannelW_NoiseParam2 = new Vector4(10, 4, 4, 0.5f);
        }
        #endregion

        #region ----GUI Constants----
        private const float k_GUI_WindowWidth = 1020;
        private const float k_GUI_WindowHeight = 730;
        private const float k_GUI_EditPanel_ScrollViewWidth = 290;
        private const float k_GUI_PreviewTex_Width = 720;
        private const float k_GUI_PreviewTex_Height = 720;
        #endregion

        #region ----Compute Shader----
        private LocalKeyword m_channelX_Keyword;
        private LocalKeyword m_channelY_Keyword;
        private LocalKeyword m_channelZ_Keyword;
        private LocalKeyword m_channelW_Keyword;
        private ComputeShader m_noise3DGenComputeShader;

        private void InitNoiseGenerateComputeShader()
        {
            if (m_noise3DGenComputeShader == null)
            {
                m_noise3DGenComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ACloud/Shader/Editor/NoiseGen/Noise3DGenerateCS.compute");
            }
            //
            m_channelX_Keyword = new LocalKeyword(m_noise3DGenComputeShader, "CHANNEL_X");
            m_channelY_Keyword = new LocalKeyword(m_noise3DGenComputeShader, "CHANNEL_Y");
            m_channelZ_Keyword = new LocalKeyword(m_noise3DGenComputeShader, "CHANNEL_Z");
            m_channelW_Keyword = new LocalKeyword(m_noise3DGenComputeShader, "CHANNEL_W");
        }
        #endregion

        #region ----Material----
        private Material m_previewMat;

        private void SetupMaterials()
        {
            m_previewMat = new Material(Shader.Find("ACloud/Editor/Noise3DPreview"));
        }

        private void ReleaseMaterials()
        {
            if (m_previewMat != null)
            {
                DestroyImmediate(m_previewMat);
                m_previewMat = null;
            }
        }
        #endregion

        #region ----RTHandles----
        private RTHandle m_previewHandle;

        private void SetupPreviewHandle()
        {
            m_previewHandle = new RTHandle(1024, 1024, 0, GraphicsFormat.R8G8B8A8_UNorm);
        }

        private void ReleasePreviewHandle()
        {
            m_previewHandle?.Release();
            m_previewHandle = null;
        }
        #endregion

        #region ----Compute Buffer----
        private ComputeBuffer m_perlinNoise3DParamsBuffer;

        private void SetupComputeBuffer()
        {
            GraphicsUtility.AllocateComputeBuffer(ref m_perlinNoise3DParamsBuffer, EditGraphicsUtility.PerlinNoiseDataArray.Length, sizeof(int));
            m_perlinNoise3DParamsBuffer.SetData(EditGraphicsUtility.PerlinNoiseDataArray);
        }

        private void ReleaseComputeBuffer()
        {
            m_perlinNoise3DParamsBuffer?.Release();
            m_perlinNoise3DParamsBuffer = null;
        }
        #endregion

        #region ----Material Properties GUI----
        private void DrawPerlinWorleyMaterialProperties(ref Vector4 noiseParam0, ref Vector4 noiseParam1, ref Vector4 noiseParam2)
        {
            noiseParam0 = EditorGUILayout.Vector4Field(new GUIContent("参数0:", "Perlin Noise的x: scale, yzw: bias"), noiseParam0);
            noiseParam1 = EditorGUILayout.Vector4Field(new GUIContent("参数1:", "Worley Noise的x: scale, yzw: bias"), noiseParam1);
            noiseParam2.x = EditorGUILayout.FloatField(new GUIContent("参数2:", "Worley Noise的频率"), noiseParam2.x);
            noiseParam2.y = EditorGUILayout.FloatField(new GUIContent("参数3:", "Perlin Noise的频率"), noiseParam2.y);
            noiseParam2.z = (int)EditorGUILayout.IntSlider(new GUIContent("参数4:", "Perlin Noise的迭代次数"), Mathf.CeilToInt(noiseParam2.z), 1, 32);
            noiseParam2.w = EditorGUILayout.Slider(new GUIContent("参数5:", "Perlin Noise的[-1, 1]区间内的Remap"), noiseParam2.w, 0f, 1f);
        }

        private void DrawWorleyMaterialProperties(ref Vector4 noiseParam0, ref Vector4 noiseParam1)
        {
            noiseParam0 = EditorGUILayout.Vector4Field(new GUIContent("参数0:", "x: scale, yzw: bias"), noiseParam0);
            noiseParam1.x = EditorGUILayout.FloatField(new GUIContent("参数1:", "频率"), noiseParam1.x);
        }
        #endregion

        #region ----Setting GUI----
        private void ChannelMethodSetting(string channel, ref Noise3DMethods method)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            method = (Noise3DMethods)EditorGUILayout.EnumPopup(new GUIContent("通道" + channel + "的噪音方法:"), method);
            //
            EditorGUILayout.EndVertical();
        }

        private void ChannelSetting(string channel, ref Noise3DMethods method, ref Vector4 noiseParam0, ref Vector4 noiseParam1, ref Vector4 noiseParam2)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            ChannelMethodSetting(channel, ref method);//TODO:用BeginChangeCheck()检查是否更改方法，然后给设置合适的初始值
            //绘制material properties gui
            switch (method)
            {
                case Noise3DMethods.PerlinWorley:
                    DrawPerlinWorleyMaterialProperties(ref noiseParam0, ref noiseParam1, ref noiseParam2);
                    break;
                case Noise3DMethods.Worley:
                    DrawWorleyMaterialProperties(ref noiseParam0, ref noiseParam1);
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSetting()
        {
            GUILayout.Space(5);
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition, GUILayout.Width(k_GUI_EditPanel_ScrollViewWidth));
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginDisabledGroup(s_disableOperateParameters);
            //
            EditorGUI.BeginChangeCheck();
            //纹理尺寸
            m_baseSetting_Size_Width = (NoiseTexSize)EditorGUILayout.EnumPopup("3D纹理X:", m_baseSetting_Size_Width);
            m_baseSetting_Size_Height = (NoiseTexSize)EditorGUILayout.EnumPopup("3D纹理Y:", m_baseSetting_Size_Height);
            m_baseSetting_Size_VolumeDepth = (NoiseTexSize)EditorGUILayout.EnumPopup("3D纹理Z:", m_baseSetting_Size_VolumeDepth);
            //
            GUILayout.Space(5f);
            //
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            ChannelSetting("X", ref m_baseSetting_Method_ChannelX, ref m_ChannelX_NoiseParam0, ref m_ChannelX_NoiseParam1, ref m_ChannelX_NoiseParam2);
            ChannelSetting("Y", ref m_baseSetting_Method_ChannelY, ref m_ChannelY_NoiseParam0, ref m_ChannelY_NoiseParam1, ref m_ChannelY_NoiseParam2);
            ChannelSetting("Z", ref m_baseSetting_Method_ChannelZ, ref m_ChannelZ_NoiseParam0, ref m_ChannelZ_NoiseParam1, ref m_ChannelZ_NoiseParam2);
            ChannelSetting("W", ref m_baseSetting_Method_ChannelW, ref m_ChannelW_NoiseParam0, ref m_ChannelW_NoiseParam1, ref m_ChannelW_NoiseParam2);
            EditorGUILayout.EndVertical();
            //
            //预览参数: 通道, 深度
            m_previewChannel = (NoisePreviewChannel)EditorGUILayout.EnumPopup("预览通道:", m_previewChannel);
            m_previewVolumeDepth = EditorGUILayout.IntSlider("深度:", m_previewVolumeDepth, 0, (int)m_baseSetting_Size_VolumeDepth - 1);
            //
            if (EditorGUI.EndChangeCheck())
            {
                m_isNeedRepaint = true;
            }
            EditorGUILayout.Space(5);
            m_setting_ExportFileName = EditorGUILayout.TextField("导出纹理名:", m_setting_ExportFileName);
            m_setting_ExportPath = EditorGUILayout.TextField("纹理导出路径:", m_setting_ExportPath);
            EditorGUILayout.Space(5);
            m_isNeedMipmap = EditorGUILayout.Toggle("生成Mipmaps:", m_isNeedMipmap);
            EditorGUILayout.Space(5);
            if (GUILayout.Button(new GUIContent("生成并导出纹理")))
            {
                //TODO:进度条 https://docs.unity3d.com/ScriptReference/EditorUtility.DisplayProgressBar.html
                s_disableOperateParameters = true;
                GenerateAndExportNoiseTex(m_noise3DGenComputeShader);
            }
            //
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        #endregion

        #region ----Shader Helper----
        private readonly static int k_shaderProperty_Float_NoiseVolumeDepth = Shader.PropertyToID("_NoiseVolumeDepth");
        private readonly static int k_shaderProperty_Vec_NoiseTexInvSize = Shader.PropertyToID("_NoiseTexInvSize");
        private readonly static int k_shaderProperty_Vec_NoiseParameter0 = Shader.PropertyToID("_NoiseParameter0");
        private readonly static int k_shaderProperty_Vec_NoiseParameter1 = Shader.PropertyToID("_NoiseParameter1");
        private readonly static int k_shaderProperty_Vec_NoiseParameter2 = Shader.PropertyToID("_NoiseParameter2");
        private readonly static int k_shaderProperty_Buffer_EW_Buffer_PerlinNoise3DParams = Shader.PropertyToID("_EW_Buffer_PerlinNoise3DParams");
        private readonly static int k_shaderProperty_Tex_RW_NoiseTex3D = Shader.PropertyToID("RW_NoiseTex3D");

        private const string k_shaderKernelName_GeneratePerlinWorleyNoiseKernel = "GeneratePerlinWorleyNoiseKernel";
        private const string k_shaderKernelName_GenerateWorleyNoiseKernel = "GenerateWorleyNoiseKernel";
        #endregion

        #region ----Generate Noise3D Tex----
        private void ExecuteNoiseGenerateKernel(ref CommandBuffer cmd, string kernelName, LocalKeyword keyword, ComputeShader cs, RTHandle handle, Vector4 noiseParam0, Vector4 noiseParam1, Vector4 noiseParam2, Vector3 invSize)
        {
            cmd.EnableKeyword(cs, keyword);
            int kernelIndex = cs.FindKernel(kernelName);
            cmd.SetRenderTarget(handle);
            cmd.SetComputeVectorParam(cs, k_shaderProperty_Vec_NoiseParameter0, noiseParam0);
            cmd.SetComputeVectorParam(cs, k_shaderProperty_Vec_NoiseParameter1, noiseParam1);
            cmd.SetComputeVectorParam(cs, k_shaderProperty_Vec_NoiseParameter2, noiseParam2);
            cmd.SetComputeVectorParam(cs, k_shaderProperty_Vec_NoiseTexInvSize, invSize);
            cmd.SetComputeBufferParam(cs, kernelIndex, k_shaderProperty_Buffer_EW_Buffer_PerlinNoise3DParams, m_perlinNoise3DParamsBuffer);
            cmd.SetComputeTextureParam(cs, kernelIndex, k_shaderProperty_Tex_RW_NoiseTex3D, handle);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(handle.Width / 4f), Mathf.CeilToInt(handle.Height / 4f), Mathf.CeilToInt(handle.VolumeDepth / 4f));
            cmd.DisableKeyword(cs, keyword);
        }

        private void GenerateAndExportNoiseTex(ComputeShader cs)
        {
            int width = (int)m_baseSetting_Size_Width;
            int height = (int)m_baseSetting_Size_Height;
            int volumeDepth = (int)m_baseSetting_Size_VolumeDepth;
            int mipmapCount = m_isNeedMipmap ? EditGraphicsUtility.CalculateMipmapCount(width, height) : 0;
            RTHandle noise3DHandle = new RTHandle(new Vector3Int(width, height, volumeDepth), GraphicsFormat.R8G8B8A8_UNorm, 0, true);
            Vector3 invTexSize = new Vector3(1f / (width - 1), 1f / (height - 1), 1f / (volumeDepth - 1));
            CommandBuffer noiseGenCmdBuffer = new CommandBuffer()
            {
                name = "Noise 3D Generate Pass"
            };
            //
            {
                switch (m_baseSetting_Method_ChannelX)
                {
                    case Noise3DMethods.PerlinWorley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GeneratePerlinWorleyNoiseKernel, m_channelX_Keyword, cs, noise3DHandle, m_ChannelX_NoiseParam0, m_ChannelX_NoiseParam1, m_ChannelX_NoiseParam2, invTexSize);
                        break;
                    case Noise3DMethods.Worley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GenerateWorleyNoiseKernel, m_channelX_Keyword, cs, noise3DHandle, m_ChannelX_NoiseParam0, m_ChannelX_NoiseParam1, m_ChannelX_NoiseParam2, invTexSize);
                        break;
                }
            }
            {
                switch (m_baseSetting_Method_ChannelY)
                {
                    case Noise3DMethods.PerlinWorley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GeneratePerlinWorleyNoiseKernel, m_channelY_Keyword, cs, noise3DHandle, m_ChannelY_NoiseParam0, m_ChannelY_NoiseParam1, m_ChannelY_NoiseParam2, invTexSize);
                        break;
                    case Noise3DMethods.Worley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GenerateWorleyNoiseKernel, m_channelY_Keyword, cs, noise3DHandle, m_ChannelY_NoiseParam0, m_ChannelY_NoiseParam1, m_ChannelY_NoiseParam2, invTexSize);
                        break;
                }
            }
            {
                switch (m_baseSetting_Method_ChannelZ)
                {
                    case Noise3DMethods.PerlinWorley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GeneratePerlinWorleyNoiseKernel, m_channelZ_Keyword, cs, noise3DHandle, m_ChannelZ_NoiseParam0, m_ChannelZ_NoiseParam1, m_ChannelZ_NoiseParam2, invTexSize);
                        break;
                    case Noise3DMethods.Worley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GenerateWorleyNoiseKernel, m_channelZ_Keyword, cs, noise3DHandle, m_ChannelZ_NoiseParam0, m_ChannelZ_NoiseParam1, m_ChannelZ_NoiseParam2, invTexSize);
                        break;
                }
            }
            {
                switch (m_baseSetting_Method_ChannelW)
                {
                    case Noise3DMethods.PerlinWorley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GeneratePerlinWorleyNoiseKernel, m_channelW_Keyword, cs, noise3DHandle, m_ChannelW_NoiseParam0, m_ChannelW_NoiseParam1, m_ChannelW_NoiseParam2, invTexSize);
                        break;
                    case Noise3DMethods.Worley:
                        ExecuteNoiseGenerateKernel(ref noiseGenCmdBuffer, k_shaderKernelName_GenerateWorleyNoiseKernel, m_channelW_Keyword, cs, noise3DHandle, m_ChannelW_NoiseParam0, m_ChannelW_NoiseParam1, m_ChannelW_NoiseParam2, invTexSize);
                        break;
                }
            }
            //
            Graphics.ExecuteCommandBuffer(noiseGenCmdBuffer);
            //
            Texture3D tex3D = null;
            Rect intRect = new Rect(0, 0, width, height);
            Texture2D intermediateTex2D;
            if (m_isNeedMipmap)
            {
                intermediateTex2D = new Texture2D(width, height, TextureFormat.RGBA32, mipmapCount, false);
            }
            else
            {
                intermediateTex2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }
            RTHandle intermediateHandle = new RTHandle(width, height, 0, GraphicsFormat.R8G8B8A8_UNorm);
            EditGraphicsUtility.AllocateTexture3D(ref tex3D, width, height, volumeDepth, TextureFormat.RGBA32, m_isNeedMipmap, true, mipmapCount);
            //需要注意的是, Unity并未提供RenderTexture同步回读的接口(CopyTexture()对rt到tex3d的作用是不会回读到cpu端的)
            //如果要回读只能异步, 然后用EditorCoroutines这个包来执行协程(Editor下需要用这个)
            //我们准备利用媒介来同步回读, 利用媒介的一个好处是我们还可以很方便的输出压缩格式的纹理(这里我们没有这么做)
            //TODO: 用异步的方法来回读、压缩、生成mipmap
            for (int i = 0; i < volumeDepth; i++)
            {
                Graphics.CopyTexture(noise3DHandle, i, intermediateHandle, 0);
                //
                RenderTexture.active = intermediateHandle;
                if (m_isNeedMipmap)
                {
                    intermediateTex2D.ReadPixels(intRect, 0, 0, m_isNeedMipmap);
                    //有需要的话, 在这里用EditorUtility.CompressTexture()来压缩格式, 等拷贝完再重新初始化
                    for (int j = 0; j < mipmapCount; j++)
                    {
                        Graphics.CopyTexture(intermediateTex2D, 0, j, tex3D, i, j);
                    }
                    tex3D.Apply();
                }
                else
                {
                    intermediateTex2D.ReadPixels(intRect, 0, 0);
                    Graphics.CopyTexture(intermediateTex2D, 0, tex3D, i);
                    tex3D.Apply();
                }
            }
            RenderTexture.active = null;
            //
            string fileName = string.IsNullOrEmpty(m_setting_ExportFileName) ? "Noise3D" : m_setting_ExportFileName;
            string exportTexPath = m_setting_ExportPath + "/" + fileName + ".asset";
            string fullPath = Path.GetDirectoryName(Path.GetFullPath(exportTexPath)).Replace('/', '\\');
            System.IO.Directory.CreateDirectory(fullPath);
            AssetDatabase.CreateAsset(tex3D, exportTexPath);
            AssetDatabase.Refresh();
            //
            noise3DHandle.Release();
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

        #region ----Preview Rendeing----
        private void RenderingPreviewHandle(Noise3DMethods method, Vector4 noiseParam0, Vector4 noiseParam1, Vector4 noiseParam2)
        {
            CommandBuffer previewCmdBuffer = new CommandBuffer()
            {
                name = "Noise 3D Preview Pass"
            };
            previewCmdBuffer.SetRenderTarget(m_previewHandle);
            m_previewMat.SetFloat(k_shaderProperty_Float_NoiseVolumeDepth, m_previewVolumeDepth / (float)((int)m_baseSetting_Size_VolumeDepth - 1));
            m_previewMat.SetVector(k_shaderProperty_Vec_NoiseParameter0, noiseParam0);
            m_previewMat.SetVector(k_shaderProperty_Vec_NoiseParameter1, noiseParam1);
            m_previewMat.SetVector(k_shaderProperty_Vec_NoiseParameter2, noiseParam2);
            m_previewMat.SetBuffer(k_shaderProperty_Buffer_EW_Buffer_PerlinNoise3DParams, m_perlinNoise3DParamsBuffer);
            switch (method)
            {
                case Noise3DMethods.PerlinWorley:
                    m_previewMat.EnableKeyword("METHOD_PERLINWORLEY");
                    m_previewMat.DisableKeyword("METHOD_WORLEY");
                    break;
                case Noise3DMethods.Worley:
                    m_previewMat.EnableKeyword("METHOD_WORLEY");
                    m_previewMat.DisableKeyword("METHOD_PERLINWORLEY");
                    break;
            }
            previewCmdBuffer.DrawProcedural(Matrix4x4.identity, m_previewMat, 0, MeshTopology.Triangles, 3, 1);
            Graphics.ExecuteCommandBuffer(previewCmdBuffer);
        }
        #endregion

        #region ----Unity----
        private void OnEnable()
        {
            InitNoiseGenerateComputeShader();
            SetupPreviewHandle();
            SetupComputeBuffer();
            SetInitProperties();
            SetupMaterials();
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            DrawSetting();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(k_GUI_PreviewTex_Width + 10), GUILayout.MaxHeight(k_GUI_PreviewTex_Height + 10));
            Rect sceneViewRect = GUILayoutUtility.GetRect(k_GUI_PreviewTex_Width, k_GUI_PreviewTex_Height, GUILayout.ExpandHeight(false), GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(sceneViewRect, m_previewHandle, null, ScaleMode.StretchToFill);
            EditorGUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (m_isNeedRepaint)
            {
                Repaint();
                //
                switch (m_previewChannel)
                {
                    case NoisePreviewChannel.X:
                        RenderingPreviewHandle(m_baseSetting_Method_ChannelX, m_ChannelX_NoiseParam0, m_ChannelX_NoiseParam1, m_ChannelX_NoiseParam2);
                        break;
                    case NoisePreviewChannel.Y:
                        RenderingPreviewHandle(m_baseSetting_Method_ChannelY, m_ChannelY_NoiseParam0, m_ChannelY_NoiseParam1, m_ChannelY_NoiseParam2);
                        break;
                    case NoisePreviewChannel.Z:
                        RenderingPreviewHandle(m_baseSetting_Method_ChannelZ, m_ChannelZ_NoiseParam0, m_ChannelZ_NoiseParam1, m_ChannelZ_NoiseParam2);
                        break;
                    case NoisePreviewChannel.W:
                        RenderingPreviewHandle(m_baseSetting_Method_ChannelW, m_ChannelW_NoiseParam0, m_ChannelW_NoiseParam1, m_ChannelW_NoiseParam2);
                        break;
                }
                //
                m_isNeedRepaint = false;
            }
        }

        private void OnDisable()
        {
            ReleasePreviewHandle();
            ReleaseComputeBuffer();
            ReleaseMaterials();
            m_noise3DGenComputeShader = null;
        }
        #endregion
    }
#endif
}