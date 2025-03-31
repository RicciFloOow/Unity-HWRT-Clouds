using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    public enum AtmosphereQuality
    {
        High,
        Normal,
        Low
    }

    [Serializable]
    public struct AtmosphereRenderingSetting
    {
        /// <summary>
        /// 分子密度的参数
        /// </summary>
        public float HeightScaleValue;
        /// <summary>
        /// 大气层厚度(等比缩放后的, 单位为km)
        /// </summary>
        public float AtmosphericThickness;
        /// <summary>
        /// 星球半径(等比缩放后的, 单位为km)
        /// </summary>
        public float PlanetRadius;
        /// <summary>
        /// 太阳光的强度
        /// </summary>
        public float SunLightIntensity;
        /// <summary>
        /// Rayleigh散射常数
        /// </summary>
        public float RayleighScatteringConstant;
        /// <summary>
        /// RGB三色的波长
        /// </summary>
        public Vector3 RGBWavelength;

        public AtmosphereQuality Quality;

        public Vector3 RayleighScatterParam
        {
            get
            {
                //如果不乘上0.01f, 那么RayleighScatteringConstant需要额外提高很多
                return RayleighScatteringConstant * new Vector3(
                    Mathf.Pow(1f / (RGBWavelength.x * 0.01f), 4),
                    Mathf.Pow(1f / (RGBWavelength.y * 0.01f), 4),
                    Mathf.Pow(1f / (RGBWavelength.z * 0.01f), 4)
                    );
            }
        }

        public AtmosphereRenderingSetting(float heightScaleValue, float atmoThickness, float planetRadius, float sunLightIntensity, float rayleighScatteringConstant)
        {
            HeightScaleValue = heightScaleValue;
            AtmosphericThickness = atmoThickness;
            PlanetRadius = planetRadius;
            SunLightIntensity = sunLightIntensity;
            RayleighScatteringConstant = rayleighScatteringConstant;
            RGBWavelength = new Vector3(689f, 621.5f, 456.3f);
            Quality = AtmosphereQuality.Normal;
        }
    }
}