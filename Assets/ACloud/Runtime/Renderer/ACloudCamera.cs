using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ACloud
{
    public partial class ACloudCamera : MonoBehaviour
    {
        #region ----Cloud Control----
        private void ExecuteCopyCustomCloudDataFieldsKernel(ComputeShader cs)
        {
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "Copy Custom Cloud Data Fields Pass";
            int kernelIndex = cs.FindKernel(k_shaderKernel_CopyCustomCloudDataFieldsKernel);
            cs.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
            cmd.SetComputeBufferParam(cs, kernelIndex, k_shaderProperty_Buffer_ACloudTracingCustomDataFieldBuffer, m_cloudCustomDataField_ComputeBuffer);
            cmd.SetComputeBufferParam(cs, kernelIndex, k_shaderProperty_Buffer_RW_ACloudTracingDataFieldBuffer, m_cloudDataField_ComputeBuffer);
            cmd.DispatchCompute(cs, kernelIndex, Mathf.CeilToInt(CloudConfig.CLOUD_CUSTOM_COUNT / (float)x), 1, 1);
            Graphics.ExecuteCommandBuffer(cmd);
        }


        IEnumerator AsyncUpdateCloudAABB()
        {
            yield return null;
            while (true)
            {
                AsyncGPUReadbackRequest proceduralAABBReadBackRequest = AsyncGPUReadback.Request(m_cloudProceduralAABB_ComputeBuffer);
                yield return new WaitWhile(() => !proceduralAABBReadBackRequest.done);
                //
                {
                    AABB[] proceduralAABBArray = proceduralAABBReadBackRequest.GetData<AABB>().ToArray();
                    //
                    AABB[] customAABBArray = CloudManager.Instance.GetCustomCloudAABBData();
                    //
                    Array.Copy(customAABBArray, 0, m_cloudAABB_Array, 0, CloudConfig.CLOUD_CUSTOM_COUNT);
                    Array.Copy(proceduralAABBArray, 0, m_cloudAABB_Array, CloudConfig.CLOUD_CUSTOM_COUNT, CloudConfig.CLOUD_PROCEDURAL_COUNT);
                }
                {
                    uint[] customTypeArray = CloudManager.Instance.GetCustomCloudTypes();
                    Array.Copy(customTypeArray, 0, m_cloudType_Array, 0, CloudConfig.CLOUD_CUSTOM_COUNT);
                }
                //
                m_cloudAABB_GraphicsBuffer.SetData(m_cloudAABB_Array);
                m_cloudType_ComputeBuffer.SetData(m_cloudType_Array);
                m_RTAccStruct.Build();
                //yield return null;
            }
        }

        IEnumerator UpdateCustomCloudDataFields()
        {
            yield return null;
            while (true)
            {
                if (CloudManager.Instance.HasCustomCloudDataChanged())
                {
                    m_cloudCustomDataField_ComputeBuffer.SetData(CloudManager.Instance.GetCustomCloudDataFields());
                    //
                    ExecuteCopyCustomCloudDataFieldsKernel(CloudDataManagementComputeShader);
                }
                yield return new WaitForFixedUpdate();//自行设定更新频率
            }
        }

        #endregion

        #region ----Atmosphere Rendering----
        
        private Coroutine m_AtmosphereIterativeRenderingCoroutine;

        private void SetupAtmosphereRenderingParams(ref float relativeHeight, ref float heightScaleValue, ref float atmosphericThickness, ref float planetRadius, ref float sunLightIntensity, ref Vector3 sunLightDir, ref Vector3 rayleighScatterParam)
        {
            relativeHeight = Mathf.Max(0, transform.position.y * 0.001f);
            heightScaleValue = AtmosphereSettings.HeightScaleValue;
            atmosphericThickness = AtmosphereSettings.AtmosphericThickness;
            planetRadius  = AtmosphereSettings.PlanetRadius;
            sunLightIntensity = AtmosphereSettings.SunLightIntensity;
            if (MainLightTransform != null)
            {
                sunLightDir = -MainLightTransform.forward;
            }
            else
            {
                sunLightDir = Vector3.up;
            }
            rayleighScatterParam = AtmosphereSettings.RayleighScatterParam;
        }

        private void SetGPUAtmosphereRenderingParams(ref CommandBuffer cmd, int iteration, float relativeHeight, float heightScaleValue, float atmosphericThickness, float planetRadius, float sunLightIntensity, Vector3 sunLightDir, Vector3 rayleighScatterParam)
        {
            cmd.SetGlobalInt(k_shaderProperty_Int_IterativeRenderingStep, iteration);
            cmd.SetGlobalFloat(k_shaderProperty_Float_CameraRelativeHeight, relativeHeight);
            cmd.SetGlobalFloat(k_shaderProperty_Float_HeightScaleValue, heightScaleValue);
            cmd.SetGlobalFloat(k_shaderProperty_Float_AtmosphericThickness, atmosphericThickness);
            cmd.SetGlobalFloat(k_shaderProperty_Float_PlanetRadius, planetRadius);
            cmd.SetGlobalFloat(k_shaderProperty_Float_MainLightIntensity, sunLightIntensity);
            cmd.SetGlobalVector(k_shaderProperty_Vec_SunLightDirection, sunLightDir);
            cmd.SetGlobalVector(k_shaderProperty_Vec_RayleighScatterParam, rayleighScatterParam);
        }

        private void ClearAtmosphereTempHandle(ref CommandBuffer cmd)
        {
            for (int i = 0; i < 6; i++)
            {
                cmd.SetRenderTarget(m_atmosphereTemp_Handle, 0, (CubemapFace)i);
                cmd.ClearRenderTarget(false, true, Color.clear);
            }
        }

        private void BlitAtmosphereHandle(ref CommandBuffer cmd)
        {
            for (int i = 0; i < 6; i++)
            {
                cmd.CopyTexture(m_atmosphereTemp_Handle, i, m_atmosphere_Handle, i);
            }
        }

        IEnumerator AtmosphereIterativeRendering()
        {
            Matrix4x4[] cubemapCam2WorldMatrixs = new Matrix4x4[6]
            {
                Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(Vector3.right, Vector3.up), Vector3.one),
                Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(Vector3.left, Vector3.up), Vector3.one),
                Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(Vector3.up, Vector3.back), Vector3.one),
                Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(Vector3.down, Vector3.forward), Vector3.one),
                Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up), Vector3.one),
                Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(Vector3.back, Vector3.up), Vector3.one)
            };
            int totalIteration;
            yield return null;//在这里是必要的
            AtmosphereQuality quality = AtmosphereSettings.Quality;//运行时不允许修改
            switch (quality)
            {
                case AtmosphereQuality.High:
                    m_atmosphere_Handle = new RTHandle(512, GraphicsFormat.R16G16B16A16_SFloat);
                    m_atmosphereTemp_Handle = new RTHandle(512, GraphicsFormat.R16G16B16A16_SFloat);
                    totalIteration = 32;
                    break;
                case AtmosphereQuality.Normal:
                    //GraphicsFormat.B10G11R11_UFloatPack32格式的HDR纹理在没有Color Banding的情况下还比不上Low的结果
                    m_atmosphere_Handle = new RTHandle(256, GraphicsFormat.R16G16B16A16_SFloat);
                    m_atmosphereTemp_Handle = new RTHandle(256, GraphicsFormat.R16G16B16A16_SFloat);
                    totalIteration = 16;
                    break;
                case AtmosphereQuality.Low:
                    m_atmosphere_Handle = new RTHandle(128, GraphicsFormat.R8G8B8A8_UNorm);
                    m_atmosphereTemp_Handle = new RTHandle(128, GraphicsFormat.R8G8B8A8_UNorm);
                    totalIteration = 8;
                    break;
                default:
                    m_atmosphere_Handle = new RTHandle(512, GraphicsFormat.R16G16B16A16_SFloat);
                    m_atmosphereTemp_Handle = new RTHandle(512, GraphicsFormat.R16G16B16A16_SFloat);
                    totalIteration = 16;
                    break;
            }
            //
            float relativeHeight = 0;
            float heightScaleValue = 0;
            float atmosphericThickness = 0;
            float planetRadius = 0;
            float sunLightIntensity = 0;
            Vector3 sunLightDir = Vector3.zero;
            Vector3 rayleighScatterParam = Vector3.zero;
            SetupAtmosphereRenderingParams(ref relativeHeight, ref heightScaleValue, ref atmosphericThickness, ref planetRadius, ref sunLightIntensity, ref sunLightDir, ref rayleighScatterParam);
            //
            int iteration = 0;
            while (true)
            {
                CommandBuffer cmd = new CommandBuffer()
                {
                    name = "Atmosphere Iterative Rendering Pass"
                };
                if (iteration >= totalIteration)
                {
                    iteration = 0;
                    BlitAtmosphereHandle(ref cmd);
                    SetupAtmosphereRenderingParams(ref relativeHeight, ref heightScaleValue, ref atmosphericThickness, ref planetRadius, ref sunLightIntensity, ref sunLightDir, ref rayleighScatterParam);
                    //
                    ClearAtmosphereTempHandle(ref cmd);
                }
                //
                SetGPUAtmosphereRenderingParams(ref cmd, iteration, relativeHeight, heightScaleValue, atmosphericThickness, planetRadius, sunLightIntensity, sunLightDir, rayleighScatterParam);
                for (int i = 0; i < 6; i++)
                {
                    cmd.SetRenderTarget(m_atmosphereTemp_Handle, 0, (CubemapFace)i);
                    MaterialPropertyBlock matPropertyBlock = new MaterialPropertyBlock();
                    matPropertyBlock.SetMatrix(k_shaderProperty_Matrix_CubeFaceRenderingCam2WorldMatrix, cubemapCam2WorldMatrixs[i]);
                    cmd.DrawProcedural(Matrix4x4.identity, m_Mat_Atmosphere, (int)quality, MeshTopology.Triangles, 3, 1, matPropertyBlock);
                }
                Graphics.ExecuteCommandBuffer(cmd);
                //
                iteration++;
                yield return null;
            }
        }
        #endregion

        #region ----Cloud Rendering Pass----
        private const string k_RTCloudPassName = "RayTracedCloud";
        private const string k_RTCloudRayGenName = "RTCloudRayGeneration";
        private const CameraEvent k_CloudRenderingPassCamEvent = CameraEvent.BeforeForwardAlpha;
        private CommandBuffer m_CloudRenderingPassBuffer;

        private void ExecuteCloudTracingPass(ref CommandBuffer cmd, RayTracingShader rts)
        {
            cmd.SetRayTracingShaderPass(rts, k_RTCloudPassName);
            cmd.SetRenderTarget(m_cloud_Handle);
            cmd.SetRayTracingFloatParam(rts, k_shaderProperty_Float_CameraZoom, Mathf.Tan(Mathf.Deg2Rad * m_renderCam.fieldOfView * 0.5f));
            cmd.SetRayTracingMatrixParam(rts, k_shaderProperty_Matrix_CameraVPInvMatrix, GraphicsUtility.CameraVPInverseMatrix(m_renderCam, true, false));
            cmd.SetRayTracingAccelerationStructure(rts, k_shaderProperty_RTAccStruct_CloudsAccelStruct, m_RTAccStruct);
            cmd.SetRayTracingTextureParam(rts, k_shaderProperty_Tex_CameraDepthRT, m_camDepth_Handle);
            cmd.SetRayTracingTextureParam(rts, k_shaderProperty_Tex_CameraNormalRT, m_camPreNormal_Handle);//用前一帧的
            cmd.SetRayTracingTextureParam(rts, k_shaderProperty_Tex_RW_CloudColorDensityRT, m_cloud_Handle);
            //
            cmd.SetGlobalFloat(k_shaderProperty_Float_CloudSimulationTime, Time.time);
            cmd.SetGlobalFloat(k_shaderProperty_Float_CloudTracingNearPlane, CloudSettings.NearPlane);
            cmd.SetGlobalFloat(k_shaderProperty_Float_CloudTracingFarPlane, CloudSettings.FarPlane);
            cmd.SetGlobalFloat(k_shaderProperty_Float_CloudTracingLayerBottom, CloudSettings.LayerBottom);
            cmd.SetGlobalFloat(k_shaderProperty_Float_CloudTracingLayerTop, CloudSettings.LayerTop);
            cmd.SetGlobalVector(k_shaderProperty_Vec_MainLightDirection, -MainLightTransform.forward);
            cmd.SetGlobalVector(k_shaderProperty_Vec_CloudShapeBundleInvSize, CloudManager.Instance.ShapeBundleInvSize);
            cmd.SetGlobalVector(k_shaderProperty_Vec_CameraForward, transform.forward);
            cmd.SetGlobalBuffer(k_shaderProperty_Buffer_ACloudTracingDataFieldBuffer, m_cloudDataField_ComputeBuffer);
            cmd.SetGlobalBuffer(k_shaderProperty_Buffer_ACloudAABBBuffer, m_cloudAABB_GraphicsBuffer);
            cmd.SetGlobalBuffer(k_shaderProperty_Buffer_ACloudTypeBuffer, m_cloudType_ComputeBuffer);
            cmd.SetGlobalBuffer(k_shaderProperty_Buffer_ACloudShapeDataBuffer, m_cloudShapeBundle_ComputeBuffer);
            cmd.SetGlobalTexture(k_shaderProperty_Tex_CloudNoise3D_Tex, CloudManager.Instance.CloudShapeBundle.Noise3DTex);
            cmd.SetGlobalTexture(k_shaderProperty_Tex_CloudShapeBundle_Tex, CloudManager.Instance.CloudShapeBundle.SDFAtlasTex);
            if (m_atmosphere_Handle != null)
            {
                cmd.SetGlobalTexture(k_shaderProperty_Tex_AtmosphereCubeTex, m_atmosphere_Handle);
            }
            //
            cmd.DispatchRays(rts, k_RTCloudRayGenName, m_ScreenWidth, m_ScreenHeight, 1, m_renderCam);
        }

        private void ReleaseCloudRenderingPass()
        {
            if (m_renderCam != null)
            {
                if (m_CloudRenderingPassBuffer != null)
                {
                    m_renderCam.RemoveCommandBuffer(k_CloudRenderingPassCamEvent, m_CloudRenderingPassBuffer);
                    m_CloudRenderingPassBuffer.Release();
                    m_CloudRenderingPassBuffer = null;
                }
            }
        }

        private void SetupCloudRenderingPass()
        {
            ReleaseCloudRenderingPass();
            if (m_renderCam != null)
            {
                m_CloudRenderingPassBuffer = new CommandBuffer()
                {
                    name = "Cloud Rendering Pass"
                };

                ExecuteCloudTracingPass(ref m_CloudRenderingPassBuffer, CloudRayGenShader);
                //
                m_CloudRenderingPassBuffer.SetGlobalTexture(k_shaderProperty_Tex_CloudColorDensity_Tex, m_cloud_Handle);
                m_renderCam.AddCommandBuffer(k_CloudRenderingPassCamEvent, m_CloudRenderingPassBuffer);
            }
        }
        #endregion

        #region ----Final Draw Rendering Pass----
        private const CameraEvent k_FinalDrawRenderingPassCamEvent = CameraEvent.AfterEverything;
        private CommandBuffer m_FinalDrawRenderingPassBuffer;

        private void ReleaseFinalDrawRenderingPass()
        {
            if (m_renderCam != null)
            {
                if (m_FinalDrawRenderingPassBuffer != null)
                {
                    m_renderCam.RemoveCommandBuffer(k_FinalDrawRenderingPassCamEvent, m_FinalDrawRenderingPassBuffer);
                    m_FinalDrawRenderingPassBuffer.Release();
                    m_FinalDrawRenderingPassBuffer = null;
                }
            }
        }

        private void SetupFinalDrawRenderingPass()
        {
            ReleaseFinalDrawRenderingPass();
            if (m_renderCam != null)
            {
                m_FinalDrawRenderingPassBuffer = new CommandBuffer()
                {
                    name = "Final Draw Pass"
                };
                //
                m_FinalDrawRenderingPassBuffer.Blit(m_camNormal_Handle, m_camPreNormal_Handle);
                m_FinalDrawRenderingPassBuffer.SetRenderTarget(m_camNormal_Handle);
                m_FinalDrawRenderingPassBuffer.ClearRenderTarget(false, true, Color.clear);
                //
                //m_FinalDrawRenderingPassBuffer.Blit(m_camBaseColor_Handle, BuiltinRenderTextureType.CameraTarget);
                m_FinalDrawRenderingPassBuffer.Blit(m_cloud_Handle, BuiltinRenderTextureType.CameraTarget);//TODO:final draw
                m_renderCam.AddCommandBuffer(k_FinalDrawRenderingPassCamEvent, m_FinalDrawRenderingPassBuffer);
            }
        }
        #endregion

        #region ----Control----
        private Vector2 m_lastFrameRightMousePosition;
        private Vector3 m_cam_EulerAngle;
        [Range(0.1f, 1000f)]
        public float CamMoveSpeed = 100;

        private void CameraControl()
        {
            //Rot
            if (Input.GetMouseButtonDown(1))
            {
                m_lastFrameRightMousePosition = Input.mousePosition;
            }
            else if (Input.GetMouseButton(1))
            {
                Vector2 _mousePosition = Input.mousePosition;
                Vector2 deltaMousePosition = _mousePosition - m_lastFrameRightMousePosition;
                deltaMousePosition *= -0.1f;
                m_cam_EulerAngle += 240 * Time.deltaTime * new Vector3(deltaMousePosition.y, -deltaMousePosition.x, 0);
                transform.eulerAngles = m_cam_EulerAngle;
                m_lastFrameRightMousePosition = _mousePosition;
            }
            //Move
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            transform.position += CamMoveSpeed * Time.deltaTime * (h * transform.right + v * transform.forward);
            if (Input.GetKey(KeyCode.E))
            {
                transform.position += CamMoveSpeed * Time.deltaTime * transform.up;
            }
            else if (Input.GetKey(KeyCode.Q))
            {
                transform.position -= CamMoveSpeed * Time.deltaTime * transform.up;
            }
        }
        #endregion

        #region ----Unity----
        private void Awake()
        {
            SetupMaterials();
        }

        private void OnEnable()
        {
            SetupRenderCam();
            SetupRTHandles();
            m_AtmosphereIterativeRenderingCoroutine = StartCoroutine(AtmosphereIterativeRendering());
        }

        private void Start()
        {
            SetupRayTracingAccelStruct();
            StartCoroutine(AsyncUpdateCloudAABB());
            StartCoroutine(UpdateCustomCloudDataFields());
        }

        private void Update()
        {
            CameraControl();
        }

        private void OnPreRender()
        {
            SetupCloudRenderingPass();
            SetupFinalDrawRenderingPass();
            m_renderCam.SetTargetBuffers(m_camColorBuffer, m_camDepth_Handle.DepthBuffer);
        }

        private void OnPostRender()
        {
            m_renderCam.targetTexture = null;
        }

        private void OnDisable()
        {
            ReleaseCloudRenderingPass();
            ReleaseFinalDrawRenderingPass();
            ReleaseRTHandles();
            if (m_AtmosphereIterativeRenderingCoroutine != null)
            {
                StopCoroutine(m_AtmosphereIterativeRenderingCoroutine);
            }
        }

        private void OnDestroy()
        {
            ReleaseRayTracingAccelStruct();
            ReleaseBuffers();
            ReleaseMaterials();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            CloudRayGenShader = AssetDatabase.LoadAssetAtPath<RayTracingShader>("Assets/ACloud/Shader/Cloud/CloudRTShader.raytrace");
            CloudDataManagementComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ACloud/Shader/Cloud/CloudDataManagementShader.compute");
            CloudSettings = new CloudRenderingSetting(0.01f, 10000, 2000, 5000);
            AtmosphereSettings = new AtmosphereRenderingSetting(8f, 500, 6000, 3, 1f);
        }
#endif
        #endregion
    }
}