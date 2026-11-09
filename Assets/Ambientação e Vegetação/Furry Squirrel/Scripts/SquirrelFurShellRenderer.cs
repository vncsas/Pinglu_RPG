using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SquirrelAsset
{
    /// <summary>
    /// Lightweight, standalone shell-fur renderer shipped with the Squirrel asset.
    /// It intentionally has no dependency on external fur components, profiles, painters or reducers.
    /// Assign a material using one of the shaders under Squirrel/Fur Shell.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Squirrel/Fur Shell Renderer")]
    public sealed class SquirrelFurShellRenderer : MonoBehaviour
    {
        const string ChildName = "__SquirrelFurShells";

        [Tooltip("Optional source renderer. When empty, the Renderer on this GameObject is used.")]
        public Renderer sourceRenderer;
        [Tooltip("Material using Squirrel/Fur Shell URP or Squirrel/Fur Shell HDRP.")]
        public Material furMaterial;
        [Range(1, 32)] public int shellCount = 16;
        [Min(0f)] public float maxFurLength = 0.05f;
        [Min(0f)] public float surfaceOffset = 0f;

        [System.NonSerialized] GameObject generatedChild;
        [System.NonSerialized] Mesh generatedMesh;
        [System.NonSerialized] Mesh builtSource;
        [System.NonSerialized] int builtShellCount;
        [System.NonSerialized] float builtOffset;

        void OnEnable() => Rebuild();
        void OnValidate() => Rebuild();
        void OnDisable() => DestroyGenerated();
        void OnDestroy() => DestroyGenerated();

        [ContextMenu("Rebuild Shell Fur")]
        public void Rebuild()
        {
            Renderer source = sourceRenderer != null ? sourceRenderer : GetComponent<Renderer>();
            Mesh sourceMesh = GetMesh(source, out bool skinned);
            if (sourceMesh == null || !sourceMesh.isReadable || furMaterial == null)
            {
                DestroyGenerated();
                return;
            }

            int shells = Mathf.Clamp(shellCount, 1, 32);
            float offset = Mathf.Max(0f, surfaceOffset);
            bool needsMesh = generatedMesh == null || builtSource != sourceMesh ||
                             builtShellCount != shells || !Mathf.Approximately(builtOffset, offset);
            if (needsMesh)
            {
                DestroyMesh();
                generatedMesh = BuildShellMesh(sourceMesh, shells, Mathf.Max(0f, maxFurLength), offset, skinned);
                builtSource = sourceMesh;
                builtShellCount = shells;
                builtOffset = offset;
            }

            EnsureChild(source, skinned);
        }

        static Mesh GetMesh(Renderer renderer, out bool skinned)
        {
            skinned = renderer is SkinnedMeshRenderer;
            if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (renderer is MeshRenderer mr)
            {
                MeshFilter filter = mr.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            return null;
        }

        void EnsureChild(Renderer source, bool skinned)
        {
            if (generatedChild == null)
            {
                Transform existing = transform.Find(ChildName);
                generatedChild = existing != null ? existing.gameObject : new GameObject(ChildName);
                generatedChild.transform.SetParent(transform, false);
            }
            generatedChild.hideFlags = HideFlags.HideAndDontSave;

            if (skinned)
            {
                SkinnedMeshRenderer sourceSmr = source as SkinnedMeshRenderer;
                SkinnedMeshRenderer shellSmr = generatedChild.GetComponent<SkinnedMeshRenderer>();
                if (shellSmr == null) shellSmr = generatedChild.AddComponent<SkinnedMeshRenderer>();
                shellSmr.sharedMesh = generatedMesh;
                shellSmr.sharedMaterials = MaterialsFor(generatedMesh.subMeshCount);
                shellSmr.bones = sourceSmr.bones;
                shellSmr.rootBone = sourceSmr.rootBone;
                shellSmr.localBounds = generatedMesh.bounds;
                shellSmr.updateWhenOffscreen = true;
                CopyRendererSettings(source, shellSmr);
            }
            else
            {
                MeshFilter filter = generatedChild.GetComponent<MeshFilter>();
                if (filter == null) filter = generatedChild.AddComponent<MeshFilter>();
                MeshRenderer shellMr = generatedChild.GetComponent<MeshRenderer>();
                if (shellMr == null) shellMr = generatedChild.AddComponent<MeshRenderer>();
                filter.sharedMesh = generatedMesh;
                shellMr.sharedMaterials = MaterialsFor(generatedMesh.subMeshCount);
                CopyRendererSettings(source, shellMr);
            }
        }

        Material[] MaterialsFor(int subMeshCount)
        {
            var materials = new Material[Mathf.Max(1, subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = furMaterial;
            return materials;
        }

        static void CopyRendererSettings(Renderer source, Renderer destination)
        {
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.renderingLayerMask = source.renderingLayerMask;
            destination.gameObject.layer = source.gameObject.layer;
        }

        static Mesh BuildShellMesh(Mesh source, int shells, float maxLength, float offset, bool skinned)
        {
            int vertexCount = source.vertexCount;
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector4[] sourceTangents = source.tangents;
            Vector2[] sourceUv = source.uv;
            Vector2[] sourceUv1 = source.uv2;
            BoneWeight[] sourceWeights = skinned ? source.boneWeights : null;

            bool hasNormals = sourceNormals != null && sourceNormals.Length == vertexCount;
            bool hasTangents = sourceTangents != null && sourceTangents.Length == vertexCount;
            bool hasUv = sourceUv != null && sourceUv.Length == vertexCount;
            bool hasUv1 = sourceUv1 != null && sourceUv1.Length == vertexCount;
            bool hasWeights = sourceWeights != null && sourceWeights.Length == vertexCount;
            int total = vertexCount * shells;

            var vertices = new Vector3[total];
            var normals = new Vector3[total];
            var tangents = new Vector4[total];
            var uv0 = new Vector2[total];
            var uv1 = new List<Vector2>(total);
            var uv2 = new List<Vector2>(total);
            var weights = hasWeights ? new BoneWeight[total] : null;

            for (int shell = 0; shell < shells; shell++)
            {
                float layer = (shell + 1f) / shells;
                int baseIndex = shell * vertexCount;
                for (int i = 0; i < vertexCount; i++)
                {
                    int index = baseIndex + i;
                    Vector3 normal = hasNormals ? sourceNormals[i] : Vector3.up;
                    vertices[index] = sourceVertices[i] + normal * offset;
                    normals[index] = normal;
                    tangents[index] = hasTangents ? sourceTangents[i] : new Vector4(1f, 0f, 0f, 1f);
                    uv0[index] = hasUv ? sourceUv[i] : Vector2.zero;
                    uv1.Add(hasUv1 ? sourceUv1[i] : uv0[index]);
                    uv2.Add(new Vector2(layer, shell));
                    if (hasWeights) weights[index] = sourceWeights[i];
                }
            }

            var mesh = new Mesh { name = source.name + "_SquirrelFurShells", hideFlags = HideFlags.HideAndDontSave };
            mesh.indexFormat = total > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv0;
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            if (hasWeights)
            {
                mesh.boneWeights = weights;
                mesh.bindposes = source.bindposes;
            }

            mesh.subMeshCount = source.subMeshCount;
            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                int[] sourceTriangles = source.GetTriangles(subMesh);
                var triangles = new List<int>(sourceTriangles.Length * shells);
                for (int shell = 0; shell < shells; shell++)
                {
                    int baseIndex = shell * vertexCount;
                    for (int i = 0; i < sourceTriangles.Length; i++) triangles.Add(sourceTriangles[i] + baseIndex);
                }
                mesh.SetTriangles(triangles, subMesh, false);
            }

            Bounds bounds = source.bounds;
            bounds.Expand((maxLength + offset) * 2f);
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            return mesh;
        }

        void DestroyGenerated()
        {
            if (generatedChild != null)
            {
                DestroyTemporaryObject(generatedChild);
                generatedChild = null;
            }
            DestroyMesh();
            builtSource = null;
        }

        void DestroyMesh()
        {
            if (generatedMesh == null) return;
            DestroyTemporaryObject(generatedMesh);
            generatedMesh = null;
        }

        static void DestroyTemporaryObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
