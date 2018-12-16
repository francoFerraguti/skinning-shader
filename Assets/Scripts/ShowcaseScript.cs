﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowcaseScript : MonoBehaviour
{
    float rotatingSpeed = 20.0f;

    void Update()
    {
        transform.Rotate(-Vector3.up * Time.deltaTime * rotatingSpeed);
    }
}
