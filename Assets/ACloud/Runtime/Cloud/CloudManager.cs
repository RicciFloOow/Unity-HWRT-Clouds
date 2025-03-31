//需要在execution order里添加并给负值
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    public partial class CloudManager : MonoBehaviour
    {
        #region ----Singleton----
        private static CloudManager instance;
        public static CloudManager Instance => instance;
        #endregion

        #region ----Settings----
        public CloudShapeBundle CloudShapeBundle;
        #endregion

        #region ----Custom Data----
        private int m_customCloudsCount;
        private List<CustomCloudRenderer> m_customCloudRendererList;

        public void Register(CustomCloudRenderer renderer)
        {
            if (m_customCloudRendererList.Contains(renderer))
            {
                return;
            }
            int rendererCloudsCount = renderer.CustomCloudCount();
            if (m_customCloudsCount + rendererCloudsCount > CloudConfig.CLOUD_CUSTOM_COUNT)
            {
                Debug.LogWarning("当前添加的自定义的云的对象数已达上限:" + CloudConfig.CLOUD_CUSTOM_COUNT);
                return;
            }
            m_customCloudRendererList.Add(renderer);
            m_customCloudsCount += rendererCloudsCount;
        }

        public void Unregister(CustomCloudRenderer renderer)
        {
            if (!m_customCloudRendererList.Contains(renderer))
            {
                return;
            }
            int rendererCloudsCount = renderer.CustomCloudCount();
            m_customCloudRendererList.Remove(renderer);
            m_customCloudsCount -= rendererCloudsCount;
        }

        public AABB[] GetCustomCloudAABBData()
        {
            var aabb = new AABB[CloudConfig.CLOUD_CUSTOM_COUNT];
            int startIndex = 0;
            for (int i = 0; i < m_customCloudRendererList.Count; i++)
            {
                var caabb = m_customCloudRendererList[i].GetCustomCloudAABBData();
                for (int j = 0; j < caabb.Length; j++)
                {
                    aabb[j + startIndex] = caabb[j];
                }
                startIndex += caabb.Length;
            }
            return aabb;
        }

        public CloudDataField[] GetCustomCloudDataFields()
        {
            var dataFields = new CloudDataField[CloudConfig.CLOUD_CUSTOM_COUNT];
            int startIndex = 0;
            for (int i = 0; i < m_customCloudRendererList.Count; i++)
            {
                var cdf = m_customCloudRendererList[i].GetCustomCloudDataFields();
                for (int j = 0; j < cdf.Length; j++)
                {
                    dataFields[j + startIndex] = cdf[j];
                }
                startIndex += cdf.Length;
                m_customCloudRendererList[i].HasDataFieldChanged = false;
            }
            return dataFields;
        }

        public uint[] GetCustomCloudTypes()
        {
            var types = new uint[CloudConfig.CLOUD_CUSTOM_COUNT];
            int startIndex = 0;
            for (int i = 0; i < m_customCloudRendererList.Count; i++)
            {
                var t = m_customCloudRendererList[i].GetCustomCloudCatTypes();
                for (int j = 0; j < t.Length; j++)
                {
                    types[j + startIndex] = t[j];
                }
                startIndex += t.Length;
            }
            return types;
        }

        public bool HasCustomCloudDataChanged()
        {
            for (int i = 0; i < m_customCloudRendererList.Count; i++)
            {
                if (m_customCloudRendererList[i].HasDataFieldChanged)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region ----Shape Bundle----
        public Vector3 ShapeBundleInvSize { get; private set; }

        private void GetShapeBundleSize()
        {
            var size = CloudShapeBundle.GetBundleSize();
            ShapeBundleInvSize = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
        }
        #endregion

        #region ----Unity----
        private void Awake()
        {
            instance = this;
            //
            m_customCloudRendererList = new List<CustomCloudRenderer>();
            //
            m_customCloudsCount = 0;
        }

        private void Start()
        {
            GetShapeBundleSize();
        }

        private void Update()
        {
            
        }

        private void OnDestroy()
        {
            
        }
        #endregion
    }
}