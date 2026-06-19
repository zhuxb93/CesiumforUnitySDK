using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace GeoTiles
{
    public static class BinaryStructSerializer
    {
        public static void SaveToFile<T>(string path, T data) where T : struct
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            WriteStruct(bw, data);
        }

        public static void SaveListToFile<T>(string path, List<T> list) where T : struct
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            bw.Write(list.Count);
            foreach (var item in list)
            {
                WriteStruct(bw, item);
            }
        }

        public static T LoadFromFile<T>(string path) where T : struct
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            return ReadStruct<T>(br);
        }

        public static List<T> LoadListFromFile<T>(string path) where T : struct
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            int count = br.ReadInt32();
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
                list.Add(ReadStruct<T>(br));
            return list;
        }

        private static void WriteStruct<T>(BinaryWriter writer, T obj)
        {
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                WriteAny(writer, field.GetValue(obj));
            }
        }

        private static T ReadStruct<T>(BinaryReader reader) where T : struct
        {
            T instance = default;
            var handle = __makeref(instance);

            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var value = ReadAny(reader, field.FieldType);
                field.SetValueDirect(handle, value);
            }

            return instance;
        }


        private static void WriteAny(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case int v: writer.Write(v); break;
                case uint v: writer.Write(v); break;
                case float v: writer.Write(v); break;
                case double v: writer.Write(v); break;
                case bool v: writer.Write(v); break;
                case string v: writer.Write(v ?? ""); break;

                case Vector2 v: writer.Write(v.x); writer.Write(v.y); break;
                case Vector3 v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); break;
                case Vector4 v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); break;
                case Quaternion v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); break;
                case Color v: writer.Write(v.r); writer.Write(v.g); writer.Write(v.b); writer.Write(v.a); break;
                case float2 v: writer.Write(v.x); writer.Write(v.y); break;
                case float3 v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); break;
                case float4 v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); break;
                case double2 v: writer.Write(v.x); writer.Write(v.y); break;
                case double3 v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); break;
                case double4 v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); writer.Write(v.w); break;

                case Array arr:
                    writer.Write(arr.Length);
                    foreach (var item in arr) WriteAny(writer, item);
                    break;

                case IList list:
                    writer.Write(list.Count);
                    foreach (var item in list) WriteAny(writer, item);
                    break;

                case IDictionary dict:
                    writer.Write(dict.Count);
                    foreach (DictionaryEntry kv in dict)
                    {
                        WriteAny(writer, kv.Key);
                        WriteAny(writer, kv.Value);
                    }
                    break;

                default:
                    if (value != null && value.GetType().IsValueType)
                    {
                        MethodInfo method = typeof(BinaryStructSerializer).GetMethod(nameof(WriteStruct), BindingFlags.Static | BindingFlags.NonPublic)?.MakeGenericMethod(value.GetType());
                        method?.Invoke(null, new object[] { writer, value });
                    }
                    else
                        throw new NotSupportedException($"不支持的字段类型：{value?.GetType()}");
                    break;
            }
        }

        private static object ReadAny(BinaryReader reader, Type type)
        {
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(string)) return reader.ReadString();

            if (type == typeof(Vector2)) return new Vector2(reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Vector3)) return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Vector4)) return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Quaternion)) return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Color)) return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(float2)) return new float2(reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(float3)) return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(float4)) return new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(double2)) return new double2(reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(double3)) return new double3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(double4)) return new double4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

            if (type.IsArray)
            {
                int len = reader.ReadInt32();
                var elementType = type.GetElementType();
                Array array = Array.CreateInstance(elementType, len);
                for (int i = 0; i < len; i++)
                {
                    array.SetValue(ReadAny(reader, elementType), i);
                }
                return array;
            }

            if (type.IsGenericType)
            {
                var gtd = type.GetGenericTypeDefinition();
                var args = type.GetGenericArguments();

                if (gtd == typeof(List<>))
                {
                    int count = reader.ReadInt32();
                    IList list = (IList)Activator.CreateInstance(type);
                    for (int i = 0; i < count; i++)
                        list.Add(ReadAny(reader, args[0]));
                    return list;
                }
                if (gtd == typeof(Dictionary<,>))
                {
                    int count = reader.ReadInt32();
                    IDictionary dict = (IDictionary)Activator.CreateInstance(type);
                    for (int i = 0; i < count; i++)
                    {
                        var key = ReadAny(reader, args[0]);
                        var val = ReadAny(reader, args[1]);
                        dict.Add(key, val);
                    }
                    return dict;
                }
            }

            if (type.IsValueType)
            {
                MethodInfo method = typeof(BinaryStructSerializer).GetMethod(nameof(ReadStruct), BindingFlags.Static | BindingFlags.NonPublic)?.MakeGenericMethod(type);
                return method?.Invoke(null, new object[] { reader });
            }

            throw new NotSupportedException($"不支持的类型：{type}");
        }
    }
}
