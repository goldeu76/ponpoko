using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JarSpawnEnemyController : MonoBehaviour
{
    // Header 속성 개념
    // 에디터에서 변수들을 시각적으로 묶어주는 기능임
    [Header("이동 설정")]
    [SerializeField] float speed = 3.0f;

    // 처음에 움직일 방향을 문자열로 지정함 왼쪽 또는 오른쪽
    [SerializeField] string initialDirection = "left";

    // 몬스터가 제자리에서 왕복할 수 있는 최대 거리 제한
    [SerializeField] float range = 0.0f;

    [Header("스폰 연출 설정")]
    // 몬스터가 생성되자마자 바로 움직이지 않고 제자리에서 대기하며 깜빡일 시간
    [SerializeField] float spawnWaitTime = 3f;
    [SerializeField] float blinkInterval = 0.15f;

    private Rigidbody2D rbody;

    // 몬스터가 여러 부위로 나뉘어 있을 수 있으니 자식 객체들에 붙은 스프라이트 렌더러까지 전부 배열로 담아둘 변수임
    private SpriteRenderer[] spriteRenderers;
    private Collider2D myCollider;
    private string currentDirection;
    private Vector3 defPos;

    // 상태 제어 플래그 변수
    // 현재 스폰 연출 중인지 아닌지 상태를 체크해서 연출 중일 때는 물리 엔진이나 이동 로직이 안 돌아가게 꽉 막아주는 핵심 안전 장치임
    private bool isSpawning = true;
    Animator animator;
    void Start()
    {
        // 캐싱 처리
        rbody = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();

        // GetComponentsInChildren 개념
        // 자기 자신뿐만 아니라 하위 자식 오브젝트들에 붙어있는 지정된 컴포넌트들을 싹 다 찾아서 배열로 묶어주는 유용한 기능임
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        currentDirection = initialDirection;

        if (currentDirection.Equals("right"))
        {
            // 스케일의 x값을 마이너스 1로 주면 이미지가 오른쪽을 보게 좌우 반전됨
            transform.localScale = new Vector2(-1, 1);
        }
        else
        {
            transform.localScale = new Vector2(1, 1);
        }

        // 코루틴 시작
        StartCoroutine(SpawnRoutine());
    }

    // 코루틴 Coroutine 개념
    // 메인 흐름을 잠깐 멈췄다가 나중에 이어서 실행할 수 있게 해주는 비동기
    IEnumerator SpawnRoutine()
    {
        isSpawning = true;

        // Physics2D.IgnoreCollision 개념
        // 몬스터가 깜빡이는 무적 시간 동안 플레이어와 부딪혀서 데미지를 주지 않도록 물리적인 "충돌 자체"를 아예 꺼버리는 예외 처리
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Collider2D playerCollider = null;
        if (player != null && myCollider != null)
        {
            playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider != null)
            {
                // 충돌 무시 켜기
                Physics2D.IgnoreCollision(playerCollider, myCollider, true);
            }
        }

        float timer = spawnWaitTime;
        float blinkTimer = 0f;
        bool isAlphaVisible = true;

        // while 루프를 이용한 타이머 로직
        // Time.deltaTime을 계속 빼주면서 지정된 시간이 다 지날 때까지 이 안에서 무한루프로 대기함
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            blinkTimer += Time.deltaTime;

            if (rbody != null)
            {
                // 스폰 연출 중에는 외부에서 힘을 받아도 밀려나지 않게 속도를 계속 0으로 덮어씌워서 제자리에 꽉 고정시킴
                rbody.velocity = new Vector2(0, rbody.velocity.y);
            }

            // 알파값 투명도 조절 로직
            // 깜빡이는 주기가 될 때마다 알파값을 1 불투명 과 0.3 반투명 사이로 왔다 갔다 스위칭해줌
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                isAlphaVisible = !isAlphaVisible;

                // foreach 반복문으로 배열 안에 모아둔 모든 스프라이트 부위의 투명도를 한 번에 싹 다 적용함
                foreach (var sr in spriteRenderers)
                {
                    if (sr != null)
                    {
                        Color color = sr.color;
                        color.a = isAlphaVisible ? 1f : 0.3f;
                        sr.color = color;
                    }
                }
            }

            // 다음 프레임까지 대기하면서 게임이 안 멈추고 계속 돌아가게 양보해줌
            yield return null;
        }

        // 스폰 연출이 다 끝났으니까 모든 스프라이트의 알파값을 1로 완벽하게 덮어서 원래대로 복구함
        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
            {
                Color color = sr.color;
                color.a = 1f;
                sr.color = color;
                animator.SetTrigger("spawned");
            }
        }

        if (playerCollider != null && myCollider != null)
        {
            // 이제 몬스터가 진짜로 활동을 시작하니까 아까 꺼뒀던 플레이어와의 물리 충돌 판정을 다시 정상으로 살려줌
            Physics2D.IgnoreCollision(playerCollider, myCollider, false);
        }

        // 몬스터가 스폰된 최종 월드 좌표를 defPos에 저장해서 나중에 왕복 범위 이동을 계산할 때 기준점으로 씀
        defPos = transform.position;
        isSpawning = false;
    }

    void Update()
    {
        // Early Return 기법
        // 아직 스폰 연출이 안 끝났으면 아래쪽 이동 계산 로직을 아예 쳐다보지도 않고 바로 빠져나와서 논리적인 꼬임을 원천 차단함
        if (isSpawning) return;

        if (range > 0.0f)
        {
            if (transform.position.x < defPos.x - (range / 2))
            {
                currentDirection = "right";
                transform.localScale = new Vector2(-1, 1);
            }
            if (transform.position.x > defPos.x + (range / 2))
            {
                currentDirection = "left";
                transform.localScale = new Vector2(1, 1);
            }
        }
    }

    private void FixedUpdate()
    {
        // 널 체크 방어 로직
        // 리지드바디가 날아갔거나 삭제됐을 때 치명적인 널 참조 에러가 터지는 걸 막음
        if (rbody == null) return;

        if (isSpawning)
        {
            // 물리 주기가 도는 FixedUpdate에서도 스폰 중일 땐 절대 못 움직이게 속도를 0으로 강제 고정하는 이중 안전 장치임
            rbody.velocity = new Vector2(0, rbody.velocity.y);
            return;
        }

        if (currentDirection.Equals("right"))
        {
            rbody.velocity = new Vector2(speed, rbody.velocity.y);
        }
        else
        {
            rbody.velocity = new Vector2(-speed, rbody.velocity.y);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.tag == "wall")
        {
            // 트리거 충돌 처리도 스폰 중일 땐 완전히 무시해서 몬스터가 벽에 끼어 연출이 고장나는 버그를 막음
            if (isSpawning) return;

            if (currentDirection.Equals("right"))
            {
                currentDirection = "left";
                transform.localScale = new Vector2(1, 1);
            }
            else
            {
                currentDirection = "right";
                transform.localScale = new Vector2(-1, 1);
            }
        }
       
    }
}