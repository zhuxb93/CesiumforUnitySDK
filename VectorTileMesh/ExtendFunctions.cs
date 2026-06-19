using System;
using System.Collections.Generic;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    /// <summary>
    /// 一些扩展方法
    /// </summary>
    public static class ExtendFunctions
    {
        /// <summary>
        /// 根据名字查找子物体
        /// </summary>
        public static Transform FindNameAllChild(this Transform trans, string name, bool isActive = true)
        {
            Transform[] transformArry = trans.GetComponentsInChildren<Transform>(isActive);//包含trans
            foreach (var item in transformArry)
            {
                if (item.gameObject.name == name)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// 数值范围归一化
        /// </summary>
        /// <param name="value"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float Normalize(this float value, float min, float max)
        {
            return (Mathf.Clamp(value, min, max) - min) / (max - min);
        }


        //public static Dictionary<TKey, List<T>> GroupBy<T, TKey>(this List<T> list, Func<T, TKey> keySelector)
        //{
        //    Dictionary<TKey, List<T>> groups = new Dictionary<TKey, List<T>>();

        //    for (int i = 0; i < list.Count; i++)
        //    {
        //        T item = list[i];
        //        TKey key = keySelector(item);

        //        if (!groups.ContainsKey(key))
        //        {
        //            groups[key] = new List<T>();
        //        }

        //        groups[key].Add(item);
        //    }

        //    return groups;
        //}


        public static List<T> Where<T>(this List<T> list, Func<T, bool> predicate)
        {
            List<T> result = new List<T>();

            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];

                if (predicate(item))
                {
                    result.Add(item);
                }
            }

            return result;
        }


        /// <summary>
        /// 十六进制颜色转Color
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static Color HexToColor(this string hex)
        {
            hex = hex.Replace("0x", "");//in case the string is formatted 0xFFFFFF
            hex = hex.Replace("#", "");//in case the string is formatted #FFFFFF
            byte a = 255;//assume fully visible unless specified in hex
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            //Only use alpha if the string has enough characters
            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return new Color32(r, g, b, a);
        }

    }

}