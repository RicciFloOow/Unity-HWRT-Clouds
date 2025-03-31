//我们不会生成很大的SDF
using UnityEngine;

namespace ACloud.Editor
{
    public enum VolumeSDFTexSize
    {
        [InspectorName("8")]
        Size8 = 8,
        [InspectorName("16")]
        Size16 = 16,
        [InspectorName("32")]
        Size32 = 32,
        [InspectorName("64")]
        Size64 = 64,
        [InspectorName("128")]
        Size128 = 128
    }
}