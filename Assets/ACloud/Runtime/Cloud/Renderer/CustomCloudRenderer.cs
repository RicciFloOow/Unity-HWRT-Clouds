using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    public enum CustomCloudCategory
    {
        Cloud,
        LightSource,
        ComplementSet
    }


    public class CustomCloudRenderer : MonoBehaviour
    {
        [HideInInspector]
        public uint[] CloudShapeIndices;

        public CustomCloudCategory CloudCategory;

        public virtual AABB[] GetCustomCloudAABBData()
        {
            return null;
        }

        public virtual CloudDataField[] GetCustomCloudDataFields()
        {
            return null;
        }
        
        public virtual uint[] GetCustomCloudCatTypes()
        {
            return null;
        }

        public virtual int CustomCloudCount()
        {
            return 0;
        }

        public virtual bool HasDataFieldChanged
        {
            get;
            set;
        }

        #region ----Editor Helper----
#if UNITY_EDITOR
        public Texture3D[] CloudShapeTexs;

        public virtual void SetCustomCloudShapeIndices(uint[] registeredIndices)
        {
            if (registeredIndices != null)
            {
                CloudShapeIndices = registeredIndices;
            }
        }
#endif
        #endregion

        #region ----Unity----
        protected virtual void OnEnable()
        {
            if (CloudManager.Instance != null)
            {
                CloudManager.Instance.Register(this);
            }
        }

        protected virtual void OnDisable()
        {
            if (CloudManager.Instance != null)
            {
                CloudManager.Instance.Unregister(this);
            }
        }
        #endregion
    }
}