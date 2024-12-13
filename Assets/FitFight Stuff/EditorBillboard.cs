// Author:
// Stefan Guntermann apocx@gmx.de 
// 
// Description: 
// Attach Label Element to a GameObject. 
// Use: 
// gameObject.BroadcastMessage("setText", "foo"); 
// From: Parent GameObject

using UnityEngine;
using UnityEditor;
using System.Collections;

[ExecuteInEditMode()]
public class EditorBillboard : MonoBehaviour
{
    public bool face_MainCam = false;
    public bool hideWhenInvisible = false;
    public TextMesh t;

    // Use this for initialization
    void Start()
    {

        t = gameObject.GetComponent(typeof(TextMesh)) as TextMesh;

        t.alignment = TextAlignment.Center;

        setText(transform.position.x.ToString() + ", " + transform.position.z.ToString());
    }

    public void setText(string text)
    {

        t.text = text;
    }

    // Update is called once per frame
    void Update()
    {
        /*
        if (face_MainCam)
        {
            // the  Label is facing in the way of the camera
            //transform.LookAt( Camera.main.transform  );

            /*
            if (SceneView.lastActiveSceneView != null)
            {
                //print(SceneView.lastActiveSceneView.pivot);
                transform.LookAt(SceneView.currentDrawingSceneView.pivot);
            }
            else
            {
                //print(Camera.main.transform.position);
                transform.LookAt(Camera.main.transform.position/*, -Vector3.up*/ //);
            //}


        transform.LookAt(Camera.main.transform.position);

        // Flip the Label so be readable
        transform.rotation = transform.rotation * new Quaternion(0, 180, 0, 0);
        
    }

}