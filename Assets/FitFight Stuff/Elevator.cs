using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevator : MonoBehaviour
{
    public GameObject objectToMove;
    public float distanceToMoveUp = 10f;
    public float speed = 5f;

    private Vector3 targetPosition;
    private bool reachedPosition = false;

    // Start is called before the first frame update
    void Start()
    {
        if (objectToMove == null)
        {
            objectToMove = gameObject.GetComponent<GameObject>();
        }
        targetPosition = objectToMove.transform.position + new Vector3(0, distanceToMoveUp, 0);
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!reachedPosition) //making this a bit more efficient, hopefully, so that it only has to check a boolean once it's done and
            //not keep trying to move back to the spot
        {
            float step = speed * Time.deltaTime;
            objectToMove.transform.position = Vector3.MoveTowards(objectToMove.transform.position, targetPosition, step);
            if (Vector3.Distance(objectToMove.transform.position, targetPosition) < 0.1)
            {
                reachedPosition = true;
            }
        }
        
    }
}
