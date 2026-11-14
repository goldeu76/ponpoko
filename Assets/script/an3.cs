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
    // ✅ 문제 원인 & 해결 목표
    // 원래 코드에서는 스페이스 바를 눌렀을 때 코루틴을 먼저 실행하고
    // 그 뒤에 isStarted = true 로 잠갔기 때문에, 그 짧은 사이에 
    // 스페이스를 또 누르면 코루틴이 여러 개 동시에 실행되어 
    // 같은 오브젝트를 중복으로 조작 → 충돌 → 런타임 에러 & 게임 멈춤 발생
    // 해결방법: isStarted = true 를 제일 먼저 실행해서 연타 입력 즉시 차단
    // ==============================================================

    private bool isStarted = false;       // 연타 방지 — 이미 실행 중이면 true
    private Coroutine danceRoutine;       // 실행 중인 코루틴 추적 — 중복 실행 시 기존 것 정리용
    private bool isDestroyed = false;     // 오브젝트가 파괴된 후에도 코루틴이 돌지 않도록 차단

    [SerializeField] GameObject pon_set;

    void Start()
    {
        // 초기화: 시작 시 상태를 깨끗하게 리셋
        isStarted = false;
        danceRoutine = null;
        isDestroyed = false;

        // 모든 너구리를 처음에는 화면에서 숨김 처리
        if (raccoons != null)
        {
            foreach (var rac in raccoons)
            {
                if (rac != null) rac.gameObject.SetActive(false);
            }
        }

        // an2 스크립트가 연결되지 않았다면 자동으로 찾아주기
        if (an2Script == null)
        {
            an2Script = FindObjectOfType<an2>();
        }
    }

    void Update()
    {
        // ==============================================================
        // ✅ 핵심: 이미 시작했거나 파괴 중이면 입력을 아예 받지 않음
        // 이 라인이 있어야 연타해도 코드가 실행되지 않고 차단됨
        // ==============================================================
        if (isStarted || isDestroyed || an2Script == null) return;

        // an2에서 시작 조건이 충족되고 스페이스 바가 눌리면 시작
        if (an2Script.an3 && Input.GetKeyDown(KeyCode.Space))
        {
            // ==============================================================
            // ✅ 가장 중요! 제일 먼저 잠금을 걸어버림
            // 이 뒤로는 스페이스를 아무리 눌러도 위쪽 조건문에서 걸려서 실행되지 않음
            // 원래 코드에서는 이게 제일 마지막에 있었기 때문에 문제가 생겼었음
            // ==============================================================
            isStarted = true;

            pon_set?.SetActive(false); // ?. → 오브젝트가 없어도 에러 안 남

            // 혹시 아직 이전 코루틴이 돌고 있다면 먼저 멈춰주기
            if (danceRoutine != null)
            {
                StopCoroutine(danceRoutine);
            }

            // 새로운 코루틴을 시작하고 참조를 보관 (나중에 정리하기 위해)
            danceRoutine = StartCoroutine(SafeIntroDanceRoutine());
        }
    }

    private IEnumerator SafeIntroDanceRoutine()
    {
        // 씬이 이미 파괴된 상태면 즉시 중단
        if (isDestroyed) yield break;

        int count = 0;
        if (raccoons != null && targetX != null)
        {
            count = Mathf.Min(raccoons.Length, targetX.Length);
        }

        Animator[] anims = new Animator[count];
        bool[] hasArrived = new bool[count];

        // 1. 너구리들을 화면에 보이게 하고 시작 위치에 배치 + 달리기 애니메이션 재생
        for (int i = 0; i < count; i++)
        {
            if (isDestroyed) yield break; // 중간에 파괴되면 즉시 탈출
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

        // 2. 각 너구리를 목표 위치까지 서서히 이동
        bool allArrived = false;
        while (!allArrived && !isDestroyed)
        {
            allArrived = true;

            for (int i = 0; i < count; i++)
            {
                if (isDestroyed) yield break;
                if (raccoons[i] == null || hasArrived[i]) continue;

                Vector3 pos = raccoons[i].position;
                pos.x -= moveSpeed * Time.deltaTime;

                if (pos.x <= targetX[i])
                {
                    pos.x = targetX[i];
                    raccoons[i].position = pos;
                    hasArrived[i] = true;

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
                    allArrived = false;
                }
            }

            yield return null;
        }

        // 3. 잠깐 대기 후 음악 재생 + 춤 애니메이션 시작
        yield return new WaitForSeconds(0.3f);
        if (isDestroyed) yield break;

        if (audioSource != null && danceSongClip != null)
        {
            comment?.SetActive(true);
            audioSource.PlayOneShot(danceSongClip);
            danceDuration = danceSongClip.length;
        }

        for (int i = 0; i < count; i++)
        {
            if (isDestroyed) yield break;
            if (anims[i] != null)
            {
                anims[i].Play(danceAnimName, 0, 0f);
                anims[i].speed = 1f;
            }
        }

        // 4. 음악 길이만큼 대기한 뒤 다음 씬으로 전환
        yield return new WaitForSeconds(danceDuration);
        if (isDestroyed) yield break;

        // 상태 리셋 — 다음에 다시 사용할 수 있도록
        isStarted = false;
        danceRoutine = null;

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // ==============================================================
    // ✅ 오브젝트가 파괴될 때 코루틴을 안전하게 정리
    // 씬 전환 중에 아직 돌고 있는 코루틴이 있으면 멈춰서 
    // "destroyed object was accessed" 에러 방지
    // ==============================================================
    void OnDestroy()
    {
        isDestroyed = true;

        if (danceRoutine != null)
        {
            StopCoroutine(danceRoutine);
            danceRoutine = null;
        }
    }

    // 오브젝트가 비활성화될 때도 상태 리셋
    void OnDisable()
    {
        if (danceRoutine != null)
        {
            StopCoroutine(danceRoutine);
            danceRoutine = null;
        }
        isStarted = false;
    }
}
