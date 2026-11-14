using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class an3 : MonoBehaviour
{
    [Header("an2 스크립트 참조 (비워두면 자동으로 찾음)")]
    [SerializeField] an2 an2Script;

    [Header("너구리 8마리 게임 오브젝트 (왼쪽 1번 ~ 오른쪽 8번 순서)")]
    [SerializeField] Transform[] raccoons;

    [Header("월드 좌표(World Space) 이동 및 정렬 설정")]
    [SerializeField] float targetY = -0.3f;
    [SerializeField] float moveSpeed = 4.5f;
    [SerializeField] float firstStartX = 6.0f;
    [SerializeField] float startQueueSpacing = 1.5f;

    [SerializeField] float[] targetX = { -4.2f, -3.0f, -1.8f, -0.6f, 0.6f, 1.8f, 3.0f, 4.2f };

    [Header("애니메이션 클립 이름")]
    [SerializeField] string runAnimName = "run";
    [SerializeField] string danceAnimName = "dance";

    [Header("노래 및 씬 전환 설정")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip danceSongClip;
    [SerializeField] float danceDuration = 7.0f;
    [SerializeField] string nextSceneName = "stage1";

    [SerializeField] GameObject comment;

    // ==============================================================
    // 🚨 진짜 문제 원인
    // Update()는 매 프레임 실행되기 때문에:
    // 1. an2.an3가 true가 되는 그 프레임에 스페이스가 눌리면
    // 2. isStarted = true가 실행되기 전에 여러 번 진입할 수 있음
    // 3. 그래서 가끔 코루틴이 2개 실행 → 가끔 에러/멈춤 발생
    // ✅ 해결: 입력 감지 즉시 가장 먼저 잠가서 틈 자체를 없앰
    // ==============================================================

    private bool isRunning = false;      // ✅ 이름 변경: isStarted → isRunning (더 명확하게)
    private Coroutine activeRoutine;     // 실행 중인 코루틴 추적
    private bool isQuitting = false;     // 게임/씬 종료 중일 때 true

    [SerializeField] GameObject pon_set;

    void Start()
    {
        ResetState(); // 시작 시 완전 초기화
    }

    // 상태를 완전히 리셋하는 함수
    void ResetState()
    {
        isRunning = false;
        activeRoutine = null;
        isQuitting = false;

        // 너구리 숨기기
        if (raccoons != null)
        {
            foreach (var rac in raccoons)
            {
                if (rac != null) rac.gameObject.SetActive(false);
            }
        }

        // an2 자동 찾기
        if (an2Script == null)
        {
            an2Script = FindObjectOfType<an2>();
        }
    }

    void Update()
    {
        // ==============================================================
        // ✅ 가장 위에 둠: 이미 실행 중이거나 종료 중이면 아예 진행하지 않음
        // 이 라인이 제일 위에 있어야 틈이 생기지 않음
        // ==============================================================
        if (isRunning || isQuitting || an2Script == null)
            return;

        // 시작 조건 충족 + 스페이스 입력
        if (an2Script.an3 && Input.GetKeyDown(KeyCode.Space))
        {
            // ==============================================================
            // ✅ 진짜 핵심: 무엇보다도 제일 먼저 잠근다!
            // 이 한줄이 다른 모든 코드보다 위에 있어야 
            // 아무리 빨리 연타해도 절대 중복 실행되지 않음
            // ==============================================================
            isRunning = true;

            // 이전에 실행 중이던 코루틴이 있다면 완전히 정리
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            // 오브젝트 처리
            if (pon_set != null)
                pon_set.SetActive(false);

            // 새로운 코루틴 시작 및 추적 보관
            activeRoutine = StartCoroutine(DanceSequenceRoutine());
        }
    }

    private IEnumerator DanceSequenceRoutine()
    {
        // 시작 전 종료 체크
        if (isQuitting) yield break;

        // 배열 안전하게 처리
        int count = 0;
        if (raccoons != null && targetX != null)
        {
            count = Mathf.Min(raccoons.Length, targetX.Length);
        }

        Animator[] anims = new Animator[count];
        bool[] arrived = new bool[count];

        // ======================================
        // 1. 너구리 출발 위치 배치
        // ======================================
        for (int i = 0; i < count; i++)
        {
            if (isQuitting) yield break;
            if (raccoons[i] == null) continue;

            raccoons[i].gameObject.SetActive(true);

            float startX = firstStartX + (i * startQueueSpacing);
            raccoons[i].position = new Vector3(startX, targetY, 0f);

            Vector3 scale = raccoons[i].localScale;
            scale.x = Mathf.Abs(scale.x);
            raccoons[i].localScale = scale;

            anims[i] = raccoons[i].GetComponent<Animator>();
            if (anims[i] != null)
            {
                anims[i].speed = 1f;
                anims[i].Play(runAnimName, 0, 0f);
            }
        }

        // ======================================
        // 2. 목표 위치까지 이동
        // ======================================
        bool allDone = false;
        while (!allDone && !isQuitting)
        {
            allDone = true;

            for (int i = 0; i < count; i++)
            {
                if (isQuitting) yield break;
                if (raccoons[i] == null || arrived[i]) continue;

                Vector3 pos = raccoons[i].position;
                pos.x -= moveSpeed * Time.deltaTime;

                if (pos.x <= targetX[i])
                {
                    pos.x = targetX[i];
                    raccoons[i].position = pos;
                    arrived[i] = true;

                    if (anims[i] != null)
                    {
                        anims[i].Play(danceAnimName, 0, 0f);
                        anims[i].Update(0f);
                        anims[i].speed = 0f;
                    }
                }
                else
                {
                    raccoons[i].position = pos;
                    allDone = false;
                }
            }

            yield return null;
        }

        // ======================================
        // 3. 춤 준비 및 음악 재생
        // ======================================
        yield return new WaitForSeconds(0.3f);
        if (isQuitting) yield break;

        if (audioSource != null && danceSongClip != null)
        {
            if (comment != null)
                comment.SetActive(true);

            audioSource.PlayOneShot(danceSongClip);
            danceDuration = danceSongClip.length;
        }

        for (int i = 0; i < count; i++)
        {
            if (isQuitting) yield break;
            if (anims[i] != null)
            {
                anims[i].Play(danceAnimName, 0, 0f);
                anims[i].speed = 1f;
            }
        }

        // ======================================
        // 4. 춤 대기 후 씬 전환
        // ======================================
        yield return new WaitForSeconds(danceDuration);
        if (isQuitting) yield break;

        // 씬 전환 전 종료 플래그 설정
        isQuitting = true;

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // ==============================================================
    // 유니티 생명주기 이벤트 처리
    // ==============================================================

    void OnDestroy()
    {
        isQuitting = true;
        StopActiveRoutine();
    }

    void OnDisable()
    {
        StopActiveRoutine();
        // ❗ 주의: OnDisable에서 isRunning = false 하면 안 됨
        // 씬 전환 중에 리셋되면 접근 오류 생김
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    // 안전하게 코루틴 정리하는 전용 함수
    void StopActiveRoutine()
    {
        if (activeRoutine != null)
        {
            try
            {
                StopCoroutine(activeRoutine);
            }
            catch (System.Exception)
            {
                // 이미 멈춰있거나 파괴된 경우 무시
            }
            activeRoutine = null;
        }
    }
}
