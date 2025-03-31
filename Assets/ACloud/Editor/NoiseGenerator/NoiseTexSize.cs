using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud.Editor
{
    public enum NoiseTexSize
    {
        [InspectorName("32")]
        Size32 = 32,
        [InspectorName("64")]
        Size64 = 64,
        [InspectorName("128")]
        Size128 = 128,
        [InspectorName("256")]
        Size256 = 256,
        [InspectorName("512")]
        Size512 = 512,
        [InspectorName("1024")]
        Size1024 = 1024,
        [InspectorName("2048")]
        Size2048 = 2048,
        [InspectorName("4096")]
        Size4096 = 4096
    }
}