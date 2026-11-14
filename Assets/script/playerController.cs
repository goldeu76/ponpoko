using System.Collections;          // IEnumerator(코루틴 리턴형)을 쓰려면 반드시 필요
using UnityEngine;
using UnityEngine.SceneManagement; // SceneManager.LoadScene() 등 씬 전환용
using UnityEngine.Tilemaps;        // Tilemap 클래스(WorldToCell, HasTile 등)용

/*
 ================================================================================
  [playerController 한눈에 보기]  (나중에 볼 때 여기부터 읽기)
 ================================================================================
 1.스크립트 구조(메인루틴 실행 순서)
 (1)Start -> FixedUpdate -> Update -> LateUpdate -> FixedUpdate -> Update ... (코루틴 시트 0번 참고)
 (2)역할 분담 : Update = 입력받기/상태판정/애니메이션,  FixedUpdate = 물리(rb.velocity) 처리,  LateUpdate = 스프라이트 마무리 덮어쓰기
 (2-1)왜 나누나 : 입력은 매프레임 체크해야 안놓치고(Update), 물리는 일정 간격(기본0.02초)으로 계산되므로(FixedUpdate) 물리관련 값은 FixedUpdate에서 건드려야 떨림이 없다.

 2.이 스크립트에서 쓴 핵심 기법 목록 (아래 본문에 ※ 표시로 자세히 적어놨음)
 (1)Linecast / Raycast 로 바닥 감지 (LayerMask 사용)
 (2)Rigidbody2D 의 gravityScale 을 구간별로 바꿔서 점프 3단계(상승-정점-하강) 만들기
 (3)코루틴으로 점프 처리 + 애니메이션을 시간에 맞춰 수동 재생(anim.Play + anim.Update(0f))
 (4)사다리 : 트리거(isTrigger) 콜라이더 + 태그("ladder","laddertop") + 중앙 X 스냅
 (5)Tilemap 으로 사다리 중앙 X 구하기 (WorldToCell, HasTile, GetCellCenterWorld)
 (6)카운터 방식(ladderTopOverlapCount) / 히스테리시스(-0.05f) 로 트리거 깜빡임 방지
 (7)Bounds(col.bounds) 로 피벗과 상관없이 콜라이더 실제 발바닥 위치 구하기
 (8)Physics2D.Distance 로 점프중 "윗층 함정" 판정
 (9)층 판정(Y좌표 분기) + 사망 연출(중력 역산, 회전 애니메이션)

 3.이 스크립트 사용 전 인스펙터 체크리스트
 (1)Player 오브젝트에 Rigidbody2D, Collider2D(BoxCollider2D 권장), Animator, SpriteRenderer, AudioSource 필요 (Start에서 GetComponent 로 가져오므로 없으면 null 에러)
 (2)groundLayer 에 바닥 레이어 지정 (안하면 바닥감지 전부 실패 -> 점프도 안됨)
 (3)사다리 오브젝트 : isTrigger 체크 + 태그 "ladder" / 꼭대기 오브젝트 : isTrigger 체크 + 태그 "laddertop"
 (4)위험물 태그 : "monster", "trap", "fall"  (태그를 Tag 목록에 먼저 만들어놔야 함)
 (5)애니메이터 파라미터 : IsGrounded(bool), IsRunning(bool), IsClimbing(bool), ClimbSpeed(float)
 (6)애니메이터 상태 이름 : "Idle", "death", "jump"(또는 "Jump")  -> 이름이 다르면 anim.Play 가 조용히 실패함(에러X 주의)
 ================================================================================
*/
public class playerController : MonoBehaviour
{
    // ------------------------------------------------------------------
    // 1.컴포넌트 참조변수 (Start에서 GetComponent 로 채운다)
    // (1)매 프레임 GetComponent 를 호출하면 느리므로 Start 에서 한번만 가져와서 변수에 저장해둔다
    // ------------------------------------------------------------------
    Rigidbody2D rb;      // 물리(속도, 중력) 담당
    Animator anim;       // 애니메이터 파라미터/상태 제어
    SpriteRenderer sp;   // 스프라이트 이미지 직접 교체할때 사용 (LateUpdate)
    Collider2D col;      // 발바닥 위치(bounds) 계산, 함정 거리계산에 사용

    // ------------------------------------------------------------------
    // 2.인스펙터 노출 변수
    // ※ [SerializeField] : private 변수인데도 인스펙터창에 보이게 해주는 속성(Attribute)
    // (1)public 으로 열면 다른 스크립트가 마음대로 바꿀 수 있으므로, 인스펙터에서만 조절하고 싶을땐 private + [SerializeField] 를 쓴다
    // (2)주의 : 인스펙터에 값이 한번 저장되면 코드의 초기값(= 1.15f 등)을 바꿔도 반영 안된다. 인스펙터에서 직접 바꾸거나 컴포넌트 우클릭 > Reset 해야함
    // ※ [Header("제목")] : 인스펙터에서 변수 그룹위에 제목글씨를 달아준다 (정리용)
    // ※ [Tooltip("설명")] : 인스펙터에서 변수에 마우스를 올리면 설명이 뜬다
    // ------------------------------------------------------------------
    [SerializeField] float speed = 1.15f;        // 평지 이동속도
    [SerializeField] float ladderSpeed = 1f;     // 사다리 오르내리는 속도

    // ※ LayerMask : 레이캐스트가 "어떤 레이어만 검사할지" 거르는 필터
    // (1)인스펙터에서 체크박스로 레이어를 선택한다. 이 값을 Raycast/Linecast 마지막 매개변수로 넣는다
    // (2)안넣으면 플레이어 자기 자신이나 사다리 트리거 등 모든걸 맞춰버려서 오작동한다
    [SerializeField] LayerMask groundLayer;

    [Header("원작 아케이드 점프 세팅 (X속도 고정 / Y성분으로 숏·롱 분단)")]
    // (1)원작처럼 점프중엔 방향전환 불가 -> 점프 시작할때 X속도를 고정시킨다(fixedVelX)
    // (2)제자리 점프(숏) vs 이동중 점프(롱)는 위로 튀는 힘(Y)만 다르게 줘서 구분한다
    [SerializeField] float fixedJumpSpeedX = 1.15f;
    [SerializeField] float shortJumpPowerY = 3.0f;  // 제자리 점프 힘
    [SerializeField] float longJumpPowerY = 3.5f;   // 방향키 누르고 점프 힘

    [Header("점프 3단계 중력 (상승 약함 -> 정점 체공 -> 하강 조금 강함)")]
    // ※ gravityScale : Rigidbody2D 에 적용되는 중력 배율 (실제중력 = Physics2D.gravity.y * gravityScale)
    // (1)점프 구간별로 gravityScale 을 갈아끼워서 "뿅뿅" 하는 아케이드 점프 느낌을 만든다
    // (2)riseGravity : 올라갈때 약한 중력 / peakHangGravity : 정점 근처 아주 약한 중력(공중에 떠있는 느낌) / fallGravity : 내려올때 조금 센 중력
    // (3)peakVelocityThreshold : |Y속도| 가 이 값 이하면 "정점 근처"라고 판단하는 기준
    [SerializeField] float riseGravity = 1.35f;
    [SerializeField] float peakHangGravity = 0.65f;
    [SerializeField] float fallGravity = 1.85f;
    [SerializeField] float peakVelocityThreshold = 0.8f;

    // Idle 상태일때 애니메이션 대신 직접 끼워넣을 스프라이트 (LateUpdate 참고)
    [SerializeField] Sprite frontSprite;  // 사다리 탈때 정면 모습
    [SerializeField] Sprite leftSprite;   // 평소 옆모습(왼쪽 기준, 오른쪽은 localScale 뒤집기로 처리)

    [Header("5개 층 Y좌표 분기 기준값 (현재 맵 좌표 적용 완료)")]
    // (1)플레이어 Y좌표가 이 값보다 크거나 같으면 해당 층으로 판정 (GetCurrentFloorByY 참고)
    // (2)맵을 수정해서 층 높이가 바뀌면 이 값들을 다시 맞춰줘야 한다. 확인법 : 사망시 콘솔에 찍히는 "사망 Y좌표" 로그를 보고 조정
    [SerializeField] float floor5MinY = 0.76f;
    [SerializeField] float floor4MinY = -0.20f;
    [SerializeField] float floor3MinY = -1.16f;
    [SerializeField] float floor2MinY = -2.12f;
    [SerializeField] float firstFloorY = -2.60f;   // 추락 사망시 떨어져서 멈출 1층 바닥 Y

