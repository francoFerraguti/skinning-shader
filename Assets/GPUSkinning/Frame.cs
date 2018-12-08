using UnityEngine;
using System.Collections;

[System.Serializable]
public class Frame
{
    public Bone[] bones = null;

    public string[] bonesHierarchyNames = null;

    public Matrix4x4[] matrices = null;

    public int IndexOf(Bone bone)
    {
        for (int i = 0; i < bones.Length; ++i)
        {
            if (bones[i] == bone)
            {
                return i;
            }
        }

        return -1;
    }
}
