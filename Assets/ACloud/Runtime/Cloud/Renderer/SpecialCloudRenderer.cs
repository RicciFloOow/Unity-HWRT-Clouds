using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    public class SpecialCloudRenderer : CustomCloudRenderer
    {
        public uint CloudType;
        public Vector3 Size;
        public CloudDataField CloudData;

        public override int CustomCloudCount()
        {
            return 1;
        }

        public override AABB[] GetCustomCloudAABBData()
        {
            Vector3 center = transform.position;
            AABB aabb = new AABB(center, 0.5f * Size);
            return new AABB[] { aabb };
        }

        public override CloudDataField[] GetCustomCloudDataFields()
        {
            return new CloudDataField[] { CloudData };
        }

        public override uint[] GetCustomCloudCatTypes()
        {
            return new uint[] { (CloudShapeIndices[0] << 16) | CloudType };
        }

        #region ----Unity----
        protected override void OnEnable()
        {
            base.OnEnable();
            HasDataFieldChanged = true;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            Size = new Vector3 (256, 128, 256);
            CloudData = new CloudDataField(new Vector3(1, 0.5f, 0));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, Size);
        }
#endif
        #endregion
    }
}