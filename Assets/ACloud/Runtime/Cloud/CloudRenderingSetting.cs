using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    [Serializable]
    public struct CloudRenderingSetting
    {
        public float NearPlane;
        public float FarPlane;
        public float LayerBottom;
        public float LayerTop;


        public CloudRenderingSetting(float near, float far, float bottom, float top)
        {
            NearPlane = near;
            FarPlane = far;
            LayerBottom = bottom;
            LayerTop = top;
        }
    }
}