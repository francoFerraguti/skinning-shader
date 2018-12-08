using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class SkinningShader : MonoBehaviour
{
    private SkinnedMeshRenderer skinnedMeshRenderer = null;

    private Animation animation = null;
    private Mesh mesh = null;
    private Bone[] bones = null;
    private Vector4[] boneWeights = null;

    private int rootBoneIndex = 0;

    private MeshFilter meshFilter = null;
    private MeshRenderer meshRenderer = null;
    private Material material = null;

    private Matrix4x4[] matricesUniformBlock = null;
    private int shaderPropID_Matrices = 0;

    private float timer = 0.0f;

    private void Awake()
    {
        shaderPropID_Matrices = Shader.PropertyToID("_Matrices");
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        material = new Material(Shader.Find("Custom/SkinningShader"));
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        mesh = skinnedMeshRenderer.sharedMesh;
        material.CopyPropertiesFromMaterial(skinnedMeshRenderer.sharedMaterial);
        meshRenderer.sharedMaterial = material;
    }


    private void Start()
    {
        InitBones();
        ConstructBonesHierarchy();
        GetBoneWeights();
        FetchAnimationData();

        Mesh newMesh = CreateNewMesh(mesh, boneWeights);

        GameObject.Destroy(transform.Find("pelvis").gameObject);
        GameObject.Destroy(transform.Find("mutant_mesh").gameObject);
        Object.Destroy(gameObject.GetComponent<Animator>());
    }

    private void InitBones()
    {
        bones = new Bone[skinnedMeshRenderer.bones.Length];
        for (int i = 0; i < bones.Length; ++i)
        {
            Bone bone = new Bone();
            bones[i] = bone;
            bone.transform = skinnedMeshRenderer.bones[i];
            bone.bindpose = mesh.bindposes[i]; //la bindpose es la inversa de la matriz de transformación del hueso, cuando el hueso está en la bind pose
        }

        matricesUniformBlock = new Matrix4x4[bones.Length];
    }

    private void ConstructBonesHierarchy()
    {
        // Construct Bones' Hierarchy
        for (int i = 0; i < bones.Length; ++i)
        {
            if (bones[i].transform == skinnedMeshRenderer.rootBone)
            {
                rootBoneIndex = i;
                break;
            }
        }
        System.Action<Bone> CollectChildren = null;
        CollectChildren = (currentBone) =>
        {
            List<Bone> children = new List<Bone>();
            for (int j = 0; j < currentBone.transform.childCount; ++j)
            {
                Transform childTransform = currentBone.transform.GetChild(j);
                Bone childBone = GetBoneByTransform(childTransform);
                if (childBone != null)
                {
                    childBone.parent = currentBone;
                    children.Add(childBone);
                    CollectChildren(childBone);
                }
            }
            currentBone.children = children.ToArray();
        };
        CollectChildren(bones[rootBoneIndex]);
    }

    private void GetBoneWeights()
    {
        boneWeights = new Vector4[mesh.vertexCount];
        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            BoneWeight boneWeight = mesh.boneWeights[i];
            boneWeights[i].x = boneWeight.boneIndex0;
            boneWeights[i].y = boneWeight.weight0;
            boneWeights[i].z = boneWeight.boneIndex1;
            boneWeights[i].w = boneWeight.weight1;
        }
    }

    private void FetchAnimationData()
    {
        // Fetch animations' data
        int boneAnimationsCount = 0;
        animation = ScriptableObject.CreateInstance<Animation>();
        AnimationClip animClip = GetComponent<Animator>().runtimeAnimatorController.animationClips[0];
        animation.fps = 30;
        animation.name = animClip.name;
        animation.frames = new Frame[(int)(animClip.length * animation.fps)];
        animation.length = animClip.length;

        for (int frameIndex = 0; frameIndex < animation.frames.Length; ++frameIndex)
        {
            Frame frame = new Frame();
            animation.frames[frameIndex] = frame;
            float second = (float)(frameIndex) / (float)animation.fps;

            List<Bone> bones2 = new List<Bone>();
            List<Matrix4x4> matrices = new List<Matrix4x4>();
            List<string> bonesHierarchyNames = new List<string>();
            EditorCurveBinding[] curvesBinding = AnimationUtility.GetCurveBindings(animClip);
            foreach (var curveBinding in curvesBinding)
            {
                Bone bone = GetBoneByHierarchyName(curveBinding.path);

                if (bones2.Contains(bone))
                {
                    continue;
                }
                bones2.Add(bone);

                bonesHierarchyNames.Add(GetBoneHierarchyName(bone));

                AnimationCurve curveRX = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.x");
                AnimationCurve curveRY = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.y");
                AnimationCurve curveRZ = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.z");
                AnimationCurve curveRW = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.w");

                AnimationCurve curvePX = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalPosition.x");
                AnimationCurve curvePY = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalPosition.y");
                AnimationCurve curvePZ = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalPosition.z");

                float curveRX_v = curveRX.Evaluate(second);
                float curveRY_v = curveRY.Evaluate(second);
                float curveRZ_v = curveRZ.Evaluate(second);
                float curveRW_v = curveRW.Evaluate(second);

                float curvePX_v = curvePX.Evaluate(second);
                float curvePY_v = curvePY.Evaluate(second);
                float curvePZ_v = curvePZ.Evaluate(second);

                Vector3 translation = new Vector3(curvePX_v, curvePY_v, curvePZ_v);
                Quaternion rotation = new Quaternion(curveRX_v, curveRY_v, curveRZ_v, curveRW_v);
                NormalizeQuaternion(ref rotation);
                matrices.Add(
                    Matrix4x4.TRS(translation, rotation, Vector3.one)
                );
            }

            frame.bones = bones2.ToArray();
            frame.matrices = matrices.ToArray();
            frame.bonesHierarchyNames = bonesHierarchyNames.ToArray();
        }
    }

    private Mesh CreateNewMesh(UnityEngine.Mesh mesh, Vector4[] boneWeights)
    {
        Mesh newMesh = new Mesh();
        newMesh.vertices = mesh.vertices; //guarda los vertices
        newMesh.tangents = boneWeights; //guarda los weights de los huesos en la matriz de tangentes
        newMesh.uv = mesh.uv; //guarda los UVs
        newMesh.triangles = mesh.triangles; //guarda los triángulos
        meshFilter.sharedMesh = newMesh; //el meshFilter es lo que le pasa el mesh al meshRenderer
        return newMesh;
    }


    private void Update()
    {
        UpdateBoneAnimationMatrix(null, timer); //poniendo esta línea al principio, se queda en el primer frame. Osea que esto "avanza" la animación
        Play();
        timer += Time.deltaTime;
    }

    private void Play()
    {
        int numBones = bones.Length;
        for (int i = 0; i < numBones; ++i)
        {
            matricesUniformBlock[i] = bones[i].animationMatrix;
        }
        material.SetMatrixArray(shaderPropID_Matrices, matricesUniformBlock);
    }

    private void UpdateBoneAnimationMatrix(string animName, float time)
    {
        int frameIndex = (int)(time * animation.fps) % (int)(animation.length * animation.fps);
        Frame frame = animation.frames[frameIndex];

        UpdateBoneTransformMatrix(bones[rootBoneIndex], Matrix4x4.identity, frame);
    }

    private void UpdateBoneTransformMatrix(Bone bone, Matrix4x4 parentMatrix, Frame frame)
    {
        int index = frame.IndexOf(bone);
        Matrix4x4 mat = parentMatrix * frame.matrices[index];
        bone.animationMatrix = mat * bone.bindpose;

        Bone[] children = bone.children;
        for (int i = 0; i < children.Length; ++i)
        {
            UpdateBoneTransformMatrix(children[i], mat, frame);
        }
    }

    private Bone GetBoneByTransform(Transform transform)
    {
        foreach (Bone bone in bones)
        {
            if (bone.transform == transform)
            {
                return bone;
            }
        }
        return null;
    }

    private Bone GetBoneByHierarchyName(string hierarchyName)
    {
        System.Func<Bone, string, Bone> Search = null;
        Search = (bone, name) =>
        {
            if (name == hierarchyName)
            {
                return bone;
            }
            foreach (Bone child in bone.children)
            {
                Bone result = Search(child, name + "/" + child.name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        };

        return Search(bones[rootBoneIndex], bones[rootBoneIndex].name);
    }

    private string GetBoneHierarchyName(Bone bone)
    {
        string boneHierarchy = "";

        Bone currentBone = bone; //agarra el primer hueso
        while (currentBone != null)  //va asignándole el padre hasta llegar al hueso root
        {
            if (boneHierarchy == "") //si este es el primer hueso que chequeo, le asigno el nombre a boneHierarchy.
            {
                boneHierarchy = currentBone.name;
            }
            else //sino, le asigno el nuevo nombre / el viejo nombre
            {
                boneHierarchy = currentBone.name + "/" + boneHierarchy;
            }

            currentBone = currentBone.parent; //asigna el padre, que si es null rompe el ciclo
        }

        return boneHierarchy;
    }

    private void NormalizeQuaternion(ref Quaternion q) //normaliza los cuaterniones
    {
        float sum = 0;
        for (int i = 0; i < 4; ++i)
            sum += q[i] * q[i];
        float magnitudeInverse = 1 / Mathf.Sqrt(sum);
        for (int i = 0; i < 4; ++i)
            q[i] *= magnitudeInverse;
    }
}
