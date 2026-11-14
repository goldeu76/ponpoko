using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class anm1 : MonoBehaviour
{
    public static anm1 Instance { get; private set; }

    [Header("과일 UI 설정")]
    public Transform fruitLayoutGroup;
    public GameObject fruitPrefab;

    public GameObject an1;

    public GameObject text1;
    public GameObject text2;

    public bool next1 = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddFruit()
    {
        if (fruitPrefab != null && fruitLayoutGroup != null)
        {
            Instantiate(fruitPrefab, fruitLayoutGroup, false);
        }
    }

    private void Start()
    {
        // 테스트용으로 831개 생성
        for (int i = 0; i < 650; i++)
        {
            AddFruit();
        }

        // 게임 시작과 동시에 로딩바처럼 부드럽게 지우는 코루틴 실행
        StartCoroutine(RemoveFruitsRoutine());
    }


    IEnumerator RemoveFruitsRoutine()
    {
        // 자식 오브젝트가 하나라도 남아있는 동안 반복
        while (fruitLayoutGroup.childCount > 0)
        {
            // 이번 프레임에 지울 개수 결정 (남은 개수가 20개보다 적으면 남은 만큼만)
            int removeCount = Mathf.Min(20, fruitLayoutGroup.childCount);

            for (int i = 0; i < removeCount; i++)
            {
                // 항상 가장 마지막 자식의 인덱스를 안전하게 가져옴 (인덱스 에러 방지)
                int lastIndex = fruitLayoutGroup.childCount - 1;

                if (lastIndex >= 0)
                {
                    Destroy(fruitLayoutGroup.GetChild(lastIndex).gameObject);
                }
            }

            // 다음 프레임까지 대기 (이 줄 덕분에 게임이 멈추지 않고 로딩바처럼 자연스럽게 줄어듦)
            // 만약 너무 빨리 줄어든다면 yield return new WaitForSeconds(0.05f);로 딜레이
            yield return null;
        }

        Debug.Log("a");
        next1 = true;
        an1.SetActive(false);
        this.gameObject.SetActive(false);
        
        
    }
}