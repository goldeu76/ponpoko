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
    [SerializeField] float targetY = -0.3f;          // 너구리들이 서 있을 월드 Y좌표
    [SerializeField] float moveSpeed = 4.5f;         // 달려오는 속도 (유닛/초)
    [SerializeField] float firstStartX = 6.0f;       // 1번 너구리의 오른쪽 출발 월드 X좌표
    [SerializeField] float startQueueSpacing = 1.5f; // 출발 시 너구리들 사이의 줄 간격

    // 1920x1080 월드 좌표 기준 8마리 목표 X좌표 (-4.2 ~ +4.2, 1.2 유닛 간격)
    [SerializeField] float[] targetX = { -4.2f, -3.0f, -1.8f, -0.6f, 0.6f, 1.8f, 3.0f, 4.2f };

    [Header("애니메이션 클립 이름")]
    [SerializeField] string runAnimName = "run";     // 달려올 때 재생할 애니메이션 이름
    [SerializeField] string danceAnimName = "dance"; // 정렬 후 출 춤 애니메이션 이름

    [Header("노래 및 씬 전환 설정")]
    [SerializeField] AudioSource audioSource;        // 오디오 소스
    [SerializeField] AudioClip danceSongClip;        // 춤출 때 재생할 노래 파일
    [SerializeField] float danceDuration = 7.0f;     // 춤 지속 시간 (클립 연결 시 클립 길이로 자동 설정됨)
    [SerializeField] string nextSceneName = "stage1";// 춤이 끝난 뒤 넘어갈 씬 이름

    [SerializeField] GameObject comment;

    private bool isStarted = false;

    [SerializeField] GameObject pon_set;
    void Start()
    {
        // 1. 게임 시작 시 8마리 너구리를 무조건 안 보이게(꺼둠) 처리
        foreach (var rac in raccoons)
        {
            if (rac != null) rac.gameObject.SetActive(false);
        }

        // 2. an2 스크립트 자동 찾기
        if (an2Script == null)
        {
            an2Script = FindObjectOfType<an2>();
        }
    }

    void Update()
    {
        // an2의 an3가 true가 된 이후, 스페이스 바를 누르면 연출 시작
        if (!isStarted && an2Script != null && an2Script.an3 == true && Input.GetKeyDown(KeyCode.Space))
        {
            isStarted = true;
            pon_set.SetActive(false);
            StartCoroutine(IntroDanceRoutine());
        }
    }

    IEnumerator IntroDanceRoutine()
    {
        int count = Mathf.Min(raccoons.Length, targetX.Length);
        Animator[] anims = new Animator[count];
        bool[] hasArrived = new bool[count];

        // 1. 8마리 너구리를 화면에 켜고, 오른쪽 출발 위치에 줄 세운 뒤 'run' 애니메이션 재생
        for (int i = 0; i < count; i++)
        {
            if (raccoons[i] == null) continue;

            raccoons[i].gameObject.SetActive(true); // 이때 비로소 보임

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

        // 2. 동시에 왼쪽으로 이동하며 자기 타겟 좌표(targetX[i])에 도착하는 순서대로 정지
        bool allArrived = false;
        while (!allArrived)
        {
            allArrived = true;

            for (int i = 0; i < count; i++)
            {
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

        // 3. 0.3초 대기 후 노래 재생과 동시에 8마리 전원 'dance' 애니메이션 시작
        yield return new WaitForSeconds(0.3f);

        if (audioSource != null && danceSongClip != null)
        {
            comment.SetActive(true);
            audioSource.PlayOneShot(danceSongClip);
            danceDuration = danceSongClip.length;
        }

        for (int i = 0; i < count; i++)
        {
            if (anims[i] != null)
            {
                anims[i].Play(danceAnimName, 0, 0f);
                anims[i].speed = 1f;
            }
        }

        // 4. 노래가 끝날 때까지 춤을 춘 뒤 다음 씬으로 이동
        yield return new WaitForSeconds(danceDuration);
        SceneManager.LoadScene(nextSceneName);
    }
}