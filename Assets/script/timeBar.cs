using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class timeBar : MonoBehaviour
{
    GameObject timebar;
    GameObject player;

    Image timebarImage;
    playerController playerScript;

    public int t = 16;          // 시간 막대의 총 칸 수 (16칸)
    public float tt = 7;        // 1칸 줄어드는 주기 (7초)
    private int maxT;

    public Sprite dangerImage;  // 1/3 이하일 때 바꿀 위험 이미지
    public bool isCleared = false; // 스테이지 클리어 시 타이머 정지 스위치

    void Start()
    {
        timebar = GameObject.Find("time");
        player = GameObject.Find("player");

        if (timebar != null) timebarImage = timebar.GetComponent<Image>();
        if (player != null) playerScript = player.GetComponent<playerController>();

        maxT = t;
    }

    void Update()
    {
        if (isCleared) return;

        tt -= Time.deltaTime;
        if (tt <= 0)
        {
            t -= 1;
            tt = 7;

            float currentRatio = (float)t / maxT;
            if (timebarImage != null)
            {
                timebarImage.fillAmount = currentRatio;

                if (currentRatio <= 1.0f / 3.0f && dangerImage != null)
                {
                    timebarImage.sprite = dangerImage;
                }
            }

            if (t <= 0)
            {
                if (playerScript != null) playerScript.Die();
            }
        }
    }

    // gameManager에서 남은 시간 칸 수를 가져갈 수 있는 함수
    public int GetRemainingTimeTicks()
    {
        return t;
    }

    // 클리어 시 일반 타이머 중지
    public void StopTimer()
    {
        isCleared = true;
    }
}