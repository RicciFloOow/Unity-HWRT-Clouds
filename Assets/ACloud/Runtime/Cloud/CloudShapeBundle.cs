//需要注意的是当前需要将SDF预先打包成3D图集也是无奈之举
//以后看看bindless texture对性能与灵活度的提升(不过这工作量就很大了)
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ACloud
{
    public enum ShapeBundleSize
    {
        [InspectorName("256X256X64")]
        Light,
        [InspectorName("512X512X64")]
        Medium,
        [InspectorName("512X512X128")]
        Large
    }

    [Serializable]
    public class CloudShapeData
    {
        /// <summary>
        /// 纹理的路径
        /// </summary>
        public string SDFTexPath;
        public Vector3Int TexSize;
        /// <summary>
        /// 默认-1:未注册
        /// </summary>
        public int RegisteredIndex;
        public Vector3Int TexOffset;

        public int Area
        {
            get
            {
                return TexSize.x * TexSize.z;
            }
        }

        public int Volume
        {
            get
            {
                return TexSize.x * TexSize.y * TexSize.z;
            }
        }

        public CloudShapeData(string path, Vector3Int size)
        {
            SDFTexPath = path;
            TexSize = size;
            RegisteredIndex = -1;
            TexOffset = Vector3Int.zero;
        }
    }

    [PreferBinarySerialization]
    [CreateAssetMenu(fileName = "CloudShapeBundle", menuName = "ACloud/Data/New CloudShapeBundle", order = 1)]
    public class CloudShapeBundle : ScriptableObject
    {
        public ShapeBundleSize BundleSize;
        [HideInInspector]
        public List<string> ProceduralShapePathList;
        [HideInInspector]
        public List<CloudShapeData> ShapeDataList;
        [HideInInspector]
        public Texture3D SDFAtlasTex;

        public Texture3D Noise3DTex;

        public int ShapeCount => ShapeDataList.Count;

        public Vector3Int GetBundleSize()
        {
            switch (BundleSize)
            {
                case ShapeBundleSize.Light:
                    return new Vector3Int(256, 64, 256);
                case ShapeBundleSize.Medium:
                    return new Vector3Int(512, 64, 512);
                case ShapeBundleSize.Large:
                    return new Vector3Int(512, 128, 512);
                default:
                    return new Vector3Int(256, 64, 256);
            }
        }

        public Vector2Int[] GetCloudShapeDatas()
        {
            //所有尺寸小于2^10, 因此可以存进一个uint里
            Vector2Int[] datas = new Vector2Int[ShapeCount];
            for (int i = 0; i < ShapeCount; i++)
            {
                var d = ShapeDataList[i];
                int size = (d.TexSize.x << 20) | (d.TexSize.y << 10) | (d.TexSize.z);
                int offset = (d.TexOffset.x << 20) | (d.TexOffset.y << 10) | (d.TexOffset.z);
                datas[i] = new Vector2Int(size, offset);
            }
            return datas;
        }

#if UNITY_EDITOR
        [ContextMenu("强制保存")]
        public void ForceSave()
        {
            //
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            //
            Debug.Log("CloudShapeBundle保存成功");
        }
#endif
    }
}