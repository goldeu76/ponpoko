using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using UnityEngine;

public class titleMove : MonoBehaviour
{
    float timer;
    int waitingTime;
    bool inside = false;

    [SerializeField] float delay = 0f;
    float speed = -0.1f;
    [SerializeField] GameObject nuguri_1;
    [SerializeField] GameObject nuguri_2;
    [SerializeField] GameObject nuguri_3;
    [SerializeField] GameObject nuguri_4;
    [SerializeField] GameObject nuguri_5;
    [SerializeField] GameObject nuguri_6;
    [SerializeField] GameObject nuguri_7;
    [SerializeField] GameObject nuguri_8;
    // Start is called before the first frame update
    void Start()
    {
        timer = 0f;
        waitingTime = 2;
        
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.deltaTime;

        if ((timer > 0.1) && (timer < 0.1 + 2f))
        {
            nuguri_1.transform.Translate(new Vector3(speed, 0f, 0f));
            
        }
        if ((timer > 0.3) && (timer < 0.3 + 1.8f))
        {
            nuguri_2.transform.Translate(new Vector3(speed, 0f, 0f));
            
        }
        if ((timer > 0.5) && (timer < 0.5 + 1.6f))
        {
            nuguri_3.transform.Translate(new Vector3(speed, 0f, 0f));
            
        }
        if ((timer > 0.7) && (timer < 0.7 + 1.4f))
        {
            nuguri_4.transform.Translate(new Vector3(speed, 0f, 0f));
           
        }
        if ((timer > 0.9) && (timer < 0.9 + 1.2f))
        {
            nuguri_5.transform.Translate(new Vector3(speed, 0f, 0f));
            
        }
        if ((timer > 1.1) && (timer < 1.1 + 1f))
        {
            nuguri_6.transform.Translate(new Vector3(speed, 0f, 0f));
            
        }
        if ((timer > 1.3) && (timer < 1.3 + 0.8f))
        {
            nuguri_7.transform.Translate(new Vector3(speed, 0f, 0f));
            
        }
        if ((timer > 1.5) && (timer < 1.5 + 0.6f))
        {
            nuguri_8.transform.Translate(new Vector3(speed, 0f, 0f));
            
        }


    }
    

}
