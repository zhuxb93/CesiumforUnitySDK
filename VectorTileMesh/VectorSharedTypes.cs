using System;
using UnityEngine;

namespace GeoTiles
{
    [Serializable]
    public class TextChunkImageModel
    {
        public string kind;
        public Sprite sprite;
        public Color faceColor;
    }

    public enum POIType
    {
        country,
        poi
    }

    [Serializable]
    public class VegaMeshMaterialProperty
    {
        public Mesh mesh;
        [ColorUsage(true, true)]
        public Color nightColor;
        public Material[] materials;
    }

    [Serializable]
    public class SelectVectorLayer
    {
        public VectorType vectorType;
        public Material material;
        [HideInInspector]
        public GameObject ParentTransform;
    }
}