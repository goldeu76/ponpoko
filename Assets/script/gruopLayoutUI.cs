using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class gruopLayoutUI : MonoBehaviour
{
    public static gruopLayoutUI Instance { get; private set; }

    [Header("목숨 UI 설정")]
    public Transform lifeLayoutGroup;
    public GameObject lifePrefab;

    [Header("과일 UI 설정")]
    public Transform fruitLayoutGroup;
    public GameObject fruitPrefab;

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

    public void AddLife()
    {
        if (lifePrefab != null && lifeLayoutGroup != null)
        {
            Instantiate(lifePrefab, lifeLayoutGroup);
        }
    }

    public void LoseLife()
    {
        int currentLives = lifeLayoutGroup.childCount;
        if (currentLives > 0)
        {
            Destroy(lifeLayoutGroup.GetChild(currentLives - 1).gameObject);
            gameManager.Instance.LoseLife();
        }
    }

    public void AddFruit()
    {
        if (fruitPrefab != null && fruitLayoutGroup != null)
        {
            // 화면 우측 상단 UI 가판대에 과일 획득 시각 효과 아이콘 생성
            Instantiate(fruitPrefab, fruitLayoutGroup, false);

            // 씬 전환 판정 로직은 gameManager로 이사했으므로 여기서는 아무것도 하지 않습니다.
        }
    }

    public void RemoveFruit()
    {
        int currentFruits = fruitLayoutGroup.childCount;
        if (currentFruits > 0)
        {
            Destroy(fruitLayoutGroup.GetChild(currentFruits - 1).gameObject);
        }
    }

    private void Start()
    {
        foreach (Transform child in lifeLayoutGroup)
        {
            Destroy(child.gameObject);
        }

        // [추가] 새 스테이지 시작 시 이전 판의 과일 UI 찌꺼기도 깔끔하게 청소해 줍니다.
        foreach (Transform child in fruitLayoutGroup)
        {
            Destroy(child.gameObject);
        }

        if (gameManager.Instance != null)
        {
            int savedLives = gameManager.currentLives;
            for (int i = 0; i < savedLives; i++)
            {
                AddLife();
            }
        }
    }
}
