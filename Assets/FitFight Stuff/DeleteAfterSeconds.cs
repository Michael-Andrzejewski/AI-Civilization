using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeleteAfterSeconds : MonoBehaviour
{
    // Start is called before the first frame update
    public float seconds = 40f;
    public GameObject gb;
    void Start()
    {
        
        
        StartCoroutine(DeleteOnSeconds());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator DeleteOnSeconds()
    {
        yield return new WaitForSeconds(seconds);
        Destroy(gb);
        if (gb == null)
        {
            Destroy(gameObject);
        }
    }
}
