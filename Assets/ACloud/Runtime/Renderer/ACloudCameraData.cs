using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ACloud
{
    public partial class ACloudCamera : MonoBehaviour
    {
        #region ----Settings----
        public CloudRenderingSetting CloudSettings;
        public AtmosphereRenderingSetting AtmosphereSettings;
        #endregion

        #region ----Camera----
        private Camera m_renderCam;

        private void SetupRenderCam()
        {
            if (m_renderCam == null)
            {
                m_renderCam = GetComponent<Camera>();
                m_renderCam.allowHDR = false;
                m_renderCam.allowMSAA = false;
            }
        }
        #endregion

        #region ----Main Light----
        public Transform MainLightTransform;
        #endregion

        #region ----RT Handle----
        private RTHandle m_camBaseColor_Handle;//这里我们直接作为最终结果(demo演示作用)
        private RTHandle m_camNormal_Handle;//xyz:normal, w:smoothness(用于是否需要反射)
        private RTHandle m_camPreNormal_Handle;
        private RTHandle m_camDepth_Handle;
        private RTHandle m_cloud_Handle;//xyz:color, w:density

        private RTHandle m_atmosphere_Handle;//因为依赖于场景中对象序列化下来的AtmosphereSettings中的设置, 因此我们不在SetupRTHandles()中初始化(需要在Start及之后才能得到AtmosphereSettings反序列化后的设置值), 如果在之前的场景中就以及反序列化了用户设置则不需要单独初始化
        private RTHandle m_atmosphereTemp_Handle;
        //TODO: God Ray

        private RenderBuffer[] m_camColorBuffer;

        private uint m_ScreenWidth;
        private uint m_ScreenHeight;

        private void SetupRTHandles()
        {
            Vector2Int _screenSize = new Vector2Int(m_renderCam.pixelWidth, m_renderCam.pixelHeight);
            m_ScreenWidth = (uint)_screenSize.x;
            m_ScreenHeight = (uint)_screenSize.y;
            //
            m_camBaseColor_Handle = new RTHandle(_screenSize.x, _screenSize.y, 0, GraphicsFormat.R8G8B8A8_UNorm);
            m_camNormal_Handle = new RTHandle(_screenSize.x, _screenSize.y, 0, GraphicsFormat.R16G16B16A16_SFloat);
            m_camPreNormal_Handle = new RTHandle(_screenSize.x, _screenSize.y, 0, GraphicsFormat.R16G16B16A16_SFloat);
            m_camDepth_Handle = new RTHandle(_screenSize.x, _screenSize.y, GraphicsFormat.None, GraphicsFormat.D32_SFloat);
            m_cloud_Handle = new RTHandle(_screenSize.x, _screenSize.y, 0, GraphicsFormat.R32G32B32A32_SFloat, 0, true);
            //
            m_camColorBuffer = new RenderBuffer[] { m_camBaseColor_Handle.ColorBuffer, m_camNormal_Handle.ColorBuffer };
        }

        private void ReleaseRTHandles()
        {
            m_camBaseColor_Handle?.Release();
            m_camBaseColor_Handle = null;
            m_camNormal_Handle?.Release();
            m_camNormal_Handle = null;
            m_camPreNormal_Handle?.Release();
            m_camPreNormal_Handle = null;
            m_camDepth_Handle?.Release();
            m_camDepth_Handle = null;
            m_cloud_Handle?.Release();
            m_cloud_Handle = null;
            //
            m_atmosphere_Handle?.Release();
            m_atmosphere_Handle = null;
            m_atmosphereTemp_Handle?.Release();
            m_atmosphereTemp_Handle = null;
        }
        #endregion

        #region ----Rendering Res----
        public RayTracingShader CloudRayGenShader;
        public ComputeShader CloudDataManagementComputeShader;
        #endregion

        #region ----Material-----
        private Material m_Mat_ACloud;
        private Material m_Mat_Atmosphere;

        private void SetupMaterials()
        {
            m_Mat_ACloud = new Material(Shader.Find("ACloud/Sky/ACloud"));
            m_Mat_Atmosphere = new Material(Shader.Find("ACloud/Sky/Atmosphere"));
        }

        private void ReleaseMaterials()
        {
            if (m_Mat_ACloud != null)
            {
                Destroy(m_Mat_ACloud);
            }
            if (m_Mat_Atmosphere != null)
            {
                Destroy(m_Mat_Atmosphere);
            }
        }
        #endregion

        #region ----Buffers----
        private AABB[] m_cloudAABB_Array;
        private uint[] m_cloudType_Array;

        private GraphicsBuffer m_cloudAABB_GraphicsBuffer;//前面的1024个是custom的，之后的是都是procedural的
        private ComputeBuffer m_cloudProceduralAABB_ComputeBuffer;
        private ComputeBuffer m_cloudCustomDataField_ComputeBuffer;
        private ComputeBuffer m_cloudDataField_ComputeBuffer;
        private ComputeBuffer m_cloudType_ComputeBuffer;//procedural的类型都是云，值为默认值0
        private ComputeBuffer m_cloudShapeBundle_ComputeBuffer;

        private void SetupBuffers()
        {
            GraphicsUtility.AllocateGraphicsBuffer(ref m_cloudAABB_GraphicsBuffer, CloudConfig.CLOUD_COUNT, Marshal.SizeOf(typeof(AABB)));
            GraphicsUtility.AllocateComputeBuffer(ref m_cloudProceduralAABB_ComputeBuffer, CloudConfig.CLOUD_PROCEDURAL_COUNT, Marshal.SizeOf(typeof(AABB)));
            GraphicsUtility.AllocateComputeBuffer(ref m_cloudCustomDataField_ComputeBuffer, CloudConfig.CLOUD_CUSTOM_COUNT, Marshal.SizeOf(typeof(CloudDataField)));
            GraphicsUtility.AllocateComputeBuffer(ref m_cloudDataField_ComputeBuffer, CloudConfig.CLOUD_COUNT, Marshal.SizeOf(typeof(CloudDataField)));
            GraphicsUtility.AllocateComputeBuffer(ref m_cloudType_ComputeBuffer, CloudConfig.CLOUD_COUNT, sizeof(uint));
            GraphicsUtility.AllocateComputeBuffer(ref m_cloudShapeBundle_ComputeBuffer, CloudManager.Instance.CloudShapeBundle.ShapeCount, Marshal.SizeOf(typeof(Vector2Int)));
            //
            m_cloudAABB_Array = new AABB[CloudConfig.CLOUD_COUNT];
            m_cloudType_Array = new uint[CloudConfig.CLOUD_COUNT];
            //
            m_cloudAABB_GraphicsBuffer.SetData(m_cloudAABB_Array);//我们demo里没有预加载，这里保险点用trivial的初始值
            m_cloudProceduralAABB_ComputeBuffer.SetData(new AABB[CloudConfig.CLOUD_PROCEDURAL_COUNT]);
            m_cloudType_ComputeBuffer.SetData(m_cloudType_Array);
            m_cloudShapeBundle_ComputeBuffer.SetData(CloudManager.Instance.CloudShapeBundle.GetCloudShapeDatas());
        }

        private void ReleaseBuffers()
        {
            m_cloudAABB_GraphicsBuffer?.Release();
            m_cloudAABB_GraphicsBuffer = null;
            m_cloudProceduralAABB_ComputeBuffer?.Release();
            m_cloudProceduralAABB_ComputeBuffer = null;
            m_cloudCustomDataField_ComputeBuffer?.Release();
            m_cloudCustomDataField_ComputeBuffer = null;
            m_cloudDataField_ComputeBuffer?.Release();
            m_cloudDataField_ComputeBuffer = null;
            m_cloudType_ComputeBuffer?.Release();
            m_cloudType_ComputeBuffer = null;
            m_cloudShapeBundle_ComputeBuffer?.Release();
            m_cloudShapeBundle_ComputeBuffer = null;
        }
        #endregion

        #region ----RT Acc Struct----
        private RayTracingAccelerationStructure m_RTAccStruct;

        private void SetupRayTracingAccelStruct()
        {
            RayTracingAccelerationStructure.RASSettings _settings = new RayTracingAccelerationStructure.RASSettings(RayTracingAccelerationStructure.ManagementMode.Manual, RayTracingAccelerationStructure.RayTracingModeMask.Everything, ~0);
            m_RTAccStruct = new RayTracingAccelerationStructure(_settings);
            SetupBuffers();
            MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
            //
            m_RTAccStruct.AddInstance(m_cloudAABB_GraphicsBuffer, CloudConfig.CLOUD_COUNT, true, Matrix4x4.identity, m_Mat_ACloud, false, matPropertyBlock);
            m_RTAccStruct.Build();
        }

        private void ReleaseRayTracingAccelStruct()
        {
            m_RTAccStruct?.Release();
            m_RTAccStruct = null;
        }
        #endregion
    }
}