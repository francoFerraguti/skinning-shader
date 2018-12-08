using UnityEngine;
using System.Collections;

public class Bone
{
    public Transform transform = null;

    public Matrix4x4 bindpose;

    public Bone parent = null;

    public Bone[] children = null;

    public Matrix4x4 animationMatrix;

    public string name
    {
        get
        {
            return transform.gameObject.name;
        }
    }
}
