using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    public struct AABB
    {
        public float MinX;
        public float MinY;
        public float MinZ;
        public float MaxX;
        public float MaxY;
        public float MaxZ;

        public AABB(Vector3 center, Vector3 extent)
        {
            Vector3 min = center - extent;
            Vector3 max = center + extent;
            //
            MinX = min.x;
            MinY = min.y;
            MinZ = min.z;
            MaxX = max.x;
            MaxY = max.y;
            MaxZ = max.z;
        }
    }
}