    [Header("각 층별(1층~5층) 사망 MP3 오디오 클립 (Death1 ~ Death5)")]
    [SerializeField] AudioClip floor1Clip;
    [SerializeField] AudioClip floor2Clip;
    [SerializeField] AudioClip floor3Clip;
    [SerializeField] AudioClip floor4Clip;
    [SerializeField] AudioClip floor5Clip;

    [Header("추락 사운드 & 애니메이션 조절")]
    [SerializeField] float crashSoundDuration = 0.45f;  // 사운드 끝부분 "쿵" 소리 길이(이만큼 빼고 낙하시간 계산)
    [SerializeField] float deathSpinMultiplier = 2.0f;  // 추락중 death 애니메이션 회전 배율

    [SerializeField] bool fallToFirstFloor = true;      // (현재 코드에선 사용 안함 - 나중에 옵션 만들때 쓰려고 남겨둔 변수)

    // ------------------------------------------------------------------
    // 3.입력/상태 변수
    // ------------------------------------------------------------------
    float axisH = 0f;   // 좌우 입력값 (-1, 0, 1)
    float axisV = 0f;   // 상하 입력값 (-1, 0, 1)

    // ※ enum(열거형) : 정해진 값들만 가질 수 있는 자료형. bool 은 2가지뿐인데 상태가 3개 이상일때 쓴다
    // (1)여기선 "지금 어느쪽 보고있나" 를 Front(정면,사다리) / Left / Right 로 구분
    // (2)private enum 이라서 이 클래스 안에서만 쓸 수 있다
    private enum FacingState { Front, Left, Right }
    private FacingState currentFacing = FacingState.Left;

    // (1)상태 플래그들 : 여러 bool 로 현재상태를 표현 -> Update/FixedUpdate 에서 if 로 분기
    bool onGround = false;       // 지금 땅에 서있나
    bool ladderTrigger = false;  // 지금 사다리 "타는 중"인가 (사다리 근처에 있는 것과는 다름)
    bool isJumping = false;      // 점프 코루틴이 돌고있는가
    bool isStunned = false;      // 경직 상태 (현재 코드에선 true 로 바꾸는 곳 없음, 확장용)
    bool isDead = false;         // 죽었나 (true면 대부분의 로직 return)
    bool isLadderTop = false;    // 사다리 꼭대기 트리거 안에 있나

    bool isNearLadder = false;   // 사다리 트리거 범위 안에 있나 (아직 타는건 아님)
    float ladderCenterX = 0f;    // 사다리 중앙 X좌표 (탈때 이 X로 스냅시킴)
    float jumpStartY = 0f;       // 점프 시작한 Y높이 (사다리 구멍 착지 보정, 윗층 함정 판정에 사용)

    // ★ [버그수정] 사다리 텔레포트 방지용 상태 변수
    // (1)원인 : 사다리 2개가 가까이 있을때 트리거가 번갈아 발동하면서 ladderCenterX 가 바뀌고,
    //          FixedUpdate 의 rb.position X스냅 때문에 즉시 옆 사다리로 순간이동했음
    // (2)해결 : "지금 감지중인 사다리"를 기억해두고(currentLadderCollider) 타는 중엔 다른 사다리를 무시
    Collider2D currentLadderCollider = null;  // 지금 감지 중인(가장 가까운) 사다리 콜라이더
    int ladderTopOverlapCount = 0;             // 꼭대기 트리거 중첩 카운터
    [Tooltip("사다리 꼭대기 트리거 범위 판정 시 가로 여유(유닛). 기본 0.1")]
    [SerializeField] float ladderTopMatchMargin = 0.1f;

    // ★ 사다리 하강 시 바닥 접촉 판정용
    // (1)ladderGroundContactGap : 발바닥-바닥 사이 간격이 이 값 이하일때만 "바닥에 닿았다" 고 보고 사다리를 해제
    // (2)너무 작으면(0.03 등) 측정 오차때문에 영영 해제가 안될 수 있고, 너무 크면 공중에서 일찍 해제돼서 뚝 떨어진다
    // (3)주의 : 인스펙터에 예전 값(0.03)이 저장돼있으면 코드값이 안먹으니 인스펙터에서 0.05로 직접 바꿀것
    [SerializeField] float ladderGroundContactGap = 0.05f;  // 발-바닥 간격이 이 값 이하일 때만 사다리 해제
    // (4)ladderExitArmed : 사다리 "탑승 직후" 에는 발밑이 바로 윗층 바닥이라 간격=0 -> 타자마자 해제되는 문제 방지용 안전핀
    //    한번 발이 바닥에서 떨어져야(간격 큼) true 가 되고, 그 이후부터 해제 판정을 한다
    bool ladderExitArmed = false;                            // 탑승 직후 발밑 바닥(상단 층)에 의한 즉시 해제 방지
    // (5)ladderStuckTimer/ladderLastY : "아래키를 누르는데 위치가 안변한다" = 바닥에 막혔다 로 보고 강제 해제하는 안전장치용
    float ladderStuckTimer = 0f;                             // 내려가려는데 안 움직이는 시간 (바닥에 막힘 감지용)
    float ladderLastY = 0f;

    [SerializeField] AudioClip walk;
    [SerializeField] AudioClip jump;
    [SerializeField] AudioClip over;   // 기본 사망음(층별 클립이 비어있을때 대체용)
    AudioSource source;

    gruopLayoutUI life;   // 목숨 UI 스크립트 (클래스 이름 오타 gruop 그대로 - 바꾸면 UI 스크립트도 같이 바꿔야 함)

    void Start()
    {
        // ※ GetComponent<T>() : 이 스크립트가 붙은 "같은 게임오브젝트" 에서 컴포넌트를 찾아온다
        // (1)주의 : 해당 컴포넌트가 오브젝트에 없으면 null 이 되어서 아래에서 NullReferenceException 에러 발생
        source = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sp = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        // ※ FindObjectOfType<T>() : 씬 전체에서 해당 타입의 오브젝트를 하나 찾는다
        // (1)GetComponent 와 차이 : GetComponent 는 내 오브젝트 안에서, FindObjectOfType 은 씬 전체에서 찾음 (느리니까 Start 에서 한번만)
        // (2)참고 : 최신 유니티(2023+)에선 FindFirstObjectByType 으로 대체되는 추세 (지금은 경고만 뜸)
        life = FindObjectOfType<gruopLayoutUI>();

        rb.gravityScale = fallGravity;   // 평소 중력 배율 = 하강 중력
        anim.speed = 1f;                 // 애니메이션 재생속도 정상(0이면 일시정지)
    }

