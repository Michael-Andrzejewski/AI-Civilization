using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExpandOnStart : MonoBehaviour
{
    public float scaleSize = 5f;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Rescale());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    IEnumerator Rescale()
    {
        yield return new WaitForSeconds(0.1f);
        gameObject.transform.localScale *= scaleSize;
    }
}
