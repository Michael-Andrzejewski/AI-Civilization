using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomTeleportStart : MonoBehaviour
{
    public float teleportRadiusSize = 10f;
    // Start is called before the first frame update
    void Start()
    {
        Teleport(teleportRadiusSize);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Teleport(float teleportRadiusSize)
    {
        //teleport somewhere random in a 10 block radius
        transform.position += new Vector3(Random.Range(-teleportRadiusSize, teleportRadiusSize), 0, Random.Range(-teleportRadiusSize, teleportRadiusSize));
    }
}
