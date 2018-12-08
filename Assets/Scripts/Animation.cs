using UnityEngine;
using System.Collections;

public class Animation : ScriptableObject
{
    public string name = null;

    public Frame[] frames = null;

    public float length = 0; //en segundos

    public int fps = 0;
}
