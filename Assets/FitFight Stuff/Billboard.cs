using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Billboard : MonoBehaviour
{
    public Transform cam;


    // Start is called before the first frame update
    void Start()
    {
        if (cam == null)
        {
            cam = GameObject.FindGameObjectWithTag("MainCamera").transform;
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(transform.position + cam.forward);
    }
}