    void Update()
    {
        // ==============================================================
        // 1.바닥 감지
        // ※ Physics2D.Linecast(시작점, 끝점, 레이어마스크)
        // (1)의미 : 시작점~끝점을 잇는 "선분" 위에 콜라이더가 있으면 true 를 돌려줌 (bool 형 반환으로 쓸때)
        // (2)Raycast 와 차이 : Raycast 는 (시작점, 방향, 거리) / Linecast 는 (시작점, 끝점) -> 둘 다 같은 원리
        // (3)transform.position - transform.up * 0.3f : 내 위치에서 아래쪽으로 0.3 유닛 (transform.up 은 내 위쪽방향 벡터, 벡터 시트 11번 참고)
        // (4)주의 : transform.position(피벗) 에서 쏘기 때문에 피벗이 발바닥이 아니면 0.3 이 짧거나 길 수 있다
        // ==============================================================
        bool hitGround = Physics2D.Linecast(transform.position, transform.position - (transform.up * 0.3f), groundLayer);

        if (isJumping)
        {
            // 점프중 : 사다리 구멍(바닥이 없는 구간) 위로 점프했다가 시작높이로 돌아오면 "착지" 로 인정
            // (1)바닥 콜라이더가 없는 사다리 구멍에서도 점프가 끝날 수 있게 하는 보정
            bool landedOnLadderGap = (isNearLadder || isLadderTop) && (rb.velocity.y <= 0f) && (transform.position.y <= jumpStartY + 0.02f);
            onGround = hitGround || landedOnLadderGap;
        }
        else
        {
            // 평소 : 바닥에 닿았거나, 사다리 근처인데 아직 안탔으면 땅으로 취급
            onGround = hitGround || ((isNearLadder || isLadderTop) && !ladderTrigger);
        }

        if (isDead) return;   // 죽었으면 입력 처리 전부 중단 (※ 가드 클로즈: 조건 맞으면 일찍 return 해서 아래 코드 실행 막기)

        // 애니메이션용 접지 상태 (점프중엔 false 로 -> 점프 애니메이션이 나오게)
        bool isAnimationGrounded = onGround && (!isJumping || isStunned);
        anim.SetBool("IsGrounded", isAnimationGrounded);

        // ==============================================================
        // 2.입력 받기
        // ※ Input.GetAxisRaw("Horizontal") vs Input.GetAxis("Horizontal")
        // (1)GetAxisRaw : -1, 0, 1 중 하나로 딱딱 떨어짐 (가속/감속 없음) -> 아케이드 게임에 적합
        // (2)GetAxis : -1~1 사이로 부드럽게 변함(스무딩)
        // ※ Input.GetKeyDown(키) : 키를 "누른 그 프레임" 에만 true (꾹 눌러도 한번만) / GetKey 는 누르고있는 동안 계속 true
        // ※ Input.GetButtonDown("Jump") : 키를 직접 지정하지 않고 Project Settings > Input Manager 에 등록된 이름("Jump")으로 받는다 (기본은 스페이스)
        // ==============================================================
        axisH = Input.GetAxisRaw("Horizontal");
        axisV = Input.GetAxisRaw("Vertical");

        // ==============================================================
        // 3.사다리 탑승 처리
        // (1)조건 : 사다리 범위 안 + 아직 안탔음 + 점프/경직 아님
        // (2)위/아래 키를 "누르는 순간" 탑승
        // (3)꼭대기에서 아래키 = 위층에서 사다리로 내려가는 탑승
        // ==============================================================
        if ((isNearLadder || isLadderTop) && !ladderTrigger && !isJumping && !isStunned)
        {
            bool pressUp = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            bool pressDownAtTop = isLadderTop && (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow));

            if (pressUp || pressDownAtTop)
            {
                // [버그수정] 탑승 직전 현재 사다리 콜라이더 기준으로 중앙 X 재계산
                if (currentLadderCollider != null) ladderCenterX = GetLadderCenterX(currentLadderCollider);

                ladderTrigger = true;
                ladderExitArmed = false;   // ★ 추가 : 탑승할때마다 안전핀 다시 꽂기
                ladderStuckTimer = 0f;
                ladderLastY = transform.position.y;
                // 사다리 중앙 X로 이동(Y,Z는 그대로) -> 사다리 한가운데 정렬
                // ※ transform.position 은 Vector3 라서 x만 단독으로 못바꾼다 -> new Vector3 로 통째로 대입 (벡터 시트 12번 참고)
                transform.position = new Vector3(ladderCenterX, transform.position.y, transform.position.z);
                rb.velocity = Vector2.zero;
                currentFacing = FacingState.Front;
            }
        }

        // ==============================================================
        // 4.사다리 타는 중 해제 판정
        // (1)옆키를 누르면 : 꼭대기에 있거나, 발이 바닥에 닿았을때만 사다리에서 내려서 걸어간다 (사다리 중간에선 옆으로 못나감)
        // (2)발이 바닥에 닿았고 위키를 안누르면 : 사다리 해제
        // ==============================================================
        if (ladderTrigger)
        {
            bool feetOnGround = IsFeetOnGroundForLadder();
            bool wantsSide = Mathf.Abs(axisH) > 0.1f;

            if (wantsSide && (isLadderTop || (feetOnGround && axisV <= 0.1f)))
            {
                ExitLadderToGround();
            }
            else if (feetOnGround && axisV <= 0.0f)
            {
                ExitLadderToGround();
            }
        }

        // ==============================================================
        // 5.좌우 방향 전환 (스프라이트 뒤집기)
        // ※ transform.localScale = new Vector2(-1, 1) : X스케일을 -1 로 하면 이미지가 좌우반전된다
        // (1)스프라이트를 좌/우 두 장 만들 필요없이 한 장(왼쪽기준)으로 뒤집어서 쓰는 방식 (Right = -1, Left = 1)
        // (2)주의 : 자식 오브젝트(콜라이더, 무기 등)도 같이 뒤집힌다 / SpriteRenderer.flipX 로도 가능하지만 자식은 안뒤집힘
        // ==============================================================
        if (!isJumping && !isStunned)
        {
            if (!ladderTrigger)
            {
                if (axisH > 0.0f)
                {
                    transform.localScale = new Vector2(-1, 1);
                    currentFacing = FacingState.Right;
                }
                else if (axisH < 0.0f)
                {
                    transform.localScale = new Vector2(1, 1);
                    currentFacing = FacingState.Left;
                }
            }
            else if (Mathf.Abs(axisV) > 0.1f)
            {
                currentFacing = FacingState.Front;   // 사다리에서 오르내리면 정면
            }
        }

        if (isJumping || isStunned)
        {
            anim.SetBool("IsRunning", false);
            return;   // 점프/경직중엔 아래 달리기, 점프시작 처리 안함
        }

        // ==============================================================
        // 6.애니메이션 파라미터 갱신 (애니메이터 시트 3번 참고)
        // (1)IsRunning : 좌우입력 있고 사다리 안탔을때
        // (2)IsClimbing : 사다리 타는 중
        // ==============================================================
        bool isRunning = Mathf.Abs(axisH) > 0.1f && !ladderTrigger;
        anim.SetBool("IsRunning", isRunning);
        anim.SetBool("IsClimbing", ladderTrigger);

        // ==============================================================
        // 7.걷는 소리
        // ※ AudioSource.PlayOneShot(clip) vs Play()
        // (1)PlayOneShot : 기존 재생을 끊지 않고 겹쳐서 한번 재생 (효과음용)
        // (2)Play : AudioSource 에 지정된 clip 을 재생 (이미 재생중이면 처음부터 다시)
        // (3)source.isPlaying 으로 "재생중이 아닐때만" PlayOneShot 해서 발소리가 겹쳐 뭉개지는걸 방지
        // (4)멈추면 source.Stop() 으로 즉시 끊는다
        // ==============================================================
        if (!isJumping)
        {
            if (isRunning && onGround && !source.isPlaying)
            {
                source.PlayOneShot(walk);
            }
            else if (!isRunning && source.isPlaying)
            {
                source.Stop();
            }
        }

        // ClimbSpeed : 사다리에서 움직일때만 애니메이션이 재생되게 (|axisV| 가 0이면 애니메이션 속도 0)
        // ※ SetFloat : 애니메이터의 float 파라미터 값 설정. 블렌드트리나 Transition 조건/Speed 파라미터 연결에 사용
        if (ladderTrigger)
        {
            anim.SetFloat("ClimbSpeed", Mathf.Abs(axisV));
        }

