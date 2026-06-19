using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;
namespace GeoTiles
{
    public static class BinarySerializer
    {
        // 原有的 List<T> 方法保持不变
        public static void SaveToFile<T>(string path, List<T> list)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(list.Count);
                foreach (var item in list)
                {
                    WriteItem(writer, item);
                }
                writer.Flush();
                fs.Close();
            }
        }

        public static List<T> LoadFromFile<T>(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                List<T> list = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadItem<T>(reader));
                }
                return list;
            }
        }

        public static List<T> LoadFromFile<T>(string path, List<T> list)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadItem<T>(reader));
                }
                return list;
            }
        }

        // 新增：数组支持
        public static void SaveToFile<T>(string path, T[] array)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(array.Length);
                foreach (var item in array)
                {
                    WriteItem(writer, item);
                }
                writer.Flush();
                fs.Close();
            }
        }

        public static T[] LoadFromFileAsArray<T>(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                T[] array = new T[count];
                for (int i = 0; i < count; i++)
                {
                    array[i] = ReadItem<T>(reader);
                }
                return array;
            }
        }

        // 新增：NativeArray 支持
        public static void SaveToFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(nativeArray.Length);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    WriteItem(writer, nativeArray[i]);
                }
                writer.Flush();
                fs.Close();
            }
        }

        public static NativeArray<T> LoadFromFileAsNativeArray<T>(string path) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                var nativeArray = new NativeArray<T>(count, Allocator.Persistent);
                for (int i = 0; i < count; i++)
                {
                    nativeArray[i] = ReadItem<T>(reader);
                }
                return nativeArray;
            }
        }

        // 新增：NativeArray 临时分配版本
        public static NativeArray<T> LoadFromFileAsNativeArrayTemp<T>(string path) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                var nativeArray = new NativeArray<T>(count, Allocator.Temp);
                for (int i = 0; i < count; i++)
                {
                    nativeArray[i] = ReadItem<T>(reader);
                }
                return nativeArray;
            }
        }

        // 新增：NativeArray 任务分配版本
        public static NativeArray<T> LoadFromFileAsNativeArrayJob<T>(string path) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                var nativeArray = new NativeArray<T>(count, Allocator.TempJob);
                for (int i = 0; i < count; i++)
                {
                    nativeArray[i] = ReadItem<T>(reader);
                }
                return nativeArray;
            }
        }

        // 新增：从数组加载到现有 List
        public static void LoadFromFile<T>(string path, T[] array)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                if (count != array.Length)
                {
                    throw new ArgumentException($"Array length mismatch. Expected {array.Length}, but file contains {count} elements.");
                }
                for (int i = 0; i < count; i++)
                {
                    array[i] = ReadItem<T>(reader);
                }
            }
        }

        // 新增：从文件加载到现有 NativeArray
        public static void LoadFromFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                if (count != nativeArray.Length)
                {
                    throw new ArgumentException($"NativeArray length mismatch. Expected {nativeArray.Length}, but file contains {count} elements.");
                }
                for (int i = 0; i < count; i++)
                {
                    nativeArray[i] = ReadItem<T>(reader);
                }
            }
        }

        private static void WriteItem<T>(BinaryWriter writer, T item)
        {
            switch (item)
            {
                case int v: writer.Write(v); break;
                case uint v: writer.Write(v); break;
                case float v: writer.Write(v); break;
                case double v: writer.Write(v); break;
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
                default: throw new NotSupportedException($"Unsupported type: {typeof(T)}");
            }
        }

        private static T ReadItem<T>(BinaryReader reader)
        {
            object value;
            Type type = typeof(T);

            if (type == typeof(int)) value = reader.ReadInt32();
            else if (type == typeof(uint)) value = reader.ReadUInt32();
            else if (type == typeof(float)) value = reader.ReadSingle();
            else if (type == typeof(double)) value = reader.ReadDouble();
            else if (type == typeof(Vector2)) value = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(Vector3)) value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(Vector4)) value = new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(Quaternion)) value = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(Color)) value = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(float2)) value = new float2(reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(float3)) value = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(float4)) value = new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            else if (type == typeof(double2)) value = new double2(reader.ReadDouble(), reader.ReadDouble());
            else if (type == typeof(double3)) value = new double3(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
            else if (type == typeof(double4)) value = new double4(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
            else throw new NotSupportedException($"Unsupported type: {type}");

            return (T)value;
        }

        public static async UniTask<List<T>> LoadFromStreamingAssetsAsync<T>(string fileName)
        {
            string url = StreamingAssetsHelper.GetStreamingAssetsURL(fileName);
            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[BinarySerializer] 加载失败: {request.error}");
                return new List<T>();
            }

            using var ms = new MemoryStream(request.downloadHandler.data);
            using var reader = new BinaryReader(ms);
            int count = reader.ReadInt32();
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadItem<T>(reader));
            }
            return list;
        }

        // 新增：异步加载数组
        public static async UniTask<T[]> LoadFromStreamingAssetsAsArrayAsync<T>(string fileName)
        {
            string url = StreamingAssetsHelper.GetStreamingAssetsURL(fileName);
            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[BinarySerializer] 加载失败: {request.error}");
                return new T[0];
            }

            using var ms = new MemoryStream(request.downloadHandler.data);
            using var reader = new BinaryReader(ms);
            int count = reader.ReadInt32();
            var array = new T[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = ReadItem<T>(reader);
            }
            return array;
        }

        // 新增：异步加载 NativeArray
        public static async UniTask<NativeArray<T>> LoadFromStreamingAssetsAsNativeArrayAsync<T>(string fileName) where T : unmanaged
        {
            string url = StreamingAssetsHelper.GetStreamingAssetsURL(fileName);
            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[BinarySerializer] 加载失败: {request.error}");
                return new NativeArray<T>(0, Allocator.Persistent);
            }

            using var ms = new MemoryStream(request.downloadHandler.data);
            using var reader = new BinaryReader(ms);
            int count = reader.ReadInt32();
            var nativeArray = new NativeArray<T>(count, Allocator.Persistent);
            for (int i = 0; i < count; i++)
            {
                nativeArray[i] = ReadItem<T>(reader);
            }
            return nativeArray;
        }
    }

    public static class UnsafeBinarySerializer
    {
        /// <summary>
        /// 序列化 List<T> 到二进制文件
        /// </summary>
        public static void SaveToFile<T>(string path, List<T> list) where T : unmanaged
        {
            if (list == null || list.Count == 0)
                throw new ArgumentException("List is null or empty");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // 写入数量
            int count = list.Count;
            bw.Write(count);

            // 临时 NativeArray 包装数据
            using var nativeArray = new NativeArray<T>(list.ToArray(), Allocator.Temp);
            var byteSize = count * UnsafeUtility.SizeOf<T>();
            unsafe
            {

                var ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nativeArray);

                // 写入裸数据
                var bytes = new byte[byteSize];
                UnsafeUtility.MemCpy(PinnedArray<byte>(bytes), ptr, byteSize);
                bw.Write(bytes);
            }
            bw.Flush();
            fs.Close();
        }

        /// <summary>
        /// 从二进制文件反序列化 List<T>
        /// </summary>
        public static List<T> LoadFromFile<T>(string path) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, Allocator.Temp);
            unsafe
            {

                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
                list.Add(result[i]);

            result.Dispose();
            return list;
        }

        // 新增：数组支持
        public static void SaveToFile<T>(string path, T[] array) where T : unmanaged
        {
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array is null or empty");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int count = array.Length;
            bw.Write(count);

            var byteSize = count * UnsafeUtility.SizeOf<T>();
            unsafe
            {
                var ptr = PinnedArray(array);
                var bytes = new byte[byteSize];
                UnsafeUtility.MemCpy(PinnedArray<byte>(bytes), ptr, byteSize);
                bw.Write(bytes);
            }
            bw.Flush();
            fs.Close();
        }

        public static T[] LoadFromFileAsArray<T>(string path) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new T[count];
            unsafe
            {
                var ptr = PinnedArray(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }

        // 新增：NativeArray 支持
        public static void SaveToFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            if (!nativeArray.IsCreated || nativeArray.Length == 0)
                throw new ArgumentException("NativeArray is not created or empty");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int count = nativeArray.Length;
            bw.Write(count);

            var byteSize = count * UnsafeUtility.SizeOf<T>();
            unsafe
            {
                var ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nativeArray);
                var bytes = new byte[byteSize];
                UnsafeUtility.MemCpy(PinnedArray<byte>(bytes), ptr, byteSize);
                bw.Write(bytes);
            }
            bw.Flush();
            fs.Close();
        }

        public static NativeArray<T> LoadFromFileAsNativeArray<T>(string path) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, Allocator.Persistent);
            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }

        public static NativeArray<T> LoadFromFileAsNativeArrayTemp<T>(string path) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, Allocator.Temp);
            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }

        public static NativeArray<T> LoadFromFileAsNativeArrayJob<T>(string path) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, Allocator.TempJob);
            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }

        // 新增：加载到现有数组
        public static void LoadFromFile<T>(string path, T[] array) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            if (count != array.Length)
            {
                throw new ArgumentException($"Array length mismatch. Expected {array.Length}, but file contains {count} elements.");
            }

            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            unsafe
            {
                var ptr = PinnedArray(array);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }
        }

        // 新增：加载到现有 NativeArray
        public static void LoadFromFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            if (count != nativeArray.Length)
            {
                throw new ArgumentException($"NativeArray length mismatch. Expected {nativeArray.Length}, but file contains {count} elements.");
            }

            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(nativeArray);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }
        }

        /// <summary>
        /// 将 byte[] 固定为内存指针（仅作用于 MemCpy）
        /// </summary>
        private static unsafe void* PinnedArray<T>(T[] array) where T : unmanaged
        {
            fixed (void* ptr = array)
                return ptr;
        }

        public static async UniTask<List<T>> LoadFromStreamingAssetsAsync<T>(string fileName) where T : unmanaged
        {
            string url = StreamingAssetsHelper.GetStreamingAssetsURL(fileName);
            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[UnsafeBinarySerializer] 加载失败: {request.error}");
                return new List<T>();
            }

            byte[] data = request.downloadHandler.data;
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            int count = reader.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = reader.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, Allocator.Temp);
            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray(bytes), byteSize);
            }

            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
                list.Add(result[i]);

            result.Dispose();
            return list;
        }

        // 新增：异步加载数组
        public static async UniTask<T[]> LoadFromStreamingAssetsAsArrayAsync<T>(string fileName) where T : unmanaged
        {
            string url = StreamingAssetsHelper.GetStreamingAssetsURL(fileName);
            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[UnsafeBinarySerializer] 加载失败: {request.error}");
                return new T[0];
            }

            byte[] data = request.downloadHandler.data;
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            int count = reader.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = reader.ReadBytes(byteSize);

            var result = new T[count];
            unsafe
            {
                var ptr = PinnedArray(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }

        // 新增：异步加载 NativeArray
        public static async UniTask<NativeArray<T>> LoadFromStreamingAssetsAsNativeArrayAsync<T>(string fileName) where T : unmanaged
        {
            string url = StreamingAssetsHelper.GetStreamingAssetsURL(fileName);
            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[UnsafeBinarySerializer] 加载失败: {request.error}");
                return new NativeArray<T>(0, Allocator.Persistent);
            }

            byte[] data = request.downloadHandler.data;
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            int count = reader.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = reader.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, Allocator.Persistent);
            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }
    }


    public static class StreamingAssetsHelper
    {
        public static string GetStreamingAssetsURL(string fileName)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return "file://" + Path.Combine(Application.streamingAssetsPath, fileName);
#elif UNITY_IOS
        return "file://" + Path.Combine(Application.dataPath + "/Raw", fileName);
#elif UNITY_ANDROID
        return "jar:file://" + Application.dataPath + "!/assets/" + fileName;
#else
        throw new PlatformNotSupportedException();
#endif
        }
    }

}