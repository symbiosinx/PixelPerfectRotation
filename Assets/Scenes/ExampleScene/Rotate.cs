using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour {

    void Start() {
        
    }

    void Update() {
        transform.Rotate(0f, 0f, Time.deltaTime*150f);
    }
}