        // 점프 시작 : 점프키 + 땅 + 사다리 안탐
        // ※ StartCoroutine(메소드()) : 코루틴 메소드 호출 (코루틴 시트 0번 참고)
        if (Input.GetButtonDown("Jump") && onGround && !ladderTrigger)
        {
            StartCoroutine(PonpokoJumpRoutine());
        }
    }

    // ==================================================================
    // ※ FixedUpdate : 물리엔진과 같은 주기(기본 0.02초)로 호출됨
    // (1)Rigidbody 의 velocity, gravityScale 등 물리값은 여기서 처리한다
    // (2)Update 에서 axisH, axisV 를 받아두고 여기서 사용하는 패턴 (입력은 Update, 물리는 FixedUpdate)
    // ==================================================================
    void FixedUpdate()
    {
        if (isDead) return;

        // ---------------- 점프중 : 구간별 중력 교체 ----------------
        // (1)|Y속도| <= 임계값 : 정점 근처 -> 아주 약한 중력(떠있는 느낌)
        // (2)Y속도 > 0 : 상승중 -> 약한 중력 / 그외 : 하강중 -> 조금 센 중력
        // (3)X속도는 코루틴(PonpokoJumpRoutine)에서 고정시켜주므로 여기선 중력만 건드림
        if (isJumping)
        {
            if (Mathf.Abs(rb.velocity.y) <= peakVelocityThreshold)
            {
                rb.gravityScale = peakHangGravity;
            }
            else if (rb.velocity.y > 0f)
            {
                rb.gravityScale = riseGravity;
            }
            else
            {
                rb.gravityScale = fallGravity;
            }
            return;
        }

        if (isStunned) return;

        // ---------------- 사다리 타는 중 ----------------
        if (ladderTrigger)
        {
            rb.gravityScale = 0f;   // 사다리 위에선 중력 OFF (안그러면 미끄러져 내려감)
            float currentLadderSpeed = ladderSpeed;

            // 꼭대기에서 위키를 누르면 더 못올라가게 속도 0
            if (isLadderTop && axisV > 0f)
            {
                currentLadderSpeed = 0f;
            }

            float vy = axisV * currentLadderSpeed;

            // ★ 안전장치: 내려가려는데(armed 상태) 위치가 거의 안 변하면 바닥에 막힌 것 -> 사다리 해제
            // (1)movedY : 지난 FixedUpdate 때 Y - 지금 Y (양수면 아래로 내려간 것)
            // (2)0.0005 보다 적게 움직인 시간이 0.08초 쌓이면 "막혔다" 고 판단
            // (3)Time.fixedDeltaTime : FixedUpdate 한번의 시간간격(기본 0.02초). FixedUpdate 안에선 Time.deltaTime 도 같은 값이지만 fixedDeltaTime 이 의미상 정확
            if (axisV < 0f && ladderExitArmed)
            {
                float movedY = ladderLastY - rb.position.y;
                if (movedY < 0.0005f) ladderStuckTimer += Time.fixedDeltaTime;
                else ladderStuckTimer = 0f;

                if (ladderStuckTimer >= 0.08f)
                {
                    ladderStuckTimer = 0f;
                    ExitLadderToGround();
                    return;
                }
            }
            else
            {
                ladderStuckTimer = 0f;
            }
            ladderLastY = rb.position.y;

            // ★ 내려갈 때 바닥을 뚫거나 크게 스냅되지 않도록, 남은 거리만큼만 이동
            // (1)속도 = 거리 / 시간 이므로, 한 프레임에 gap(남은거리) 만큼만 내려가게 속도 상한을 건다
            // (2)maxDown = gap / fixedDeltaTime  ->  vy 는 음수라서 Mathf.Max(vy, -maxDown) 으로 "더 느린쪽" 선택
            // (3)이 덕분에 바닥에 닿는 순간 순간이동 없이 부드럽게 멈춘다 (원래 텔레포트 버그 해결 포인트)
            if (vy < 0f && ladderExitArmed)
            {
                float gap = GetGroundGapBelowFeet();
                if (gap < float.MaxValue)
                {
                    float maxDown = gap / Time.fixedDeltaTime;
                    vy = Mathf.Max(vy, -maxDown);
                }
            }

            rb.velocity = new Vector2(0f, vy);   // X속도는 0 (사다리에선 좌우로 안움직임)
            // ※ rb.position vs transform.position
            // (1)rb.position : 물리엔진 쪽 위치를 직접 지정 (FixedUpdate 에서 물리 오브젝트 위치를 바꿀땐 이쪽이 안전)
            // (2)transform.position : 트랜스폼 위치 (Update 에서 즉시 이동시킬때)
            // (3)매 FixedUpdate 마다 X를 사다리 중앙으로 고정 -> ladderCenterX 가 바뀌면 바로 순간이동하니 주의(★텔레포트 버그 원인이었음)
            rb.position = new Vector2(ladderCenterX, rb.position.y);

            // 아래로 내려가려는 중이 아니고(axisV <= 0) 발이 바닥에 닿았으면 사다리 해제
            if (axisV <= 0f && IsFeetOnGroundForLadder())
            {
                ExitLadderToGround();
            }
        }
        // ---------------- 평지 이동 ----------------
        else
        {
            // 사다리 근처(타지는 않음)에선 중력을 꺼서 사다리 구멍에 빠지지 않게 하고 Y속도도 0으로 고정
            rb.gravityScale = (isNearLadder || isLadderTop) ? 0f : fallGravity;
            float targetVelY = (isNearLadder || isLadderTop) ? 0f : rb.velocity.y;
            // ※ 속도 설정 주의 : velocity 는 x,y 를 따로 못바꾸고 통째로 대입해야 한다 (벡터 시트 12번)
            //   그래서 Y는 "현재 Y속도 그대로(rb.velocity.y)" 를 넣어서 중력낙하가 유지되게 한다
            rb.velocity = new Vector2(axisH * speed, targetVelY);
        }
    }

    // ==================================================================
    // ※ LateUpdate : 모든 Update 가 끝난 뒤 호출됨
    // (1)애니메이터가 Update 이후에 스프라이트를 바꾸기 때문에, 내가 원하는 스프라이트로 "덮어쓰기" 할땐 LateUpdate 에서 해야 적용된다
    // (2)Idle 상태일때만 : 정면(사다리)이면 frontSprite, 아니면 leftSprite 로 강제 교체
    // ※ anim.GetCurrentAnimatorStateInfo(0) : 0번 레이어의 현재 상태 정보. .IsName("Idle") 로 상태 이름 비교
    // (3)주의 : IsName 은 애니메이터 창의 "상태 이름" 과 철자/대소문자까지 같아야 true
    // ==================================================================
    void LateUpdate()
    {
        if (isDead) return;

        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);

        if (!isJumping && state.IsName("Idle"))
        {
            if (currentFacing != FacingState.Front)
            {
                sp.sprite = leftSprite;
            }
            else
            {
                sp.sprite = frontSprite;
            }
        }
    }

    // ==================================================================
    // ※ 사다리 중앙 X좌표 구하기 (타일맵 / 박스콜라이더 둘 다 대응)
    // (1)사다리가 Tilemap 으로 만들어졌으면 : 내 위치 주변 셀을 훑어서 타일이 있는 최소/최대 셀의 중앙을 평균
    // (2)BoxCollider2D 가 여러개 붙어있으면 : 내 X와 가장 가까운 박스의 중앙
    // (3)그 외 : 콜라이더의 bounds.center.x
    //
    // ※ Tilemap 관련 메소드 (타일맵 시트 참고)
    // (1)tilemap.WorldToCell(월드좌표) : 월드좌표 -> 셀 좌표(Vector3Int, 정수 격자) 변환
    // (2)tilemap.HasTile(셀좌표) : 그 셀에 타일이 칠해져 있나 (bool)
    // (3)tilemap.GetCellCenterWorld(셀좌표) : 그 셀의 "중앙" 월드좌표 (Vector3)
    // (4)Vector3Int : int 3개짜리 벡터 (타일 셀은 소수점이 없으니 Vector3 가 아니라 Int 형 사용)
    // ==================================================================
    float GetLadderCenterX(Collider2D ladderCol)
    {
        Tilemap tilemap = ladderCol.GetComponent<Tilemap>();   // 타일맵이 아니면 null
        if (tilemap != null)
        {
            Vector3Int playerCell = tilemap.WorldToCell(transform.position);
            int minX = int.MaxValue;   // 최소값 찾기 패턴 : 최대로 초기화해두고 더 작은게 나오면 갱신
            int maxX = int.MinValue;   // 최대값 찾기 패턴 : 최소로 초기화해두고 더 큰게 나오면 갱신
            bool found = false;

            // dy : 위(+1) -> 현재(0) -> 아래(-1) 순서로 훑다가 타일을 찾으면 break (가장 가까운 줄 우선)
            for (int dy = 1; dy >= -1; dy--)
            {
                // [버그수정] 원래 ±2칸(5칸 폭)으로 너무 넓게 훑어 옆 사다리까지 같이 잡음. ±1칸으로 축소.
                //   (사다리가 가로 3칸 이상 넓다면 이 범위를 다시 ±2로 늘릴 것)
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector3Int checkCell = new Vector3Int(playerCell.x + dx, playerCell.y + dy, playerCell.z);
                    if (tilemap.HasTile(checkCell))
                    {
                        if (checkCell.x < minX) minX = checkCell.x;
                        if (checkCell.x > maxX) maxX = checkCell.x;
                        found = true;
                    }
                }
                if (found) break;
            }

            if (found)
            {
                Vector3 minCenter = tilemap.GetCellCenterWorld(new Vector3Int(minX, playerCell.y, playerCell.z));
                Vector3 maxCenter = tilemap.GetCellCenterWorld(new Vector3Int(maxX, playerCell.y, playerCell.z));
                return (minCenter.x + maxCenter.x) * 0.5f;   // 양끝 중앙의 평균 = 사다리 폭의 한가운데
            }
        }

        // ※ GetComponents<T>() (복수형 s) : 같은 타입 컴포넌트가 여러개일때 배열로 전부 가져온다
        // (1)GetComponent(단수) 는 첫번째 하나만 가져옴
        // (2)주의 : 배열이 비었을때 [0] 으로 접근하면 IndexOutOfRangeException (디버깅 시트 2번) -> 반드시 .Length 체크
        BoxCollider2D[] boxes = ladderCol.GetComponents<BoxCollider2D>();
        if (boxes.Length > 1)
        {
            float closestX = transform.position.x;
            float minDistance = float.MaxValue;
            foreach (BoxCollider2D box in boxes)
            {
                float dist = Mathf.Abs(box.bounds.center.x - transform.position.x);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestX = box.bounds.center.x;
                }
            }
            return closestX;
        }

        // ※ collider.bounds : 월드좌표 기준 콜라이더를 감싸는 사각형(Bounds). center / min / max / extents / size 사용 가능
        return ladderCol.bounds.center.x;
    }

    // ==================================================================
    // [버그수정] 이 꼭대기(laddertop) 트리거가 "지금 타는 사다리와 관련 있는가" 판정.
    // (1)사다리 타는 중이 아니면 무조건 true (일반 감지)
    // (2)타는 중이면 ladderCenterX 가 이 꼭대기 트리거의 가로범위 안에 있어야 인정 -> 다른 사다리 꼭대기 오염 차단
    // (3)Margin(0.1) : 경계에 딱 걸칠때 흔들리지 않게 주는 여유값
    // ==================================================================
    bool IsThisLadderTopRelevant(Collider2D topCol)
    {
        if (!ladderTrigger) return true;
        Bounds b = topCol.bounds;
        return ladderCenterX >= b.min.x - ladderTopMatchMargin && ladderCenterX <= b.max.x + ladderTopMatchMargin;
    }

    // ==================================================================
    // 피벗(transform.position) 에서 콜라이더 발바닥까지의 거리
    // (1)BoxCollider2D : size.y 의 절반 - offset.y  (콜라이더 중심이 offset 만큼 위로 올라가있으면 그만큼 발바닥은 가까워짐)
    // (2)그외 콜라이더 : bounds.extents.y (중심에서 위/아래 끝까지의 절반높이)
    // ※ 참고 : `col as BoxCollider2D` -> col 이 BoxCollider2D 면 변환해주고, 아니면 null (예외 안터지는 안전한 형변환)
    // (3)지금은 ExitLadderToGround 가 bounds 기준으로 바뀌어서 안쓰지만, 피벗->발바닥 거리가 필요할때 재활용 가능
    // ==================================================================
    float GetFootOffset()
    {
        BoxCollider2D boxCol = col as BoxCollider2D;
        if (boxCol != null) return boxCol.size.y * 0.5f - boxCol.offset.y;
        if (col != null) return col.bounds.extents.y;
        return 0.25f;
    }

    // ==================================================================
    // ★ 발 아래 바닥까지의 실제 간격 (없으면 float.MaxValue)
    // ※ Physics2D.Raycast(시작점, 방향, 거리, 레이어마스크) -> RaycastHit2D 반환
    // (1)hit.collider == null : 아무것도 못맞춤
    // (2)hit.distance : 시작점에서 맞은 곳까지 거리 / hit.point : 맞은 지점의 월드좌표
    // ※ 왜 col.bounds.min.y 를 썼나
    // (1)피벗(transform.position)은 스프라이트마다 위치가 달라서 발바닥과 어긋날 수 있음
    // (2)bounds.min.y 는 콜라이더 "실제 바닥면" 이라 피벗/오프셋에 상관없이 정확
    // ※ lift(0.05) : 바닥면 살짝 "위"에서 쏴야 한다. 바닥면 정확히 그 높이에서 쏘면 이미 바닥 안쪽이라 판정이 불안정
    //   -> 쏜 거리에서 lift 를 빼서 실제 간격으로 환산
    // ※ const : 바뀌지 않는 상수 선언 (지역변수에도 사용 가능)
    // ==================================================================
    float GetGroundGapBelowFeet()
    {
        if (col == null) return float.MaxValue;

        // 콜라이더 실제 바닥(bounds.min.y) 기준으로 측정 (피벗/오프셋 오차 영향 없음)
        Bounds b = col.bounds;
        const float lift = 0.05f;
        Vector2 origin = new Vector2(b.center.x, b.min.y + lift);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, lift + 1.0f, groundLayer);
        if (hit.collider == null) return float.MaxValue;
        return Mathf.Max(0f, hit.distance - lift);   // 음수 방지
    }

    // ==================================================================
    // ★ 사다리 중 "발이 실제로 바닥에 닿았는가"
    // (1)ladderExitArmed 가 false 인 동안은 항상 false 반환 (탑승 직후 윗층 바닥 때문에 바로 해제되는 것 방지)
    // (2)간격이 (contactGap + 0.02) 보다 커지면 armed = true -> 그 뒤부터 간격 <= contactGap 이면 "닿았다"
    // (3)+0.02 는 armed 되는 기준과 해제 기준 사이에 틈을 둬서 경계에서 깜빡이는 걸 막는 용도 (히스테리시스)
    // ==================================================================
    bool IsFeetOnGroundForLadder()
    {
        float gap = GetGroundGapBelowFeet();

        // 탑승 직후엔 발이 바닥에서 한 번 떨어진 뒤부터 해제 판정 활성화
        if (!ladderExitArmed)
        {
            if (gap > ladderGroundContactGap + 0.02f) ladderExitArmed = true;
            else return false;
        }
        return gap <= ladderGroundContactGap;
    }

    // ==================================================================
    // ★ 사다리에서 내려와 땅 위로 복귀
    // (1)사다리 상태 플래그 끄고 중력 복구
    // (2)발 바로 아래(0.1 이내)에 바닥이 있으면 "발바닥이 바닥면에 딱 붙도록" 위치 보정
    //    -> dy = 바닥면Y - 내 발바닥Y 만큼 이동 (멀면 보정안하고 중력으로 자연스럽게 낙하)
    // (3)transform.position 과 rb.position 을 둘 다 갱신 (한쪽만 바꾸면 물리엔진이 다음 프레임에 되돌릴 수 있음)
    // (4)rb.velocity 에 axisH * speed 를 주는 이유 : 사다리에서 옆키 누른채 내렸을때 바로 걸어가게
    // ==================================================================
    void ExitLadderToGround()
    {
        ladderTrigger = false;
        ladderExitArmed = false;

        // 아주 가까운 바닥에만 보정 스냅 (멀면 스냅하지 않고 중력으로 자연스럽게 낙하)
        if (col != null)
        {
            Bounds b = col.bounds;
            const float lift = 0.05f;
            Vector2 origin = new Vector2(b.center.x, b.min.y + lift);
            RaycastHit2D groundHit = Physics2D.Raycast(origin, Vector2.down, lift + 0.1f, groundLayer);
            if (groundHit.collider != null)
            {
                float dy = groundHit.point.y - b.min.y;
                Vector2 targetPos = new Vector2(transform.position.x, transform.position.y + dy);
                transform.position = targetPos;
                rb.position = targetPos;
            }
        }

        rb.velocity = new Vector2(axisH * speed, 0f);
        rb.gravityScale = fallGravity;
    }

    // ==================================================================
    // ※ 뿅뿅 점프 코루틴 (코루틴 시트 참고)
    // 1.흐름
    // (1)점프 시작 처리(속도 부여, 소리) -> (2)공중 체공시간 미리 계산 -> (3)while 루프로 착지할때까지 매 FixedUpdate 마다 속도/애니메이션 갱신
    // -> (4)착지 후 상태 복구
    // 2.왜 코루틴인가 : "점프 시작 ~ 착지" 라는 시간에 걸친 일을 변수/플래그 없이 한 메소드에 순서대로 쓸 수 있어서
    //   (Update 로 하면 상태 변수가 여러개 필요함)
    // ==================================================================
    IEnumerator PonpokoJumpRoutine()
    {
        isJumping = true;
        jumpStartY = transform.position.y;

        source.Stop();
        source.PlayOneShot(jump);

        // ---- 1.점프 방향 결정 ----
        // (1)방향키 누르고 있으면 그 방향 / 안누르면 지금 바라보는 방향으로 점프
        // ※ Mathf.Sign(값) : 양수면 1, 음수면 -1 (0이면 1 반환 주의)
        float dirX = 0f;
        if (Mathf.Abs(axisH) > 0.1f)
        {
            dirX = Mathf.Sign(axisH);
            if (dirX > 0f)
            {
                transform.localScale = new Vector2(-1, 1);
                currentFacing = FacingState.Right;
            }
            else
            {
                transform.localScale = new Vector2(1, 1);
                currentFacing = FacingState.Left;
            }
        }
        else
        {
            if (currentFacing == FacingState.Right) dirX = 1f;
            else if (currentFacing == FacingState.Left) dirX = -1;
            else dirX = (transform.localScale.x < 0f) ? 1f : -1f;   // Front 일땐 localScale 로 판단
        }

        // 점프중엔 이 X속도 고정 (원작처럼 공중에서 방향전환 불가)
        float fixedVelX = dirX * fixedJumpSpeedX;

        // 방향키 누르고 점프 = 롱점프(Y힘 큼), 제자리 = 숏점프
        // ※ 삼항연산자 : 조건 ? 참일때값 : 거짓일때값
        float selectedJumpY = (Mathf.Abs(axisH) > 0.1f) ? longJumpPowerY : shortJumpPowerY;

        // ---- 2.점프 애니메이션 상태 이름 찾기 ----
        // ※ Animator.StringToHash("이름") : 상태이름 문자열을 정수 해시로 변환. anim.Play 에 문자열 대신 해시를 쓰면 빠르다
        // ※ anim.HasState(레이어, 해시) : 그 레이어에 그 상태가 존재하나 (대소문자 둘다 대응하려고 "jump" 없으면 "Jump" 사용)
        int jumpStateHash = anim.HasState(0, Animator.StringToHash("jump"))
            ? Animator.StringToHash("jump")
            : Animator.StringToHash("Jump");

        // ---- 3.애니메이션을 "수동 재생" 모드로 ----
        // ※ anim.speed = 0f : 애니메이터 자동 재생을 멈춤 (내가 직접 시간을 정해서 재생하려고)
        // ※ anim.Play(상태해시, 레이어, 정규화시간) : 그 상태의 "정규화시간" 위치로 점프해서 재생
        //   (정규화시간 : 0 = 처음, 1 = 끝. 4f/30f 는 30프레임짜리 클립의 4번째 프레임 위치)
        // ※ anim.Update(0f) : 애니메이터를 "지금 즉시" 한번 평가해서 화면에 반영 (안쓰면 다음 프레임에야 바뀜)
        // 주의 : 애니메이션 클립 길이를 30프레임 기준으로 가정하고 있음. 클립 프레임수가 바뀌면 4f/30f, 30f/30f 도 같이 수정해야함
        anim.speed = 0f;
        anim.Play(jumpStateHash, 0, 4f / 30f);
        anim.Update(0f);

        rb.gravityScale = riseGravity;
        rb.velocity = new Vector2(fixedVelX, selectedJumpY);   // 이 순간 점프 발사

        // ---- 4.총 체공시간 계산 (애니메이션을 점프 시간에 딱 맞추려고) ----
        // ※ 등가속도 운동 공식 (물리) : v = v0 - a*t,  h = (v0^2 - v^2) / (2a),  a = g * gravityScale
        // (1)상승 구간1 : 처음속도(v0) -> 정점 임계속도(thr) 까지 (riseGravity 로 감속)  => riseT1(시간), riseH1(올라간 높이)
        // (2)상승 구간2 : thr -> 0 (정점, peakHangGravity)                            => riseT2, riseH2
        // (3)정점 높이 peakH = riseH1 + riseH2
        // (4)하강 구간1 : 정점에서 thr 속도까지 (peakHangGravity)                      => fallT1
        // (5)하강 구간2 : 남은 높이(fallH2)를 fallGravity 로 낙하                       => fallT2 (2차방정식 근의공식으로 시간 구함)
        // (6)총 체공시간 = 네 구간 합 (최소 0.2초 보장)
        float g = Mathf.Abs(Physics2D.gravity.y);   // 기본 중력 크기 (Edit > Project Settings > Physics 2D 값, 보통 9.81)
        float thr = Mathf.Max(0.01f, peakVelocityThreshold);   // 0 으로 나누기 방지용 최소값
        float v0 = Mathf.Max(thr + 0.01f, selectedJumpY);

        float riseT1 = (v0 - thr) / (g * riseGravity);
        float riseH1 = (v0 * v0 - thr * thr) / (2f * g * riseGravity);
        float riseT2 = thr / (g * peakHangGravity);
        float riseH2 = (thr * thr) / (2f * g * peakHangGravity);
        float peakH = riseH1 + riseH2;

        float fallT1 = thr / (g * peakHangGravity);
        float fallH2 = peakH - (thr * thr) / (2f * g * peakHangGravity);
        float fallT2 = (Mathf.Sqrt(thr * thr + 2f * g * fallGravity * Mathf.Max(0.001f, fallH2)) - thr) / (g * fallGravity);

        float totalAirTime = Mathf.Max(0.2f, riseT1 + riseT2 + fallT1 + fallT2);
        float jumpStartTime = Time.time;   // 점프 시작 시각 (Time.time = 게임 시작 후 경과 초)

        // ※ yield return new WaitForFixedUpdate() : 다음 FixedUpdate 까지 대기 (코루틴 시트 4-4, 철자는 WaitForFixedUpdate 가 맞음)
        // (1)점프 발사 직후 한 물리프레임 기다려서 rb.velocity 가 실제 반영된 상태에서 루프 시작
        yield return new WaitForFixedUpdate();

        bool hasReachedPeak = false;   // 정점(Y속도가 0 이하로 꺾임)을 한번 찍었나 -> 점프 직후 바닥에 붙어있는 상태를 착지로 오인하는 것 방지

        while (true)
        {
            if (isDead) yield break;   // 점프중 죽으면 코루틴 즉시 종료 (코루틴 시트 5번 yield break)

            if (!hasReachedPeak && rb.velocity.y <= 0f)
            {
                hasReachedPeak = true;
            }

            // 경과시간 비율(0~1) -> 애니메이션 정규화시간(4/30 ~ 30/30) 으로 변환
            // ※ Mathf.Clamp01(값) : 값을 0~1 사이로 잘라냄
            // ※ Mathf.Lerp(a, b, t) : t(0~1) 비율만큼 a 에서 b 로 보간 (t=0 이면 a, t=1 이면 b)
            float t = Mathf.Clamp01((Time.time - jumpStartTime) / totalAirTime);
            float animNormalizedTime = Mathf.Lerp(4f / 30f, 30f / 30f, t);

            // 점프 진행 방향에 벽이 있는지 검사
            float jumpVelX = fixedVelX;

            if (Mathf.Abs(jumpVelX) > 0.01f && col != null)
            {
                Bounds b = col.bounds;

                RaycastHit2D wallHit = Physics2D.BoxCast(
                    b.center,
                    b.size * 0.95f,
                    0f,
                    Vector2.right * Mathf.Sign(jumpVelX),
                    0.05f,
                    groundLayer
                );

                // 벽이 감지되면 수평 이동만 차단
                if (wallHit.collider != null)
                {
                    jumpVelX = 0f;
                }
            }

            rb.velocity = new Vector2(jumpVelX, rb.velocity.y);
            anim.Play(jumpStateHash, 0, animNormalizedTime);
            anim.Update(0f);

            // 정점을 찍은 뒤 땅에 닿으면 착지 -> while 탈출
            if (hasReachedPeak && onGround)
            {
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        // ---- 5.착지 후 처리 ----
        // 사다리 구멍(바닥 콜라이더 없는 곳)에 착지했으면 시작 높이로 되돌려서 구멍에 빠지는 것 방지
        bool hitSolidGround = Physics2D.Linecast(transform.position, transform.position - (transform.up * 0.25f), groundLayer);
        if (!hitSolidGround && (isNearLadder || isLadderTop))
        {
            transform.position = new Vector3(transform.position.x, jumpStartY, transform.position.z);
        }

        isStunned = false;
        isJumping = false;
        anim.speed = 1f;   // 수동재생 끝, 애니메이션 자동 재생 복구

        // 착지했는데 방향키 누르고 있으면 바로 달리기로 이어가고, 아니면 정지 + Idle
        if (Mathf.Abs(axisH) > 0.1f && !ladderTrigger)
        {
            rb.velocity = new Vector2(axisH * speed, 0f);
            anim.SetBool("IsGrounded", true);
            anim.SetBool("IsRunning", true);
        }
        else
        {
            rb.velocity = Vector2.zero;
            anim.Play("Idle", 0);   // 문자열로 상태 재생 (해시보다 느리지만 가끔 한번이라 OK)
        }
    }

    // ==================================================================
    // ※ 점프중 "윗층 함정" 판정 (점프로 윗층 함정에 부딪힌것처럼 오인되는 것 방지)
    // (1)Physics2D.Distance(콜라이더A, 콜라이더B) : 두 콜라이더 사이 "가장 가까운 두 점" 정보를 돌려줌 (ColliderDistance2D)
    //    - dist.pointA : A(나) 위의 가장 가까운 점 / dist.pointB : B(함정) 위의 가장 가까운 점 / dist.isValid : 계산 성공 여부
    // (2)접촉점이 내 몸 중심보다 위쪽 + 점프 시작 높이보다 0.2 이상 위 -> 한층 위의 함정이라 판단하고 무시(true 반환)
    // (3)점프중이 아니면 항상 false (평소엔 모든 함정에 걸림)
    // ==================================================================
    bool IsUpperFloorTrap(Collider2D trapCol)
    {
        if (!isJumping || col == null) return false;

        ColliderDistance2D dist = Physics2D.Distance(col, trapCol);
        if (!dist.isValid) return false;

        float contactY = Mathf.Max(dist.pointA.y, dist.pointB.y);
        float playerCenterY = col.bounds.center.y;

        return (contactY > playerCenterY) && (contactY > jumpStartY + 0.20f);
    }

    // ==================================================================
    // ※ 트리거 이벤트 3종 (isTrigger 체크된 콜라이더와 겹칠때 자동 호출)
    // (1)OnTriggerEnter2D : 겹치기 시작한 "그 순간" 한번
    // (2)OnTriggerStay2D : 겹쳐있는 동안 매 물리프레임 호출
    // (3)OnTriggerExit2D : 겹침이 끝난 순간 한번
    // (4)매개변수 collision : 부딪힌 "상대방" 콜라이더 (collision.tag 로 종류 구분)
    // (5)주의 : 2D 는 반드시 이름 끝에 2D 가 붙은 메소드여야 함 (OnTriggerEnter 는 3D용이라 2D 에선 호출 안됨)
    // (6)주의 : 둘 중 하나는 Rigidbody2D 가 있어야 이벤트 발생 / 상대가 isTrigger 여야 Trigger 계열, 아니면 OnCollision 계열
    // ※ collision.tag.Equals("문자열") : 태그 비교 (CompareTag("문자열") 이 더 빠르고 오타시 경고도 떠서 권장되지만 동작은 같음)
    // ==================================================================
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag.Equals("ladder"))
        {
            // ★ [버그수정 핵심] 사다리 타는 중엔 다른 사다리 트리거에 들어가도 완전 무시.
            if (ladderTrigger) return;

            // [버그수정] 더 가까운 사다리만 current로 등록 (flip-flop 방지, -0.05f 히스테리시스)
            // ※ 히스테리시스 : 후보가 "충분히 더(0.05) 가까울때만" 교체해서 두 사다리 사이에서 번갈아 바뀌는 깜빡임을 막는 기법
            float centerX = GetLadderCenterX(collision);
            float dist = Mathf.Abs(centerX - transform.position.x);
            float currentDist = (currentLadderCollider != null)
                ? Mathf.Abs(ladderCenterX - transform.position.x)
                : float.MaxValue;
            if (currentLadderCollider == null || collision == currentLadderCollider || dist < currentDist - 0.05f)
            {
                currentLadderCollider = collision;
                isNearLadder = true;
                ladderCenterX = centerX;
            }
        }
        else if (collision.tag.Equals("laddertop"))
        {
            // [버그수정] 지금 타는 사다리 꼭대기만 인정. 카운터로 중첩 관리.
            // ※ 카운터 방식 : 트리거가 여러개 겹칠때 bool 하나로 하면 하나만 나가도 false 가 되어버림
            //   -> Enter 때 ++, Exit 때 -- 해서 0 이 되면 진짜로 다 나간 것으로 판단
            if (!IsThisLadderTopRelevant(collision)) return;
            ladderTopOverlapCount++;
            isLadderTop = true;
        }
        else if (collision.tag.Equals("monster"))
        {
            Die(DeathType.OnFloor);
        }
        else if (collision.tag.Equals("trap"))
        {
            if (IsUpperFloorTrap(collision)) return;
            Die(DeathType.OnFloor);
        }
        else if (collision.tag.Equals("fall"))
        {
            if (IsUpperFloorTrap(collision)) return;
            Die(DeathType.FallDown);
        }
    }

    // Stay : Enter 를 놓쳤을때(예: 사다리 타다 내린 직후 다시 범위 안) 상태를 보정하는 역할도 함
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.tag.Equals("ladder") && !ladderTrigger)
        {
            // [버그수정] 가장 가까운 사다리만 갱신 (Enter와 동일 로직)
            float centerX = GetLadderCenterX(collision);
            float dist = Mathf.Abs(centerX - transform.position.x);
            float currentDist = (currentLadderCollider != null)
                ? Mathf.Abs(ladderCenterX - transform.position.x)
                : float.MaxValue;
            if (currentLadderCollider == null || collision == currentLadderCollider || dist < currentDist - 0.05f)
            {
                currentLadderCollider = collision;
                isNearLadder = true;
                ladderCenterX = centerX;
            }
        }
        else if (collision.tag.Equals("laddertop"))
        {
            // [버그수정] 관련 없는 꼭대기 트리거는 무시. Enter 누락 대비 카운터 보정.
            if (!IsThisLadderTopRelevant(collision)) return;
            if (!isLadderTop)
            {
                ladderTopOverlapCount++;
                isLadderTop = true;
            }
        }
        else if (collision.tag.Equals("monster"))
        {
            Die(DeathType.OnFloor);
        }
        else if (collision.tag.Equals("trap"))
        {
            if (IsUpperFloorTrap(collision)) return;
            Die(DeathType.OnFloor);
        }
        else if (collision.tag.Equals("fall"))
        {
            if (IsUpperFloorTrap(collision)) return;
            Die(DeathType.FallDown);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag.Equals("ladder"))
        {
            // [버그수정] "지금 감지 중인 사다리"에서 빠져나왔을 때만 탈출 처리.
            if (collision == currentLadderCollider)
            {
                currentLadderCollider = null;
                isNearLadder = false;
                if (ladderTrigger)
                {
                    ExitLadderToGround();
                }
                ladderTrigger = false;
                currentFacing = FacingState.Front;
            }
            // 다른 사다리 트리거에서 빠져나온 경우: 아무것도 안 함.
        }
        else if (collision.tag.Equals("laddertop"))
        {
            // [버그수정] 카운터 방식. 꼭대기 여러 개 겹쳐 있을 때 하나만 빠져나와도 isLadderTop 유지.
            if (!IsThisLadderTopRelevant(collision)) return;
            ladderTopOverlapCount--;
            if (ladderTopOverlapCount <= 0)
            {
                ladderTopOverlapCount = 0;   // 혹시 음수로 내려가는 것 방지
                isLadderTop = false;
            }
        }
    }

    // ==================================================================
    // Y좌표로 층 판정 : 위층부터 차례로 검사해서 처음 만족하는 층 반환
    // (1)else if 체인 : 위에서부터 y >= 기준값 인지 보고, 안되면 다음 층 기준으로 내려감
    // ==================================================================
    int GetCurrentFloorByY(float y)
    {
        if (y >= floor5MinY) return 5;
        else if (y >= floor4MinY) return 4;
        else if (y >= floor3MinY) return 3;
        else if (y >= floor2MinY) return 2;
        else return 1;
    }

    // 사망 종류 : OnFloor(그자리에서 쓰러짐 - 몬스터/함정) / FallDown(추락 - 낭떠러지)
    // ※ public enum : 다른 스크립트에서 playerController.DeathType.FallDown 으로 접근 가능
    public enum DeathType { OnFloor, FallDown }

    // ==================================================================
    // ※ 사망 처리
    // (1)매개변수 기본값 : deathType = DeathType.OnFloor -> Die() 처럼 인자 없이 호출하면 OnFloor 로 처리
    // (2)public 이라서 다른 스크립트(몬스터 등)에서 player.Die() 로 호출 가능
    // (3)isDead 가드 : 두번 죽는 것(트리거 Stay 로 매 프레임 호출) 방지
    // (4)StopAllCoroutines() : 이 스크립트에서 돌고있는 모든 코루틴 중단 (점프 코루틴 등). 아래에서 새 코루틴을 시작하므로 순서 중요
    // ==================================================================
    public void Die(DeathType deathType = DeathType.OnFloor)
    {
        if (isDead) return;

        isDead = true;
        StopAllCoroutines();

        if (life != null) life.LoseLife();   // null 체크 : UI 스크립트가 없어도 에러 안나게

        anim.SetBool("IsRunning", false);
        anim.SetBool("IsClimbing", false);
        anim.SetBool("IsGrounded", false);
        source.Stop();

        int currentFloor = GetCurrentFloorByY(transform.position.y);
        // ※ $"문자열 {변수:F2}" : 문자열 보간. {변수} 자리에 값이 들어가고, :F2 는 소수점 2자리까지 표시
        Debug.Log($"사망 Y좌표: {transform.position.y:F2} -> {currentFloor}층 판정 (유형: {deathType})");

        AudioClip selectedClip = over;

        // ---- 그자리 사망 (1층에서의 추락도 같이 처리) ----
        if (deathType == DeathType.OnFloor || currentFloor == 1)
        {
            rb.velocity = Vector2.zero;
            rb.gravityScale = 0f;   // 그 자리에 고정

            anim.Play("death", 0, 0f);   // death 애니메이션 첫 프레임에서
            anim.Update(0f);
            anim.speed = 0f;             // 정지 (쓰러진 모습 고정)

            selectedClip = (floor1Clip != null) ? floor1Clip : over;
            if (selectedClip != null) source.PlayOneShot(selectedClip);
        }
        // ---- 추락 사망 ----
        else if (deathType == DeathType.FallDown)
        {
            rb.velocity = Vector2.zero;

            // 층별 사망 사운드 선택 (비어있으면 기본 over)
            switch (currentFloor)
            {
                case 2: selectedClip = (floor2Clip != null) ? floor2Clip : over; break;
                case 3: selectedClip = (floor3Clip != null) ? floor3Clip : over; break;
                case 4: selectedClip = (floor4Clip != null) ? floor4Clip : over; break;
                case 5: selectedClip = (floor5Clip != null) ? floor5Clip : over; break;
            }

            if (selectedClip != null) source.PlayOneShot(selectedClip);

            // 낙하시간 = 사운드 길이 - 마지막 "쿵" 소리 길이 (사운드 끝에 맞춰 착지하도록)
            float fallDuration = 0.5f;
            if (selectedClip != null)
            {
                fallDuration = Mathf.Max(0.25f, selectedClip.length - crashSoundDuration);
            }

            // ※ 중력 역산 : 원하는 낙하시간(fallDuration) 안에 낙하거리(fallDistance)를 떨어지게 하는 중력 배율 구하기
            //   자유낙하 공식 : 거리 = 1/2 * a * t^2  ->  a = 2 * 거리 / t^2
            //   gravityScale = a / 기본중력(9.81)
            // (1)높은 층일수록 멀리 떨어지니 자동으로 더 센 중력이 적용돼서 항상 사운드 길이에 맞춰 떨어진다
            float startY = transform.position.y;
            float targetY = firstFloorY;
            float fallDistance = Mathf.Max(0.2f, startY - targetY);

            float requiredGravity = (2f * fallDistance) / (fallDuration * fallDuration);
            rb.gravityScale = requiredGravity / Mathf.Abs(Physics2D.gravity.y);

            // 층이 높을수록 많이 회전 (최소 1바퀴)
            int rotations = Mathf.Max(1, currentFloor - 1);
            StartCoroutine(FallDeathLandingRoutine(fallDuration, rotations));
        }

        // ---- 사운드가 끝난 뒤 씬 재시작/게임오버 ----
        float waitTime = 2.0f;
        if (selectedClip != null)
        {
            waitTime = selectedClip.length;
        }

        // gameManager.currentLives : static 변수(어디서나 접근 가능) / gameManager.Instance : 싱글톤(씬에 하나뿐인 매니저)
        if (gameManager.currentLives <= 0)
        {
            gameManager.Instance.ResetAllData();                    // 게임오버 : 데이터 초기화 후 시작씬("basic")으로
            StartCoroutine(LoadSceneRoutine("basic", waitTime));
        }
        else
        {
            gameManager.Instance.RollbackScore();                   // 목숨 남음 : 점수 되돌리고 현재 씬 재시작
            // ※ SceneManager.GetActiveScene().name : 지금 열려있는 씬의 이름 (같은 씬을 다시 로드해서 재시작)
            string activeSceneName = SceneManager.GetActiveScene().name;
            StartCoroutine(LoadSceneRoutine(activeSceneName, waitTime));
        }
    }

    // ==================================================================
    // 추락 사망 연출 코루틴
    // (1)콜라이더를 꺼서(col.enabled = false) 추락하는 동안 바닥/함정에 안걸리고 1층까지 떨어지게 함
    // (2)death 애니메이션을 진행도(progress)에 맞춰 여러 바퀴 돌려서 "빙글빙글 추락" 연출
    //    cycle = (progress * 회전수 * 배율) % 1.0  -> % 1.0 으로 0~1 을 반복해서 애니메이션이 여러번 재생되는 효과
    // (3)1층 Y(firstFloorY) 아래로 내려가거나, 제한시간(fallDuration + 0.5초) 지나면 루프 탈출 (무한루프 방지)
    // (4)끝나면 콜라이더 복구, 정지, death 첫 프레임에 고정
    // ※ yield return null : 다음 프레임까지 대기 (매 프레임 갱신할때 사용 - 코루틴 시트 4-1)
    // ※ % (나머지 연산자) : float 에도 사용 가능 (예: 2.7f % 1.0f = 0.7)
    // ==================================================================
    IEnumerator FallDeathLandingRoutine(float fallDuration, int rotations)
    {
        anim.speed = 0f;

        if (col != null)
        {
            col.enabled = false;
        }

        float elapsed = 0f;

        while (transform.position.y > firstFloorY)
        {
            elapsed += Time.deltaTime;   // Time.deltaTime : 이전 프레임 이후 지난 시간(초). 이걸 누적하면 경과시간

            float progress = Mathf.Clamp01(elapsed / fallDuration);
            float cycle = (progress * rotations * deathSpinMultiplier) % 1.0f;

            anim.Play("death", 0, cycle * 0.98f);   // 0.98 : 1.0 이 되면 다음 반복으로 넘어가 깜빡이는 것 방지
            anim.Update(0f);

            if (elapsed >= fallDuration + 0.5f) break;

            yield return null;
        }

        if (col != null) col.enabled = true;

        rb.velocity = Vector2.zero;
        rb.gravityScale = 0f;

        anim.Play("death", 0, 0f);
        anim.Update(0f);
        anim.speed = 0f;
    }

    // ==================================================================
    // 딜레이 후 씬 로드 코루틴 (코루틴 시트 1번 "딜레이 주는 방식" 과 같은 패턴)
    // (1)WaitForSeconds(초) : 그 시간동안 대기 -> 사망 사운드가 끝날때까지 기다렸다가 씬 전환
    // (2)다른 방식 : Invoke("메소드이름", 초) 도 가능하지만 매개변수를 못넘기고 문자열 이름이라 오타 위험 -> 매개변수가 필요하면 코루틴 사용
    // ※ SceneManager.LoadScene("씬이름") : 씬 전환. 주의 : File > Build Settings 의 Scenes In Build 목록에 씬이 등록돼있어야 함
    // ==================================================================
    IEnumerator LoadSceneRoutine(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}