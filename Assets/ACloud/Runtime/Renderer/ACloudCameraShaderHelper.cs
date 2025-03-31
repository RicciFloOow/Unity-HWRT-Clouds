using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    public partial class ACloudCamera
    {
        #region ----Kernel Name----
        private const string k_shaderKernel_CopyCustomCloudDataFieldsKernel = "CopyCustomCloudDataFieldsKernel";
        #endregion

        #region -----RT Acc Struct----
        private static readonly int k_shaderProperty_RTAccStruct_CloudsAccelStruct = Shader.PropertyToID("_CloudsAccelStruct");
        #endregion

        #region ----Int----
        private readonly static int k_shaderProperty_Int_IterativeRenderingStep = Shader.PropertyToID("_IterativeRenderingStep");
        #endregion

        #region ----Float----
        private readonly static int k_shaderProperty_Float_CameraZoom = Shader.PropertyToID("_CameraZoom");
        private readonly static int k_shaderProperty_Float_CloudSimulationTime = Shader.PropertyToID("_CloudSimulationTime");
        private readonly static int k_shaderProperty_Float_CloudTracingNearPlane = Shader.PropertyToID("_CloudTracingNearPlane");
        private readonly static int k_shaderProperty_Float_CloudTracingFarPlane = Shader.PropertyToID("_CloudTracingFarPlane");
        private readonly static int k_shaderProperty_Float_CloudTracingLayerBottom = Shader.PropertyToID("_CloudTracingLayerBottom");
        private readonly static int k_shaderProperty_Float_CloudTracingLayerTop = Shader.PropertyToID("_CloudTracingLayerTop");
        private readonly static int k_shaderProperty_Float_CameraRelativeHeight = Shader.PropertyToID("_CameraRelativeHeight");
        private readonly static int k_shaderProperty_Float_HeightScaleValue = Shader.PropertyToID("_HeightScaleValue");
        private readonly static int k_shaderProperty_Float_AtmosphericThickness = Shader.PropertyToID("_AtmosphericThickness");
        private readonly static int k_shaderProperty_Float_PlanetRadius = Shader.PropertyToID("_PlanetRadius");
        private readonly static int k_shaderProperty_Float_MainLightIntensity = Shader.PropertyToID("_MainLightIntensity");
        #endregion

        #region ----Vector----
        private readonly static int k_shaderProperty_Vec_MainLightDirection = Shader.PropertyToID("_MainLightDirection");
        private readonly static int k_shaderProperty_Vec_SunLightDirection = Shader.PropertyToID("_SunLightDirection");
        private readonly static int k_shaderProperty_Vec_RayleighScatterParam = Shader.PropertyToID("_RayleighScatterParam");
        private readonly static int k_shaderProperty_Vec_CloudShapeBundleInvSize = Shader.PropertyToID("_CloudShapeBundleInvSize");
        private readonly static int k_shaderProperty_Vec_CameraForward = Shader.PropertyToID("_CameraForward");
        #endregion

        #region ----Matrix----
        private readonly static int k_shaderProperty_Matrix_CameraVPInvMatrix = Shader.PropertyToID("_CameraVPInvMatrix");
        private readonly static int k_shaderProperty_Matrix_CubeFaceRenderingCam2WorldMatrix = Shader.PropertyToID("_CubeFaceRenderingCam2WorldMatrix");
        #endregion

        #region ----Texs----
        private readonly static int k_shaderProperty_Tex_CameraDepthRT = Shader.PropertyToID("_CameraDepthRT");
        private readonly static int k_shaderProperty_Tex_CameraNormalRT = Shader.PropertyToID("_CameraNormalRT");
        private readonly static int k_shaderProperty_Tex_RW_CloudColorDensityRT = Shader.PropertyToID("RW_CloudColorDensityRT");
        //
        private readonly static int k_shaderProperty_Tex_CloudNoise3D_Tex = Shader.PropertyToID("_CloudNoise3D_Tex");
        private readonly static int k_shaderProperty_Tex_CloudShapeBundle_Tex = Shader.PropertyToID("_CloudShapeBundle_Tex");
        private readonly static int k_shaderProperty_Tex_CloudColorDensity_Tex = Shader.PropertyToID("_CloudColorDensity_Tex");
        //
        private readonly static int k_shaderProperty_Tex_AtmosphereCubeTex = Shader.PropertyToID("_AtmosphereCubeTex");
        #endregion

        #region ----Buffers----
        private readonly static int k_shaderProperty_Buffer_ACloudAABBBuffer = Shader.PropertyToID("_ACloudAABBBuffer");
        private readonly static int k_shaderProperty_Buffer_ACloudTracingCustomDataFieldBuffer = Shader.PropertyToID("_ACloudTracingCustomDataFieldBuffer");
        private readonly static int k_shaderProperty_Buffer_ACloudTracingDataFieldBuffer = Shader.PropertyToID("_ACloudTracingDataFieldBuffer");
        private readonly static int k_shaderProperty_Buffer_ACloudTypeBuffer = Shader.PropertyToID("_ACloudTypeBuffer");
        private readonly static int k_shaderProperty_Buffer_ACloudShapeDataBuffer = Shader.PropertyToID("_ACloudShapeDataBuffer");

        private readonly static int k_shaderProperty_Buffer_RW_ACloudTracingDataFieldBuffer = Shader.PropertyToID("RW_ACloudTracingDataFieldBuffer");
        #endregion
    }
}