using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class gameManager : MonoBehaviour
{
    public static gameManager Instance { get; private set; }

    public static int currentLives = 3;

    public int highScore = 0;
    public int accumulatedScore = 0;
    public int stageScore = 0;

    [Header("너구리 점수 시스템")]
    private readonly int[] fruitScoreTable = { 10, 20, 30, 50, 100, 200, 300, 500, 1000 };
    private int currentFruitCombo = 0;

    [Header("점수 이펙트 설정")]
    [SerializeField] private GameObject scoreEffectPrefab;
    [SerializeField] private Sprite[] scoreSprites;

    [Header("스테이지 클리어 연출 설정")]
    [Tooltip("과일 8개 획득 직후 보너스 화면이 뜨기 전 대기 시간(초)")]
    [SerializeField] private float delayBeforeBonusScreen = 0.6f;
    [Tooltip("BONUS 점수가 뜬 후 NEXT PATTERN 글씨가 뜰 때까지의 간격(초)")]
    [SerializeField] private float delayBeforeNextPattern = 1.0f;
    [Tooltip("NEXT PATTERN 표시 후 다음 씬으로 넘어갈 때까지 대기 시간(초)")]
    [SerializeField] private float bonusDisplayDuration = 2.0f;
    [Tooltip("클리어 시 재생할 효과음 (비워두면 재생 안 함)")]
    [SerializeField] private AudioClip stageClearClip;
    [Tooltip("직접 만든 폰트가 있다면 연결 (비워두면 기본 폰트 사용)")]
    [SerializeField] private Font customRetroFont;

    [Header("커스텀 클리어 UI (비워두면 코드에서 자동 생성)")]
    [SerializeField] private GameObject bonusScreenPanel;
    [SerializeField] private Text bonusScoreText;
    [SerializeField] private GameObject nextPatternObj;

    private bool isClearing = false;
    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnEatFruit(Vector3 spawnPosition)
    {
        if (isClearing) return;

        int index = Mathf.Clamp(currentFruitCombo, 0, fruitScoreTable.Length - 1);
        int earnScore = fruitScoreTable[index];
        AddScore(earnScore);
        currentFruitCombo++;

        SpawnScoreEffect(index, spawnPosition);

        Debug.Log($"과일 획득! 콤보: {currentFruitCombo}, 점수: {earnScore}");

        // 과일 8개를 다 먹으면 클리어 연출 시작
        if (currentFruitCombo >= 8)
        {
            ClearStage();
        }
    }

    public void OnEatJar(int targetScore, Vector3 spawnPosition)
    {
        if (isClearing) return;

        AddScore(targetScore);

        int spriteIndex = 0;
        switch (targetScore)
        {
            case 20: spriteIndex = 1; break;
            case 40: spriteIndex = 2; break;
            case 60: spriteIndex = 2; break;
            case 100: spriteIndex = 4; break;
            case 200: spriteIndex = 5; break;
            case 400: spriteIndex = 6; break;
            case 600: spriteIndex = 6; break;
            case 1000: spriteIndex = 8; break;
            default: spriteIndex = 0; break;
        }

        SpawnScoreEffect(spriteIndex, spawnPosition);
    }

    private void SpawnScoreEffect(int spriteIndex, Vector3 position)
    {
        if (scoreEffectPrefab != null && scoreSprites != null)
        {
            Vector3 spawnPos = position + new Vector3(0, 0.5f, 0);
            GameObject effectObj = Instantiate(scoreEffectPrefab, spawnPos, Quaternion.identity);

            ScoreEffect effectScript = effectObj.GetComponent<ScoreEffect>();
            if (effectScript != null)
            {
                effectScript.Setup(scoreSprites[spriteIndex]);
            }
        }
    }

    public void AddScore(int score)
    {
        stageScore += score;

        if (scoreUI.Instance != null)
        {
            scoreUI.Instance.UpdateScoreDisplay();
        }
    }

    public void LoseLife()
    {
        if (currentLives > 0)
        {
            currentLives--;
        }
    }

    public void CommitStageScore()
    {
        accumulatedScore += stageScore;
        stageScore = 0;
        currentFruitCombo = 0;

        if (accumulatedScore > highScore)
        {
            highScore = accumulatedScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();
        }
    }

    public void RollbackScore()
    {
        stageScore = 0;
        currentFruitCombo = 0;
    }

    public void LoadData()
    {
        highScore = PlayerPrefs.GetInt("HighScore", 0);
    }

    public void ResetAllData()
    {
        accumulatedScore = 0;
        stageScore = 0;
        currentLives = 3;
        currentFruitCombo = 0;
    }

    public int GetJarScore()
    {
        int index = Mathf.Clamp(currentFruitCombo, 0, fruitScoreTable.Length - 1);
        int currentFruitBaseScore = fruitScoreTable[index];
        int jarBonusScore = currentFruitBaseScore * 2;
        return jarBonusScore;
    }

    public int GetCurrentFruitCount()
    {
        return currentFruitCombo;
    }

    public void ClearStage()
    {
        if (isClearing) return;
        StartCoroutine(ClearStageRoutine());
    }

    // 원작처럼 화면에 BONUS 점수와 NEXT PATTERN을 띄우고 대기한 뒤 씬을 넘기는 코루틴
    private IEnumerator ClearStageRoutine()
    {
        isClearing = true;
        Debug.Log("스테이지 클리어!");

        // 1. 타이머 정지 및 남은 시간 보너스 계산
        int timeBonus = 0;
        timeBar timeBarScript = FindObjectOfType<timeBar>();
        if (timeBarScript != null)
        {
            timeBarScript.StopTimer();
            int remainingTicks = timeBarScript.GetRemainingTimeTicks();

            int currentStage = SceneManager.GetActiveScene().buildIndex;
            timeBonus = remainingTicks * (100 + (currentStage - 1) * 50);
        }

        // 2. 플레이어와 몬스터가 움직이지 못하도록 시간을 멈추고 플레이어 소리 정지
        Time.timeScale = 0f;
        playerController player = FindObjectOfType<playerController>();
        if (player != null)
        {
            AudioSource playerAudio = player.GetComponent<AudioSource>();
            if (playerAudio != null) playerAudio.Stop();
        }

        // 마지막 8번째 과일 점수(1000점) 이펙트를 잠깐 보여줌
        yield return new WaitForSecondsRealtime(delayBeforeBonusScreen);

        // 3. 보너스 UI가 없으면 자동 생성
        EnsureBonusUICreated();

        // 4. 검은 배경 및 "BONUS   5000" 먼저 표시 + 점수 합산 + 클리어 사운드 재생
        if (bonusScreenPanel != null) bonusScreenPanel.SetActive(true);
        if (nextPatternObj != null) nextPatternObj.SetActive(false);
        if (bonusScoreText != null) bonusScoreText.text = $"BONUS     {timeBonus}";

        AddScore(timeBonus);
        Debug.Log($"시간 보너스 획득: {timeBonus}점");

        if (stageClearClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(stageClearClip);
        }

        // 5. 약 1초 대기 후 "NEXT PATTERN" 문구 추가로 띄우기
        yield return new WaitForSecondsRealtime(delayBeforeNextPattern);
        if (nextPatternObj != null) nextPatternObj.SetActive(true);

        // 6. 남은 시간 동안 보너스 화면을 보여준 뒤 정리
        yield return new WaitForSecondsRealtime(bonusDisplayDuration);

        if (bonusScreenPanel != null) bonusScreenPanel.SetActive(false);
        Time.timeScale = 1f;

        // 7. 점수 확정 및 다음 스테이지 이동
        CommitStageScore();
        isClearing = false;

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex >= 11)
        {
            SceneManager.LoadScene("basic");
        }
        else if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            SceneManager.LoadScene("stage1");
        }
    }

    // 별도 UI를 연결하지 않았을 때 원작 스타일의 보너스 화면을 자동 생성하는 메서드
    private void EnsureBonusUICreated()
    {
        if (bonusScreenPanel != null) return;

        // gameManager 자식으로 전용 Canvas 생성 (DontDestroyOnLoad 함께 유지)
        GameObject canvasObj = new GameObject("BonusStageCanvas");
        canvasObj.transform.SetParent(this.transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // 맵은 가리고 필요시 조정 가능

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // 중앙 게임 화면을 덮는 검은색 패널 생성 (상단 점수 UI와 하단 목숨 UI가 보이도록 상하 여백 설정 가능)
        bonusScreenPanel = new GameObject("BonusScreenPanel");
        bonusScreenPanel.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bonusScreenPanel.AddComponent<Image>();
        bgImage.color = Color.black;

        RectTransform bgRect = bonusScreenPanel.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.12f); // 하단 목숨/크레딧 영역 남김
        bgRect.anchorMax = new Vector2(1f, 0.9f); // 상단 점수 영역 남김
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Font useFont = customRetroFont != null ? customRetroFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 1. "BONUS     5000" 텍스트 (원작 하늘색)
        GameObject bonusTextObj = new GameObject("BonusScoreText");
        bonusTextObj.transform.SetParent(bonusScreenPanel.transform, false);
        bonusScoreText = bonusTextObj.AddComponent<Text>();
        bonusScoreText.font = useFont;
        bonusScoreText.fontSize = 56;
        bonusScoreText.alignment = TextAnchor.MiddleCenter;
        bonusScoreText.color = new Color(0f, 1f, 1f); // Cyan (#00FFFF)

        RectTransform bonusRect = bonusTextObj.GetComponent<RectTransform>();
        bonusRect.anchorMin = new Vector2(0.5f, 0.5f);
        bonusRect.anchorMax = new Vector2(0.5f, 0.5f);
        bonusRect.sizeDelta = new Vector2(1000f, 100f);
        bonusRect.anchoredPosition = new Vector2(0f, 60f);

        // 2. "NEXT PATTERN" 텍스트 (원작 주황색)
        nextPatternObj = new GameObject("NextPatternText");
        nextPatternObj.transform.SetParent(bonusScreenPanel.transform, false);
        Text nextText = nextPatternObj.AddComponent<Text>();
        nextText.font = useFont;
        nextText.fontSize = 56;
        nextText.alignment = TextAnchor.MiddleCenter;
        nextText.color = new Color(1f, 0.55f, 0f); // Orange (#FF8C00)
        nextText.text = "NEXT PATTERN";

        RectTransform nextRect = nextPatternObj.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(0.5f, 0.5f);
        nextRect.anchorMax = new Vector2(0.5f, 0.5f);
        nextRect.sizeDelta = new Vector2(1000f, 100f);
        nextRect.anchoredPosition = new Vector2(0f, -60f);

        bonusScreenPanel.SetActive(false);
    }
}