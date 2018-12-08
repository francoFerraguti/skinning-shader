﻿using UnityEngine;
using System.Collections;

public class GPUSkinning_BoneAnimation : ScriptableObject
{
    public string animName = null;

    public GPUSkinning_BoneAnimationFrame[] frames = null;

    public float length = 0; /*in seconds*/

    public int fps = 0;
}
