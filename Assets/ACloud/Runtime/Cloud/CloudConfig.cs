using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ACloud
{
    public static class CloudConfig
    {
        /// <summary>
        /// 程序化控制的一般的云的数量上限
        /// </summary>
        public const int CLOUD_PROCEDURAL_COUNT = 16384;
        /// <summary>
        /// 用户可控的云(以及相关的对象，比如减集、光源等)的数量上限
        /// </summary>
        public const int CLOUD_CUSTOM_COUNT = 1024;
        /// <summary>
        /// 与云相关的全部对象的上限
        /// </summary>
        public const int CLOUD_COUNT = CLOUD_PROCEDURAL_COUNT + CLOUD_CUSTOM_COUNT;
    }
}