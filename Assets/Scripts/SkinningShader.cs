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

        //Debug.Break();

        Object.Destroy(gameObject.GetComponent<Animator>());

        foreach (GameObject oldMeshes in GameObject.FindGameObjectsWithTag("Destructable"))
        {
            GameObject.Destroy(oldMeshes);
        }
    }

    private void InitBones()
    {
        bones = new Bone[skinnedMeshRenderer.bones.Length];
        Debug.Log("el modelo tiene " + skinnedMeshRenderer.bones.Length.ToString() + " huesos");
        for (int i = 0; i < bones.Length; ++i)
        {
            Bone bone = new Bone();
            bones[i] = bone;
            bone.transform = skinnedMeshRenderer.bones[i];
            bone.bindpose = mesh.bindposes[i]; //la bindpose es la inversa de la matriz de transformación del hueso, cuando el hueso está en la bind pose

            /*Debug.Log("hueso: " + bone.name + " tiene un transform de: posXYZ(" +
                                bone.transform.position.x + "," + bone.transform.position.y + "," + bone.transform.position.z +
                                "),rotXYZW(" + bone.transform.rotation.x + ", " + bone.transform.rotation.y + ", " + bone.transform.rotation.z + ", " + bone.transform.rotation.w + ")");*/
        }

        matricesUniformBlock = new Matrix4x4[bones.Length];
    }

    private void ConstructBonesHierarchy() //crea la jerarquía de huesos, empezando por el root bone
    {
        for (int i = 0; i < bones.Length; ++i)
        {
            if (bones[i].transform == skinnedMeshRenderer.rootBone)
            {
                Debug.Log("el hueso root es " + bones[i].name);
                rootBoneIndex = i;
                break;
            }
        }

        CollectChildren(bones[rootBoneIndex], true, true);
    }

    private void CollectChildren(Bone currentBone, bool checkChildren, bool checkParent) //settea en cada Bone sus children correspondientes
    {
        if (checkParent)
        {
            Transform parentTransform = currentBone.transform.parent;
            Bone parentBone = GetBoneByTransform(parentTransform);
            if (parentBone != null)
            {
                currentBone.parent = parentBone;
                Debug.Log("El padre de " + currentBone.name + " es " + parentBone.name);
                CollectChildren(parentBone, false, true);
            }
        }

        if (checkChildren)
        {
            List<Bone> children = new List<Bone>();
            for (int j = 0; j < currentBone.transform.childCount; ++j)
            {
                Transform childTransform = currentBone.transform.GetChild(j);
                Bone childBone = GetBoneByTransform(childTransform);
                if (childBone != null)
                {
                    if (checkChildren)
                    {
                        childBone.parent = currentBone;
                        children.Add(childBone);
                    }
                    CollectChildren(childBone, true, false);
                }
            }
            currentBone.children = children.ToArray();
        }
    }

    private void GetBoneWeights() //Devuelve los weights de cada Bone, sacados del mesh
    {
        boneWeights = new Vector4[mesh.vertexCount];
        Debug.Log("el modelo tiene " + mesh.vertexCount + " vértices");
        for (int i = 0; i < mesh.vertexCount; ++i)
        {
            BoneWeight boneWeight = mesh.boneWeights[i];
            //Debug.Log("el vértice " + i + " es modificado por el hueso " + boneWeight.boneIndex0 + " => " + boneWeight.weight0 + " y por el hueso " + boneWeight.boneIndex1 + " => " + boneWeight.weight1);
            boneWeights[i].x = boneWeight.boneIndex0;
            boneWeights[i].y = boneWeight.weight0;
            boneWeights[i].z = boneWeight.boneIndex1;
            boneWeights[i].w = boneWeight.weight1;
        }
    }

    private void FetchAnimationData()
    {
        animation = ScriptableObject.CreateInstance<Animation>();
        AnimationClip animClip = GetComponent<Animator>().runtimeAnimatorController.animationClips[0];
        animation.fps = 30;
        animation.name = animClip.name;
        animation.frames = new Frame[(int)(animClip.length * animation.fps)]; //con 30fps, si el clip dura 10 segundos, van a haber 300 frames
        animation.length = animClip.length;

        Debug.Log("animación: |FPS=" + animation.fps + "| |Nombre:" + animation.name + "| |Duración:" + animation.length + "|");

        for (int frameIndex = 0; frameIndex < animation.frames.Length; frameIndex++)
        {
            Debug.Log("calculando animación para frame " + frameIndex);

            Frame frame = new Frame();
            animation.frames[frameIndex] = frame;
            float second = (float)(frameIndex) / (float)animation.fps; //calcula el momento exacto en el que el frame va a reproducirse

            //Debug.Log("que se reproducirá en el segundo " + second);

            List<Bone> bonesAlreadyCalculated = new List<Bone>();
            List<Matrix4x4> matrices = new List<Matrix4x4>();
            List<string> bonesHierarchyNames = new List<string>();
            EditorCurveBinding[] curvesBinding = AnimationUtility.GetCurveBindings(animClip);

            foreach (var curveBinding in curvesBinding)
            {

                if (curveBinding.path == "Armature" || curveBinding.path == "Esqueleto")
                {
                    continue;
                }

                Bone bone = GetBoneByHierarchyName(curveBinding.path);

                if (bone == null || bonesAlreadyCalculated.Contains(bone))
                {
                    //Debug.Log("fin");
                    continue;
                }

                Debug.Log("1");

                bonesHierarchyNames.Add(GetBoneHierarchyName(bone));

                Debug.Log("2");

                AnimationCurve curveRotX = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.x");
                AnimationCurve curveRotY = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.y");
                AnimationCurve curveRotZ = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.z");
                AnimationCurve curveRotW = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalRotation.w");

                AnimationCurve curvePosX = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalPosition.x");
                AnimationCurve curvePosY = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalPosition.y");
                AnimationCurve curvePosZ = AnimationUtility.GetEditorCurve(animClip, curveBinding.path, curveBinding.type, "m_LocalPosition.z");

                float curvePX_v = curvePosX.Evaluate(second); //calcula las posiciones y rotaciones que tendrá el hueso en cada frame
                float curvePY_v = curvePosY.Evaluate(second);
                float curvePZ_v = curvePosZ.Evaluate(second);

                float curveRX_v = curveRotX.Evaluate(second);
                float curveRY_v = curveRotY.Evaluate(second);
                float curveRZ_v = curveRotZ.Evaluate(second);
                float curveRW_v = curveRotW.Evaluate(second);

                Vector3 translation = new Vector3(curvePX_v, curvePY_v, curvePZ_v); //las pone en Vector3 y Quaternion
                Quaternion rotation = new Quaternion(curveRX_v, curveRY_v, curveRZ_v, curveRW_v);
                NormalizeQuaternion(ref rotation); //normaliza los Quaternions, ya que se trabaja siempre sobre vectores normalizados
                matrices.Add(
                    Matrix4x4.TRS(translation, rotation, Vector3.one) //agrega los valores al array de matrices para pasarle después al shader
                );

                bonesAlreadyCalculated.Add(bone);

                //Debug.Log("hueso añadido correctamente: " + bone.name);
            }

            frame.bones = bonesAlreadyCalculated.ToArray();
            frame.matrices = matrices.ToArray();
            frame.bonesHierarchyNames = bonesHierarchyNames.ToArray(); //le agrega los huesos, las matrices de translación y rotación y la jerarquía al frame
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
        UpdateBoneAnimationMatrix(null, timer); //settea los datos para enviarle al shader
        PassMatrices();
        timer += Time.deltaTime;
    }

    private void PassMatrices()
    {
        int numBones = bones.Length;
        for (int i = 0; i < numBones; ++i)
        {
            matricesUniformBlock[i] = bones[i].animationMatrix; //agrega la matriz de transformación de la animación
        }
        material.SetMatrixArray(shaderPropID_Matrices, matricesUniformBlock); //le pasa los datos al shader
    }

    private void UpdateBoneAnimationMatrix(string animName, float time)
    {
        int frameIndex = (int)(time * animation.fps) % (int)(animation.length * animation.fps); // saca el porcentaje, que es el index del frame
        Frame frame = animation.frames[frameIndex]; //este es el frame en el que estamos

        UpdateBoneTransformMatrix(bones[rootBoneIndex], Matrix4x4.identity, frame); //updatea las matrices, como empieza con el rootBone le pasa la matriz identidad
    }

    private void UpdateBoneTransformMatrix(Bone bone, Matrix4x4 parentMatrix, Frame frame)
    {
        //Debug.Log("actualizando la matriz de transformación del hueso: " + bone.name + " - el frame actual tiene " + frame.bones.Length + " huesos");

        int index = frame.IndexOf(bone); //en que posición está el hueso dentro del array de huesos en Frame
        Matrix4x4 mat = parentMatrix * frame.matrices[index]; //multiplica a la matriz del padre por la del hueso en este frame, obteniendo 
        bone.animationMatrix = mat * bone.bindpose; //esta matriz que al multiplicarla al bindpose nos dará los valores correctos del frame

        Bone[] children = bone.children;
        for (int i = 0; i < children.Length; ++i)
        {
            UpdateBoneTransformMatrix(children[i], mat, frame);
        }
    }

    private Bone GetBoneByTransform(Transform transform) //usado para ir pasando hueso por hueso, a través de children
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

    private Bone SearchBone(Bone bone, string boneName, string hierarchyName, bool searchChildren, bool searchParent)
    {
        //Debug.Log("EMPEZANDO: " + bone.name + " - " + boneName + " - " + hierarchyName);
        if (boneName == hierarchyName || "Armature/" + boneName == hierarchyName || "Esqueleto/" + boneName == hierarchyName)
        {
            //Debug.Log("hueso encontrado normal: " + bone.name);
            return bone;
        }


        if (searchParent && bone.parent != null)
        {
            //Debug.Log("buscando en el padre de: " + bone.name + ": " + bone.parent.name);
            Bone parentResult = SearchBone(bone.parent, bone.parent.name + "/" + boneName, hierarchyName, false, true);
            if (parentResult != null)
            {
                //Debug.Log("hueso encontrado parent: " + parentResult.name);
                return parentResult;
            }
        }
        else
        {
            //Debug.Log("el hueso: " + bone.name + " no tiene padre");
        }

        if (searchChildren)
        {
            foreach (Bone child in bone.children)
            {
                //Debug.Log("buscando en su hijo: " + child.name);
                Bone childResult = SearchBone(child, boneName + "/" + child.name, hierarchyName, true, false);
                if (childResult != null)
                {
                    //Debug.Log("hueso encontrado child: " + childResult.name);
                    return childResult;
                }
            }
        }

        //Debug.Log("el segundo parámetro de SearchBone, boneName=" + boneName + ", no equivale al tercero, hierarchyName=" + hierarchyName + ".A su vez, tampoco ninguno de sus hijos");
        return null;
    }

    private Bone GetBoneByHierarchyName(string hierarchyName) //te devuelve el hueso con el nombre (de la jerarquía hasta llegar a ese hueso)
    {
        return SearchBone(bones[rootBoneIndex], bones[rootBoneIndex].name, hierarchyName, true, true);
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
