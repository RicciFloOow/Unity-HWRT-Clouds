using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using ACloud.Editor;
#endif
using UnityEngine;

namespace ACloud
{
#if UNITY_EDITOR
    public partial class CloudManager : MonoBehaviour
    {
        private class CloudShapePair
        {
            public Texture3D Shape;
            public CloudShapeData Data;
            /// <summary>
            /// 是否被用于组合了
            /// </summary>
            public bool IsFilled;

            public CloudShapePair(Texture3D tex, CloudShapeData data)
            {
                Shape = tex;
                Data = data;
                IsFilled = false;
            }
        }

        private int CuboidCoordToZOrderIndex(int x, int y, int z)//这里其实并不是Z字(而是Z的镜像)
        {
            //对于x,y,z=0,1的, 
            //我们要的索引为x + 2 * z + 4 * y
            //也即x | (z << 1) | (y << 2)
            int highPart = ((y & 2) << 1) | (z & 2) | (x >> 1);
            return (highPart << 3) | ((y << 2) | (z << 1) | x);
        }

        private ulong ConvertVolumeToMask(Vector3Int size, Vector3Int offset)
        {
            ulong mask = 0uL;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int x = i * 4;
                        int y = j * 4;
                        int z = k * 4;
                        //
                        if (offset.x <= x && x <= offset.x + size.x
                            && offset.y <= y && y <= offset.y + size.y
                            && offset.z <= z && z <= offset.z + size.z)
                        {
                            int index = CuboidCoordToZOrderIndex(i, j, k);
                            mask |= (1uL << index);
                        }
                    }
                }
            }
            return mask;
        }

        private Vector3Int BlockToConservativeEmptySize(ulong blockState, out Vector3Int offset)
        {
            offset = Vector3Int.zero;
            var dimensionChecks = new (ulong Mask, Vector3Int Offset, Vector3Int Size)[]
            {
                (0x000000000000FF00, new Vector3Int(2, 0, 0), new Vector3Int(2, 4, 4)),
                (0x0000000000FF0000, new Vector3Int(0, 0, 2), new Vector3Int(4, 4, 2)),
                (0x00000000FF000000, new Vector3Int(2, 0, 2), new Vector3Int(2, 4, 2)),
                (0x000000FF00000000, new Vector3Int(0, 2, 0), new Vector3Int(4, 2, 4)),
                (0x0000FF0000000000, new Vector3Int(2, 2, 0), new Vector3Int(2, 2, 4)),
                (0x00FF000000000000, new Vector3Int(0, 2, 2), new Vector3Int(4, 2, 2)),
                (0xFF00000000000000, new Vector3Int(2, 2, 2), new Vector3Int(2, 2, 2))
            };

            foreach (var (mask, checkOffset, checkSize) in dimensionChecks)
            {
                if ((blockState & mask) == 0)
                {
                    offset = checkOffset;
                    return checkSize;
                }
            }
            offset = new Vector3Int(2, 2, 2);
            return new Vector3Int(2, 2, 2);
        }

        private bool IsAccVolumeEmpty(ulong[,,] accStruct, Vector3Int start, Vector3Int size, Vector3Int accStructSize)
        {
            if (MathUtility.AnyGreater(start + size, accStructSize))
            {
                return false;
            }
            //
            for (int x = 0; x < accStructSize.x; x++)
            {
                for (int y = 0; y < accStructSize.y; y++)
                {
                    for (int z = 0; z < accStructSize.z; z++)
                    {
                        ulong blockState = accStruct[x, y, z];
                        if (start.x <= x && x <= start.x + size.x
                            && start.y <= y && y <= start.y + size.y
                            && start.z <= z && z <= start.z + size.z
                            && blockState != 0)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private bool TryFindFillableCoord(ref ulong[,,] accStruct, CloudShapeData data, Vector3Int accStructSize)
        {
            for (int x = 0; x < accStructSize.x; x++)
            {
                for (int y = 0; y < accStructSize.y; y++)
                {
                    for (int z = 0; z < accStructSize.z; z++)
                    {
                        ulong blockState = accStruct[x, y, z];
                        //先尝试在完全空的64x64x64的block内填充
                        if (blockState == 0)
                        {
                            if (MathUtility.AllGreaterEqual(64, data.TexSize))
                            {
                                //可用直接填充
                                accStruct[x, y, z] = ConvertVolumeToMask(data.TexSize, Vector3Int.zero);
                                data.TexOffset = 64 * new Vector3Int(x, y, z);
                                return true;
                            }
                            else
                            {
                                //需要查看相邻的Block是否有空间给它填充
                                Vector3Int accCoordSize = new Vector3Int(data.TexSize.x / 64, data.TexSize.y / 64, data.TexSize.z / 64);
                                if (IsAccVolumeEmpty(accStruct, new Vector3Int(x, y, z), accCoordSize, accStructSize))
                                {
                                    for (int i = 0; i < accCoordSize.x; i++)
                                    {
                                        for (int j = 0; j < accCoordSize.y; j++)
                                        {
                                            for (int k = 0; k < accCoordSize.z; k++)
                                            {
                                                accStruct[x + i, y + j, z + k] = ConvertVolumeToMask(Vector3Int.Min(data.TexSize, new Vector3Int(64, 64, 64)), Vector3Int.zero);
                                            }
                                        }
                                    }
                                    data.TexOffset = 64 * new Vector3Int(x, y, z);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            //没有完全空的64x64x64的block可以填充, 我们将剩余未被填充的尺寸大于64x64x64的纹理直接舍去(虽然, 可能明明仍有空间允许填充却被舍去了)
            if (MathUtility.AllGreaterEqual(64, data.TexSize))
            {
                for (int x = 0; x < accStructSize.x; x++)
                {
                    for (int y = 0; y < accStructSize.y; y++)
                    {
                        for (int z = 0; z < accStructSize.z; z++)
                        {
                            ulong blockState = accStruct[x, y, z];
                            if (blockState == ulong.MaxValue)
                            {
                                continue;//说明当前block被完全占用
                            }
                            //
                            Vector3Int emptySpace = BlockToConservativeEmptySize(blockState, out Vector3Int offset);
                            if (MathUtility.AllGreaterEqual(emptySpace * 16, data.TexSize))
                            {
                                accStruct[x, y, z] = blockState | ConvertVolumeToMask(data.TexSize, 16 * offset);
                                data.TexOffset = 16 * offset + 64 * new Vector3Int(x, y, z);
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        //本来想用[InitializeOnEnterPlayMode]来自动在进入play mode时重新生成, 但是这样会有一个问题: 每次进入play mode都要重新生成一次bundle(太浪费了)
        //如果我们用纹理的InstanceID、guid等信息来比较判断是否需要更新, 那么会容易出现明明纹理修改了(但InstanceID、尺寸等并未修改)也无法强制bundle重新生成的问题(这样的问题在以前用过的一些插件上碰到过几次)
        //因此我们这里还是手动控制生成
        public void OnPreBuildCloudShapeBundle(ref bool isBuilding)
        {
            if (CloudShapeBundle == null)
            {
                Debug.LogWarning("未配置目标CloudShapeBundle!");
                return;
            }
            Dictionary<Texture3D, CloudShapeData> shapeDataDict = new Dictionary<Texture3D, CloudShapeData>();
            Vector3Int bundleSize = CloudShapeBundle.GetBundleSize();
            //加载程序化的云
            if (CloudShapeBundle.ProceduralShapePathList != null && CloudShapeBundle.ProceduralShapePathList.Count > 0)
            {
                for (int i = 0; i < CloudShapeBundle.ProceduralShapePathList.Count; i++)
                {
                    string path = CloudShapeBundle.ProceduralShapePathList[i];
                    Texture3D sdf = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                    if (sdf != null)
                    {
                        var shapeSize = new Vector3Int(sdf.width, sdf.height, sdf.depth);
                        if (MathUtility.AllGreaterEqual(bundleSize, shapeSize))
                        {
                            if (!shapeDataDict.ContainsKey(sdf))
                            {
                                shapeDataDict.Add(sdf, new CloudShapeData(path, shapeSize));
                            }
                        }
                        else
                        {
                            Debug.LogWarning(sdf.name + "的尺寸超过设置的bundle的尺寸!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("未能加载" + path + "下的纹理!");
                    }
                }
            }
            //获取全部CustomCloudRenderer
            CustomCloudRenderer[] customClouds = FindObjectsOfType<CustomCloudRenderer>(true);
            //
            if (customClouds != null && customClouds.Length > 0)
            {
                //先检查一遍配置的纹理是否有效: 是否配置了纹理, 纹理的大小是否超过目标bundle的尺寸
                for (int i = 0; i < customClouds.Length; i++)
                {
                    var cloud = customClouds[i];
                    if (cloud.CloudShapeTexs != null && cloud.CloudShapeTexs.Length > 0)
                    {
                        for (int j = 0; j < cloud.CloudShapeTexs.Length; j++)
                        {
                            if (cloud.CloudCategory != CustomCloudCategory.Cloud)
                            {
                                continue;
                            }
                            var shape = cloud.CloudShapeTexs[j];
                            if (shape != null)
                            {
                                var shapeSize = new Vector3Int(shape.width, shape.height, shape.depth);
                                if (MathUtility.AllGreaterEqual(bundleSize, shapeSize))
                                {
                                    if (!shapeDataDict.ContainsKey(shape))
                                    {
                                        string texPath = AssetDatabase.GetAssetPath(shape);
                                        shapeDataDict.Add(shape, new CloudShapeData(texPath, shapeSize));
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning(cloud.name + "的第" + j + "个形状纹理的尺寸超过设置的bundle的尺寸!\nShape Size: " + shapeSize + ", Bundle Size: " + bundleSize);
                                }
                            }
                            else
                            {
                                Debug.LogError(cloud.name + "的第" + j + "个形状纹理为空!");
                                return;//这种必须停止, 因为后面的处理也必会出错
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning(cloud.name + "未配置云的形状!");
                    }
                }
            }
            //bundle的生成是一个"相对简单"的三维装箱问题(毕竟尺寸全是2的幂次, 加速结构容易设计)
            //不过我们这里是demo, 组合的方法不是主要的, 就随便写一个(极其)简陋的方案
            List<CloudShapePair> shapeDataList = shapeDataDict.Select(kvp => new CloudShapePair(kvp.Key, kvp.Value)).ToList();
            shapeDataList = shapeDataList.OrderByDescending(c => c.Data.Area).ThenByDescending(c => c.Data.Volume).ToList();//面积占用的多的先处理, 同面积下体积占的多的先处理(本质上也等效于.y大的先处理)
            //
            Texture3D sdfAtlas = null;
            EditGraphicsUtility.AllocateTexture3D(ref sdfAtlas, bundleSize.x, bundleSize.y, bundleSize.z, TextureFormat.RFloat);//这里需要与所用的SDF的纹理格式一致
            List<CloudShapeData> validCloudShapes = new List<CloudShapeData>();
            //
            Vector3Int accStructSize = new Vector3Int(bundleSize.x / 64, bundleSize.y / 64, bundleSize.z / 64);
            //我们用一个long来记录64x64x64区域的状态, 每一位对应一个16x16x16大小的区域, 0表示该位对应的区域未被占用, 1表示该为对应的区域被占用
            ulong[,,] accStruct = new ulong[accStructSize.x, accStructSize.y, accStructSize.z];
            for (int i = 0; i < shapeDataList.Count; i++)
            {
                var shapePair = shapeDataList[i];
                if (TryFindFillableCoord(ref accStruct, shapePair.Data, accStructSize))
                {
                    shapePair.IsFilled = true;
                    shapePair.Data.RegisteredIndex = validCloudShapes.Count;
                    validCloudShapes.Add(shapePair.Data);
                    Vector3Int size = shapePair.Data.TexSize;
                    Vector3Int offset = shapePair.Data.TexOffset;
                    for (int j = 0; j < size.z; j++)
                    {
                        Graphics.CopyTexture(shapePair.Shape, j, 0, 0, 0, size.x, size.y, sdfAtlas, j + offset.z, 0, offset.x, offset.y);
                    }
                }
            }
            sdfAtlas.Apply();
            CloudShapeBundle.SDFAtlasTex = sdfAtlas;
            CloudShapeBundle.ShapeDataList = validCloudShapes;
            //
            {
                //修改CustomCloudRenderer中记录的索引, 当然我们这里对未被打包组合的纹理的处理就非常简陋了(毕竟demo)
                if (customClouds != null && customClouds.Length > 0)
                {
                    for (int i = 0; i < customClouds.Length; i++)
                    {
                        var cloud = customClouds[i];
                        if (cloud.CloudCategory != CustomCloudCategory.Cloud)
                        {
                            continue;
                        }
                        if (cloud.CloudShapeTexs != null && cloud.CloudShapeTexs.Length > 0)
                        {
                            uint[] registeredIndices = new uint[cloud.CloudShapeTexs.Length];
                            for (int j = 0; j < cloud.CloudShapeTexs.Length; j++)
                            {
                                var shape = cloud.CloudShapeTexs[j];//不必检查是否为空
                                var data = shapeDataDict[shape];
                                registeredIndices[j] = data.RegisteredIndex < 0 ? 0u : (uint)data.RegisteredIndex;
                            }
                            //
                            cloud.SetCustomCloudShapeIndices(registeredIndices);
                        }
                    }
                }
            }
            //
            {
                //导出Tex3D
                string fileName = CloudShapeBundle.name + "_SDFAtlas";
                string bundlePath = AssetDatabase.GetAssetPath(CloudShapeBundle);
                string exportTexPath = Path.GetDirectoryName(bundlePath) + "/Texs/" + fileName + ".asset";//注意, 我们这里只考虑使用形状的SDF, 实际中一般也会制作用于散射的LUT(类似大气层的渲染时有些人用的LUT)并打包组合成一张
                string fullPath = Path.GetDirectoryName(Path.GetFullPath(exportTexPath)).Replace('/', '\\');
                System.IO.Directory.CreateDirectory(fullPath);
                AssetDatabase.CreateAsset(sdfAtlas, exportTexPath);
                AssetDatabase.Refresh();
            }
            //
            sdfAtlas = null;
            //
            isBuilding = false;
            Debug.Log("组合完成!");
        }
    }
#endif
}