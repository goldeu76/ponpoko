using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class scoreUI : MonoBehaviour
{
    public static scoreUI Instance { get; private set; }

    [Header("UI 텍스트 컴포넌트 설정")]
    public Text scoreText;      // 기존 TOP SCORE용 텍스트 (또는 통합 텍스트)
    public Text oneUpText;      // 새로 추가할 1 UP 점수용 텍스트

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

    private void Start()
    {
        UpdateScoreDisplay();
    }

    // 데이터 동기화 및 화면 갱신 메서드
    public void UpdateScoreDisplay()
    {
        if (gameManager.Instance != null)
        {
            int totalScore = gameManager.Instance.accumulatedScore + gameManager.Instance.stageScore;

            // 1. 기존 TOP SCORE 텍스트 갱신 (리치 텍스트 방식 유지 또는 기존 방식)
            if (scoreText != null)
            {
                scoreText.text = "<color=#FFFFFF>TOP SCORE \n</color><color=#00FFFF>" + totalScore.ToString() + "</color>";
            }

            // 2. 새로 추가한 1 UP 점수 텍스트 갱신 (이미지처럼 1 UP과 점수를 각각 출력)
            if (oneUpText != null)
            {
                oneUpText.text = "<color=#FFFFFF>1 UP \n</color><color=#00FFFF>" + totalScore.ToString() + "</color>";
            }
        }
    }
}