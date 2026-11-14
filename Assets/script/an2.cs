using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class an2 : MonoBehaviour
{
    [SerializeField] GameObject text1;
    [SerializeField] GameObject text2;

    [SerializeField] GameObject pon_set;

    [SerializeField] GameObject score;

    [SerializeField] GameObject space;

    private bool isStarted = false; // 코루틴이 중복 실행되는 것을 막아주는 플래그 변수
    public bool an3 = false;
    void Update()
    {
        // anm1의 싱글톤 인스턴스가 존재하고, next1이 true이며, 아직 코루틴이 실행되지 않았을 때
        if (!isStarted && anm1.Instance != null && anm1.Instance.next1 == true)
        {
            isStarted = true; // 중복 실행 방지 잠금
            StartCoroutine(text_anm());
            StartCoroutine(alpha());
        }
        
    }

    IEnumerator text_anm()
    {
        text1.SetActive(true);
        yield return new WaitForSeconds(0.5f);
        text2.SetActive(true);
        yield return new WaitForSeconds(1f);
        text1.SetActive(false);
        text2.SetActive(false);
        pon_set.SetActive(true);
        score.SetActive(true);
        an3 = true;
        
    }

    IEnumerator alpha()
    {
        while (true)
        {
            if (!Input.GetKeyDown(KeyCode.Space))
            {
                space.SetActive(true);
                yield return new WaitForSeconds(0.5f);
                space.SetActive(false);
                yield return new WaitForSeconds(0.5f);
            }
            
        }
    }
}