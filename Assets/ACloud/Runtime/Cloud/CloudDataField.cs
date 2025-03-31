using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    [Serializable]
    public struct CloudDataField
    {
        public Vector3 Color;

        public CloudDataField(Vector3 color)
        {
            Color = color;
        }
    }
}