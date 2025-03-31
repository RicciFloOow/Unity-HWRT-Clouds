using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ACloud
{
    public static class MathUtility
    {
        #region ----Comparison----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AnyGreater(Vector3Int a, Vector3Int b)
        {
            return a.x > b.x || a.y > b.y || a.z > b.z;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AllGreaterEqual(Vector3Int a, Vector3Int b)//all(a >= b)
        {
            return a.x >= b.x && a.y >= b.y && a.z >= b.z;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AllGreaterEqual(int a, Vector3Int b)
        {
            return a >= b.x && a >= b.y && a >= b.z;
        }
        #endregion
    }
}