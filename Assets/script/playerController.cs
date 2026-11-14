using System.Collections;               // (1)의미: IEnumerator(코루틴) 쓰려면 필수. 없으면 코루틴 선언 자체가 에러.
using UnityEngine;                      // (2) MonoBehaviour, Rigidbody2D, Input 등 유니티 기본 클래스 전부 여기 있음.
using UnityEngine.SceneManagement;      // (3) SceneManager.LoadScene() 쓰려면 필요. "씬 전환"은 UnityEngine 네임스페이스에 없음!
using UnityEngine.Tilemaps;             // (4) Tilemap 클래스. 사다리가 타일맵이라 사다리 중앙 X좌표 계산할 때 씀.

public class playerController : MonoBehaviour
{
    // 컴포넌트 캐싱 변수 선언 영역
    // (1)왜 전역으로 선언하나: GetComponent<>()는 "그 순간 오브젝트에 붙은 컴포넌트를 뒤져 찾는" 메소드라 매 프레임 부르면 성능 버림.
    //   Start()에서 딱 한 번만 찾아서 여기 저장(캐싱)해두고 계속 재사용하는 기법.
    // (2)이 변수들은 "참조변수"다. 실제 데이터를 복사하는 게 아니라 메모리 주소만 저장. rb.velocity를 바꾸면 실제 리지드바디 속도가 바뀜.
    Rigidbody2D rb;          // 물리(속도·중력) 제어. 이 스크립트의 핵심.
    Animator anim;           // 애니메이션 상태 전환 및 프레임 직접 제어.
    SpriteRenderer sp;       // 스프라이트 이미지 교체(정면/측면). Idle 시 방향에 따라 갈아끼우기.
    Collider2D col;          // 충돌체. 발 위치 계산, 트랩 판정에 씀.

    // 플레이어 이동 및 물리 관련 세팅값
    // [SerializeField] (1)의미: private 필드인데도 인스펙터에 노출시켜주는 어트리뷰트.
    //   (2)왜 씀: public으로 선언하면 정보 은닉성이 깨져서 다른 스크립트가 마음대로 바꿀 수 있음.
    //   private으로 막아두되 인스펙터에서만 조절하고 싶을 때 사용. 직렬화만 시켜줄 뿐 접근제한자는 안 바꿈.
    // [Header("...")] : 인스펙터에 굵은 제목줄 그려주는 어트리뷰트. 변수 많을 때 그룹핑용. 기능은 전혀 없음.
    // [Tooltip("...")] : 인스펙터에서 마우스 올리면 말풍선 설명 뜸. 몇 달 뒤 "이 값이 뭐였지?" 방지용.
    [SerializeField] float speed = 1.15f;        // 걷기 속도 (유닛/초)
    [SerializeField] float ladderSpeed = 1f;     // 사다리 오르내리기 속도
    [SerializeField] LayerMask groundLayer;      // (1)의미: 바닥으로 판정할 레이어를 비트마스크로 지정. int형 비트마스크.
                                                 // (2)왜 필요: Linecast/Raycast 마지막 매개변수로 "어떤 레이어만 감지할지" 골라줘야
                                                 //   몬스터나 아이템에 레이가 닿아도 바닥으로 오판 안 함.

    [Header("원작 아케이드 점프 세팅 (X속도 고정 / Y성분으로 숏·롱 분단)")]
    // (1)왜 X속도를 고정하나: 일반 플랫포머는 점프 중 공중 방향전환이 되지만 원작 아케이드는 점프하면 그 순간
    //   정해진 속도로만 날아간다. 그래야 착지점 예측이 가능하고 원작의 "꽉 막힌" 조작감 재현됨.
    // (2)구현: 점프 코루틴 진입 시 axisH를 읽어 고정 dirX를 만들고, 그 뒤로는 입력 무시하고 fixedVelX 재대입.
    [Tooltip("점프 중 고정 X축 속도 (걷기 속도와 맞춰 옆으로 밀리는 가속감 제거)")]
    [SerializeField] float fixedJumpSpeedX = 1.15f;
    [Tooltip("제자리 점프 시 Y축 초기 속도 (체공 약 0.55초, 높이 약 0.36)")]
    [SerializeField] float shortJumpPowerY = 3.0f;
    [Tooltip("방향키 이동 점프 시 Y축 초기 속도 (체공 약 0.63초, 높이 약 0.49)")]
    [SerializeField] float longJumpPowerY = 3.5f;
    // (1)왜 숏/롱을 "버튼 홀드 시간"이 아니라 "방향키 입력 유무"로 분단하나:
    //   원작 폰포코는 가변 점프가 아니라 "서서 점프=낮게, 뛰면서 점프=높게"다.
    // (2)물리 근거: v = v0 - g·t, 최고점 t_peak = v0/g, 최고높이 h = v0²/(2g). v0가 3.0이냐 3.5냐에 따라 제곱으로 차이남.

    [Header("점프 3단계 중력 (상승 약함 -> 정점 체공 -> 하강 조금 강함)")]
    // (1)왜 중력을 단계별로 바꾸나: 현실 물리는 중력 일정하지만 게임은 조작감 위해 왜곡한다.
    //   올라갈 땐 약하게(천천히 뜨고), 정점에선 더 약하게(둥실 체공), 내려올 땐 강하게(후딱 떨어짐).
    //   마리오 시리즈 대표 기법. "coyote time", "jump buffering"과 함께 쓰임.
    // (2)구현: FixedUpdate에서 isJumping 중일 때 rb.velocity.y 부호·크기 보고 rb.gravityScale 매 프레임 재대입.
    //   실제 중력가속도 = Physics2D.gravity.y × gravityScale.
    [Tooltip("올라갈 때 적용되는 약한 중력")]
    [SerializeField] float riseGravity = 1.35f;
    [Tooltip("정점 부근(|velocity.y| <= peakVelocityThreshold)에서 둥근 포물선을 만드는 체공 중력")]
    [SerializeField] float peakHangGravity = 0.65f;
    [Tooltip("내려올 때 적용되는 중력 (상승보다 강하지만 압정에 처박히지 않는 수치)")]
    [SerializeField] float fallGravity = 1.85f;
    [Tooltip("정점 체공 중력이 적용되는 Y속도 임계값")]
    [SerializeField] float peakVelocityThreshold = 0.8f;
    // (1)왜 임계값 필요: velocity.y가 정확히 0이 되는 순간은 물리 엔진이 이산적(틱 단위)이라 프레임 하나에
    //   걸치지 않고 지나쳐버릴 수 있음. 그래서 "0에 가까운 구간"을 ±0.8로 넉넉히 잡아 체공 중력 적용.

    // 스프라이트 상태 전환용 이미지
    // (1)왜 스프라이트 직접 바꾸나: 달리기/점프/사다리/죽음은 애니메이션 클립으로 처리하지만,
    //   "서 있을 때(Idle) 정면이냐 측면이냐"는 클립 없이 스프라이트 한 장 갈아끼우기로 구현(아트 절약).
    // (2)사용 시점: LateUpdate에서 애니메이터 상태가 Idle일 때 방향에 따라 대입.
    [SerializeField] Sprite frontSprite;   // 정면(사다리 탈 때)
    [SerializeField] Sprite leftSprite;    // 측면(좌우 이동). 오른쪽은 localScale 반전으로 처리.

    [Header("5개 층 Y좌표 분기 기준값 (현재 맵 좌표 적용 완료)")]
    // (1)의미: 폰포코 맵은 5층. 사망 위치 Y좌표로 "몇 층에서 죽었나" 판정 → 층별 사망 음원(Death1~5) 재생.
    // (2)판정 로직: GetCurrentFloorByY()에서 위(5층)부터 차례로 비교. 순서 바꾸면(1층부터) 전부 1층으로 나옴.
    // (3)주의: 이 값들은 현재 맵 월드 좌표에 종속적. 맵을 위아래로 옮기면 이 값들도 같이 옮겨줘야 함.
    [SerializeField] float floor5MinY = 0.76f;  // 5층 기준 Y좌표
    [SerializeField] float floor4MinY = -0.20f; // 4층 기준 Y좌표
    [SerializeField] float floor3MinY = -1.16f; // 3층 기준 Y좌표
    [SerializeField] float floor2MinY = -2.12f; // 2층 기준 Y좌표
    [SerializeField] float firstFloorY = -2.60f; // 1층 맨 아래 바닥 Y좌표 (추락사 최종 도착점)

    [Header("각 층별(1층~5층) 사망 MP3 오디오 클립 (Death1 ~ Death5)")]
    // (1)왜 층별 음원 다르나: 원작은 높은 층에서 떨어질수록 비명이 길다. 추락사 시 "떨어지는 시간 = 음원길이 - 마지막 충돌음길이"
    //   로 맞춰서 바닥 부딪히는 순간과 MP3 끝의 "콰직!"이 정확히 일치하게 동기화.
    // (2)주의: AudioClip은 리소스 참조일 뿐 재생은 AudioSource(source)가 함. null이면 over(기본음)로 폴백.
    [SerializeField] AudioClip floor1Clip; // Death1 (제자리 사망 및 1층 사망 시 재생)
    [SerializeField] AudioClip floor2Clip; // Death2 (2층 추락 시 재생)
    [SerializeField] AudioClip floor3Clip; // Death3 (3층 추락 시 재생)
    [SerializeField] AudioClip floor4Clip; // Death4 (4층 추락 시 재생)
    [SerializeField] AudioClip floor5Clip; // Death5 (5층 추락 시 재생)

    [Header("추락 사운드 & 애니메이션 조절")]
    [Tooltip("MP3 파일 맨 끝에 들어있는 '콰직!' 충돌음의 길이(초). MP3 전체 길이에서 이 값을 뺀 시간 동안 낙하하여 바닥 충돌과 '콰직!' 소리를 정확히 일치시킴")]
    [SerializeField] float crashSoundDuration = 0.45f;
    [Tooltip("추락사 시 빙빙 도는 애니메이션의 회전 속도 및 횟수 배수 (기본 2.0 = 2배 빠르게 회전)")]
    [SerializeField] float deathSpinMultiplier = 2.0f;

    [Tooltip("체크하면 위층에서 fall로 떨어질 때 중간 층을 통과해 맨 아래 1층 바닥까지 추락함")]
    [SerializeField] bool fallToFirstFloor = true;

    // 입력 및 상태 체크 변수들
    // (1)왜 이것들은 [SerializeField] 안 붙이나: 에디터서 조절할 세팅값이 아니라 게임 돌아가는 동안 계속 바뀌는 상태값이라 노출할 이유 없음.
    // (2)이 변수들이 바로 "상태 머신(State Machine)"의 상태다. isJumping, ladderTrigger, isDead 등 조합으로 플레이어 상태 표현.
    float axisH = 0f;   // Input.GetAxisRaw("Horizontal") 결과. -1/0/1만 가짐(가속 없이 즉시 최대치).
    float axisV = 0f;   // Input.GetAxisRaw("Vertical") 결과. 사다리 오르내리기에만 씀.

    // (1)enum(열거형): 상수들을 묶어 의미 있는 이름으로 쓰는 자료형. int 0,1,2로 쓰면 "0이 뭐였지" 헷갈리니 이름 붙인 것.
    // (2)왜 private: 이 방향 상태는 이 스크립트 안에서만 쓰니까.
    private enum FacingState { Front, Left, Right }
    private FacingState currentFacing = FacingState.Left;  // 게임 시작 시 왼쪽 보고 서 있음.

    bool onGround = false;     // 발 밑에 바닥 닿아있는가 (점프 가능 조건)
    bool ladderTrigger = false;// 사다리 타는 중인가 (중력 0, 상하 이동만)
    bool isJumping = false;    // 점프 코루틴 도는 중인가
    bool isStunned = false;    // (현재 안 쓰는 상태. 기절/넉백 연출용으로 남겨둔 플래그)
    bool isDead = false;       // 사망 상태. true면 Update/FixedUpdate/LateUpdate 전부 early return.
    bool isLadderTop = false;  // 사다리 꼭대기 트리거 안에 있는가 (위로 더 못 올라가게 막음)

    // 사다리 범위 감지 및 현재 사다리 정중앙 X좌표, 점프 시작 높이 저장 변수
    bool isNearLadder = false;     // 사다리 트리거(ladder 태그) 안에 서 있는가
    float ladderCenterX = 0f;      // 지금 사다리 정중앙 X좌표. 사다리 타면 이 X로 스냅(고정).
    float jumpStartY = 0f;         // 점프 시작 순간 Y좌표. 착지 판정·위층 트랩 예외에 씀.

    // 오디오 소스 및 클립
    [SerializeField] AudioClip walk;  // 걷는 발걸음 소리 (반복)
    [SerializeField] AudioClip jump;  // 점프 시 "퐁" 소리
    [SerializeField] AudioClip over;  // 기본 사망음 (층별 클립 null일 때 폴백)
    AudioSource source;               // 실제 소리 재생 컴포넌트. Start에서 캐싱.

    // 라이프(목숨) UI 관리 컴포넌트 참조
    // (1)FindObjectOfType<T>(): 씬 전체 뒤져 T 타입 컴포넌트 가진 오브젝트 하나 찾아줌. Start에서 딱 한 번만 호출해 캐싱.
    // (2)주의: 이 메소드는 씬 순회라 무겁다. 매 프레임 부르면 절대 안 됨. 같은 타입이 씬에 2개 이상이면 "어떤 게 올지 보장 없음".
    // (3)참고: 클래스명 "gruopLayoutUI"는 "group" 오타로 보임. 기존 코드 유지.
    gruopLayoutUI life;

    // Start() — 오브젝트 활성화된 첫 프레임 직전에 딱 한 번 호출.
    // (1)하는 일: 필요 컴포넌트 전부 캐싱 + 초기 물리 상태 세팅.
    // (2)왜 Start에서 찾나: Awake는 씬 로딩 직후라 다른 오브젝트가 아직 준비 안 됐을 수 있고, Start는 첫 Update 직전이라
    //   씬의 모든 오브젝트가 준비된 게 보장됨.
    void Start()
    {
        // GetComponent<T>(): "이 스크립트가 붙은 오브젝트"에서 T 타입 컴포넌트 찾아옴.
        // (1)같은 오브젝트에 붙은 것만 찾음. 다른 오브젝트꺼는 Find나 FindObjectOfType으로.
        // (2)해당 컴포넌트 없으면 null 리턴되고 나중에 NullReferenceException 터짐. 인스펙터에서 미리 붙여놔야 함.
        source = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sp = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        life = FindObjectOfType<gruopLayoutUI>();  // 씬에 하나뿐인 목숨 UI 찾아 캐싱

        rb.gravityScale = fallGravity;   // 기본 중력은 하강용 중력으로. 평소 걷는 상태는 하강이 기본.
        anim.speed = 1f;                 // 애니메이터 재생 속도 1배수. (점프 중엔 0으로 만들어 프레임 수동 조작)
    }

    // Update() — 매 프레임 호출 (FPS에 따라 횟수 달라짐).
    // (1)주로 하는 일: 입력 처리(Input) + 애니메이터 파라미터 세팅 + 상태 전환 판정. 물리는 FixedUpdate에서.
    // (2)왜 입력은 Update에서 받나: Input.GetButtonDown/GetKeyDown은 "그 프레임에 눌린 순간"을 감지하는데,
    //   FixedUpdate는 50Hz라 "눌렀다 떼진" 순간을 놓칠 수 있음. 그래서 입력은 매 프레임 도는 Update에서 받아야 함.
    void Update()
    {
        // 실제 바닥 레이어 감지
        // Physics2D.Linecast(시작점, 끝점, 레이어마스크): 두 점 잇는 선분 상에 지정 레이어 콜라이더 있으면 true.
        // (1)시작점: transform.position (캐릭터 중심, pivot 기준)
        // (2)끝점: 중심에서 아래로 0.3유닛 지점. "발 밑 0.3유닛 선분" 안에 바닥 있나 봄.
        //   0.3은 발 크기보다 약간 커야 바닥에 살짝 붙어있을 때도 true. 너무 짧으면 계단 오를 때 붕 뜬다고 오판.
        // (3)왜 Raycast 아니라 Linecast: Linecast는 "두 점 사이"를 보는 거라 발 길이만큼 정확히 끊어서 볼 수 있고,
        //   Raycast는 "시점에서 무한정 뻗는 광선"이라 거리 제한을 별도로 줘야 함. 여기선 Linecast가 직관적.
        bool hitGround = Physics2D.Linecast(transform.position, transform.position - (transform.up * 0.3f), groundLayer);

        // 점프 중일 때는 공중의 사다리에 닿아도 착지로 오판하지 않고,
        // 실제 바닥에 닿았거나 점프를 시작했던 바닥 높이(jumpStartY)까지 완전히 내려왔을 때만 착지로 판정
        // (1)왜 이렇게 복잡하나: 사다리 있는 자리는 바닥에 구멍(사다리 통과구)이 뚫려있음.
        //   걸어갈 때 Linecast는 구멍 통해 아래가 뚫려있어서 false 리턴 → 플레이어가 갑자기 아래로 빠짐.
        //   그걸 막기 위해 "사다리 근처이고 사다리 안 타고 있으면 바닥으로 쳐준다"는 예외 규칙을 둠.
        // (2)점프 중일 땐 반대로 "사다리 구멍 지나 내려올 때" 바닥으로 오판하면 안 되니,
        //   실제 바닥에 닿았거나(hitGround) 점프 시작 높이까지 내려왔을 때만 착지로 침.
        if (isJumping)
        {
            bool landedOnLadderGap = (isNearLadder || isLadderTop) && (rb.velocity.y <= 0f) && (transform.position.y <= jumpStartY + 0.02f);
            onGround = hitGround || landedOnLadderGap;
        }
        else
        {
            // 걸어 다닐 때는 사다리 위쪽 구멍을 지나갈 때도 바닥으로 판정하여 아래로 빠지지 않게 유지
            onGround = hitGround || ((isNearLadder || isLadderTop) && !ladderTrigger);
        }

        // 사망 시 조기 리턴 (early return). 죽은 캐릭터가 입력 받거나 애니메이션 바꾸면 안 되니까.
        // Die()는 한 번만 호출되지만 Update는 계속 도니까 매번 체크해서 사망 연출이 깨지지 않게 함.
        if (isDead) return;

        bool isAnimationGrounded = onGround && (!isJumping || isStunned);
        anim.SetBool("IsGrounded", isAnimationGrounded);  // "서 있음"을 애니메이터에 알려 Idle/달리기로 전환.

        axisH = Input.GetAxisRaw("Horizontal");  // GetAxisRaw는 -1/0/1 세 값만 가짐(가속 없이 즉시 최대치).
        axisV = Input.GetAxisRaw("Vertical");    // GetAxis는 0~1 사이 부드러운 값. 차이 알아둘 것.

        // 1. 사다리 범위 안에서 W/윗화살표(또는 꼭대기에서 S/아랫화살표)를 누르면 사다리 정중앙으로 스냅 후 탑승
        // (1)탑승하는 순간 하는 일: ladderTrigger=true(상태전환) → X를 사다리 정중앙으로 스냅 → velocity=0(관성 방지) → 정면 스프라이트.
        if ((isNearLadder || isLadderTop) && !ladderTrigger && !isJumping && !isStunned)
        {
            bool pressUp = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            bool pressDownAtTop = isLadderTop && (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow));

            if (pressUp || pressDownAtTop)
            {
                ladderTrigger = true;
                transform.position = new Vector3(ladderCenterX, transform.position.y, transform.position.z);
                rb.velocity = Vector2.zero;
                currentFacing = FacingState.Front;
            }
        }

        // 2. 사다리 꼭대기(isLadderTop)나 맨 아래 바닥(hitGround)에서 탈출 조건 처리
        // (1)탈출 조건1: 꼭대기나 맨 아래 바닥에서 좌우 방향키 누름 → 옆으로 빠져나감
        // (2)탈출 조건2: 맨 아래 바닥에 서 있고 위로 안 올라감(axisV<=0) → 그냥 내려놓음
        if (ladderTrigger)
        {
            if (Mathf.Abs(axisH) > 0.1f && (isLadderTop || (hitGround && axisV <= 0.1f)))
            {
                ExitLadderToGround();
            }
            else if (hitGround && axisV <= 0.0f)
            {
                ExitLadderToGround();
            }
        }

        // 바라보는 방향(스프라이트 반전) 처리.
        // (1)왜 localScale.x를 -1로 만드나: 스프라이트를 "좌우 반전"시키는 가장 간단한 방법.
        //   오른쪽 이미지 따로 안 만들어도 왼쪽 스프라이트 하나로 좌우 커버. (SpriteRenderer.flipX 프로퍼티도 있지만 여기선 scale 방식 사용)
        // (2)조건: 점프 중도 아니고 기절도 아닐 때만 방향 바꿈. 점프 중엔 방향 고정이니까.
        if (!isJumping && !isStunned)
        {
            if (!ladderTrigger)
            {
                if (axisH > 0.0f)
                {
                    transform.localScale = new Vector2(-1, 1);  // 오른쪽 볼 땐 스프라이트 반전
                    currentFacing = FacingState.Right;
                }
                else if (axisH < 0.0f)
                {
                    transform.localScale = new Vector2(1, 1);   // 왼쪽 볼 땐 원본 그대로
                    currentFacing = FacingState.Left;
                }
            }
            else if (Mathf.Abs(axisV) > 0.1f)
            {
                currentFacing = FacingState.Front;  // 사다리 타며 위아래 누르면 정면으로
            }
        }

        // 점프 중이거나 기절 중이면 달리기 애니메이션 끄고 이번 프레임 종료(return). 입력·점프 판정 스킵.
        if (isJumping || isStunned)
        {
            anim.SetBool("IsRunning", false);
            return;
        }

        bool isRunning = Mathf.Abs(axisH) > 0.1f && !ladderTrigger;  // 좌우 입력 있고 사다리 안 타면 달리는 중
        anim.SetBool("IsRunning", isRunning);       // 달리기 ↔ Idle 전환
        anim.SetBool("IsClimbing", ladderTrigger);  // 사다리 타는 중이면 Climb 애니메이션으로

        // 걷기 소리: 달리는 중이고 바닥에 있고 소리 안 나고 있을 때만 재생.
        // (1)source.isPlaying 체크: PlayOneShot은 중첩 재생되니 매 프레임 부르면 소리가 수백 겹으로 겹침.
        //   "지금 재생 중이 아니면" 한 번만 틀게 함. (2)달리기 멈추면 소리도 Stop.
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

        if (ladderTrigger)
        {
            anim.SetFloat("ClimbSpeed", Mathf.Abs(axisV));  // 위아래 입력 크기 넣어줌. 0이면 애니메이션 정지.
        }

        // 점프 입력. Input.GetButtonDown("Jump"): "이번 프레임에 점프 버튼이 막 눌렸을 때" true.
        // (1)Input Manager의 "Jump" 축에 매핑된 키(기본 스페이스바). 누르고 있으면 매 프레임 true가 되는 게 아니라
        //   "처음 누른 그 프레임"만 true라서 점프가 한 번만 됨.
        // (2)조건: 땅에 붙어있고(onGround) 사다리 안 타는 중 — 사다리 위에선 점프 불가.
        // (3)점프는 코루틴으로 구현. 코루틴 써야 "점프 전 과정을 한 로직으로 추적"하며 매 FixedUpdate마다
        //   애니메이션 프레임을 동기화할 수 있음.
        if (Input.GetButtonDown("Jump") && onGround && !ladderTrigger)
        {
            StartCoroutine(PonpokoJumpRoutine());
        }
    }

    // FixedUpdate() — 물리 틱(기본 50Hz)마다 호출. FPS와 무관.
    // (1)하는 일: 오직 "물리(Rigidbody2D velocity, gravityScale)"만 건드림.
    //   물리는 일정 간격으로 시뮬레이션돼야 "프레임 떨어져도 똑같이 움직"이니까 여기서 함.
    // (2)왜 Update에서 velocity 안 바꾸나: Update는 FPS에 따라 호출 횟수 들쭉날쭉해서 거기서 속도 바꾸면
    //   고사양에선 빨리, 저사양에선 느리게 움직임.
    void FixedUpdate()
    {
        if (isDead) return;  // 사망 중이면 물리도 스킵 (Die()에서 이미 중력 0으로 만들었음)

        // 점프 중 3단계 중력 제어: 상승(약함) -> 정점 부근(체공) -> 하강(강함)
        // (1)왜 매 FixedUpdate마다 대입하나: gravityScale은 계속 유지되는 값이지만 점프 중엔 velocity.y가
        //   계속 변하니 "지금 몇 단계인지"를 매번 재판정해서 바꿔줘야 함.
        // (2)return: 점프 중이면 아래(사다리/걷기) 물리 로직은 스킵. 점프 코루틴이 velocity를 직접 관리하기 때문.
        if (isJumping)
        {
            if (Mathf.Abs(rb.velocity.y) <= peakVelocityThreshold)
            {
                rb.gravityScale = peakHangGravity;  // 정점 부근: 체공 중력(가장 약함)
            }
            else if (rb.velocity.y > 0f)
            {
                rb.gravityScale = riseGravity;      // 상승 중: 약한 중력
            }
            else
            {
                rb.gravityScale = fallGravity;      // 하강 중: 강한 중력
            }
            return;
        }

        if (isStunned) return;  // 기절 중도 물리 스킵 (현재 isStunned은 항상 false라 사실상 안 씀)

        if (ladderTrigger)
        {
            // 사다리 타는 중 물리: 중력 0, 이동은 오직 상하로만, X좌표는 사다리 중앙에 고정.
            rb.gravityScale = 0f;  // 사다리 타는 중엔 중력 작용하면 안 됨
            float currentLadderSpeed = ladderSpeed;

            if (isLadderTop && axisV > 0f)
            {
                currentLadderSpeed = 0f;  // 꼭대기에서 위로는 못 올라감
            }

            rb.velocity = new Vector2(0f, axisV * currentLadderSpeed);  // X는 0 강제, Y는 입력×속도
            rb.position = new Vector2(ladderCenterX, rb.position.y);    // X를 사다리 중앙에 계속 덮어씀 → 이탈 방지

            // 사다리를 타고 내려오는 중 실제 바닥에 도달하면 즉시 사다리 탈출 및 밀착
            // footOffset: 콜라이더 중심에서 발바닥까지 거리. BoxCollider2D면 정확히 계산.
            float footOffset = 0.25f;  // 기본값 (콜라이더 없을 경우 대비)
            BoxCollider2D boxCol = col as BoxCollider2D;  // (1)as 캐스팅: col이 BoxCollider2D면 변환, 아니면 null.
            if (boxCol != null) footOffset = boxCol.size.y * 0.5f - boxCol.offset.y;
            // (1)계산식: BoxCollider2D 세로 절반(size.y*0.5)에서 offset.y 빼면 "중심에서 발바닥까지 거리" 나옴.

            RaycastHit2D downHit = Physics2D.Raycast(transform.position, Vector2.down, footOffset + 0.2f, groundLayer);
            if (axisV <= 0f && downHit.collider != null)  // 내려가는 중이고 발 밑에 바닥 감지되면 탈출
            {
                ExitLadderToGround();
            }
        }
        else
        {
            // 일반 걷기 상태 물리.
            // (1)사다리 근처(isNearLadder||isLadderTop)면 중력 0 + Y속도 0 → 사다리 구멍 위에 서 있을 때
            //   아래로 빨려들어가지 않게 "공중에 살짝 붙잡아주는" 효과. (사다리 구멍은 바닥이 뚫려 Linecast가 false 나오니 이 보정 필요)
            // (2)평소엔 fallGravity 적용하고 X속도는 axisH×speed로 직접 세팅.
            //   velocity 직접 세팅 = AddForce보다 조작감 좋음 (관성력 무시 가능).
            rb.gravityScale = (isNearLadder || isLadderTop) ? 0f : fallGravity;
            float targetVelY = (isNearLadder || isLadderTop) ? 0f : rb.velocity.y;
            rb.velocity = new Vector2(axisH * speed, targetVelY);
        }
    }

    // LateUpdate() — 매 프레임 가장 마지막에 호출.
    // (1)하는 일: 애니메이터가 이번 프레임에 어떤 상태(클립)를 재생했는지 확정된 뒤에,
    //   "Idle 상태일 때만" 스프라이트 이미지를 정면/측면으로 갈아끼움.
    // (2)왜 LateUpdate에서 하나: Update에서 애니메이터 파라미터를 바꾸면 애니메이터는 그 프레임 중간에 상태 전환함.
    //   "최종적으로 어떤 상태가 재생됐는지"는 LateUpdate 시점에 와야 확정됨. 그래서 스프라이트 교체는 여기서.
    void LateUpdate()
    {
        if (isDead) return;

        // anim.GetCurrentAnimatorStateInfo(0): 애니메이터 레이어 0번의 "지금 재생 중인 상태 정보".
        // state.IsName("Idle"): 지금 재생 중인 클립 이름이 "Idle"인가? (대소문자 주의)
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);

        if (!isJumping && state.IsName("Idle"))
        {
            if (currentFacing != FacingState.Front)
            {
                sp.sprite = leftSprite;   // 측면 보는 서있기 → 측면 스프라이트
            }
            else
            {
                sp.sprite = frontSprite;  // 정면 보는 서있기 → 정면 스프라이트
            }
        }
    }

    // GetLadderCenterX() — 사다리 콜라이더로부터 "사다리 정중앙 X좌표" 계산
    // (1)왜 필요하나: 사다리 타면 캐릭터를 사다리 줄 정중앙에 스냅시켜야 하는데, 사다리가 "타일맵"으로 만들어져
    //   있으면 콜라이더 bounds.center가 사다리 전체 중심이 아니라 "타일맵 전체" 중심일 수 있음. 그래서 직접 타일 훑어서 계산.
    // (2)처리 순서(3가지 케이스 폴백 구조):
    //   ① Tilemap 컴포넌트 있으면 → 주변 타일 훑어 실제 사다리 타일의 X 범위 구해 중앙 계산
    //   ② BoxCollider2D가 여러 개(사다리가 여러 줄)면 → 플레이어와 가장 가까운 콜라이더의 중앙 사용
    //   ③ 그 외(콜라이더 하나)면 → 그냥 bounds.center.x 사용
    // (3)타일맵 계산 로직: WorldToCell(월드좌표)=월드좌표를 격자좌표(정수)로 변환 / HasTile(셀)=그 칸에 타일 있나 bool
    //   / GetCellCenterWorld(셀)=그 격자 칸의 월드 중심 좌표 리턴. 플레이어 주변 ±2칸 훑어 minX/maxX 구해 평균 = 중앙.
    float GetLadderCenterX(Collider2D ladderCol)
    {
        Tilemap tilemap = ladderCol.GetComponent<Tilemap>();
        if (tilemap != null)
        {
            Vector3Int playerCell = tilemap.WorldToCell(transform.position);  // 플레이어가 있는 격자 칸
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            bool found = false;

            // dy=1(한 칸 위)부터 dy=-1(한 칸 아래)로 훑음. 타일 찾은 행에서 멈춤.
            for (int dy = 1; dy >= -1; dy--)
            {
                for (int dx = -2; dx <= 2; dx++)  // 좌우 ±2칸 훑기
                {
                    Vector3Int checkCell = new Vector3Int(playerCell.x + dx, playerCell.y + dy, playerCell.z);
                    if (tilemap.HasTile(checkCell))
                    {
                        if (checkCell.x < minX) minX = checkCell.x;
                        if (checkCell.x > maxX) maxX = checkCell.x;
                        found = true;
                    }
                }
                if (found) break;  // 타일 찾은 행에서 멈춤 (가장 가까운 행의 폭을 씀)
            }

            if (found)
            {
                // 양 끝 칸의 월드 중심 좌표 구해서 평균 = 사다리 정중앙 X
                Vector3 minCenter = tilemap.GetCellCenterWorld(new Vector3Int(minX, playerCell.y, playerCell.z));
                Vector3 maxCenter = tilemap.GetCellCenterWorld(new Vector3Int(maxX, playerCell.y, playerCell.z));
                return (minCenter.x + maxCenter.x) * 0.5f;
            }
        }

        // 타일맵 아니면 BoxCollider2D 여러 개인지 검사 (사다리가 여러 줄일 수 있음)
        BoxCollider2D[] boxes = ladderCol.GetComponents<BoxCollider2D>();
        if (boxes.Length > 1)
        {
            float closestX = transform.position.x;
            float minDistance = float.MaxValue;
            foreach (BoxCollider2D box in boxes)
            {
                float dist = Mathf.Abs(box.bounds.center.x - transform.position.x);
                if (dist < minDistance)  // 플레이어와 가장 가까운 사다리 줄의 중앙을 고름
                {
                    minDistance = dist;
                    closestX = box.bounds.center.x;
                }
            }
            return closestX;
        }

        return ladderCol.bounds.center.x;  // 그냥 콜라이더 하나면 그 중심 X를 그대로 씀
    }

    // [사다리 탈출 함수: 레이 거리를 짧게(0.6f) 제한하여 현재 층 바닥만 정확히 스냅]
    // (1)왜 필요하나: 사다리에서 그냥 ladderTrigger=false만 하면 중력이 다시 적용되면서 캐릭터가 한 프레임 동안
    //   살짝 추락해서 "바닥에 툭 떨어지는" 현상 생김. 그래서 아래로 짧은 Raycast 쏴서 "바로 아래 바닥" 찾아
    //   발 위치를 그 높이에 강제 스냅시켜 부드럽게 착지하게 만듦.
    // (2)왜 레이 거리를 0.6f로 짧게 제한하나: 길게 쏘면 "아래층 바닥"까지 감지해서 캐릭터가 아래층으로 순간이동해버림.
    //   그래서 "지금 서 있는 층 바닥만" 감지되게 짧게 제한한 것. (이 버그를 실제로 겪어서 이렇게 만든 것으로 보임)
    // (3)footOffset: 발바닥 위치 = 중심 - footOffset. 그래서 바닥 Y + footOffset이 캐릭터 중심이 가야 할 좌표.
    void ExitLadderToGround()
    {
        ladderTrigger = false;  // 사다리 상태 해제

        // 발 오프셋 계산 (BoxCollider2D면 정확히, 아니면 콜라이더 외형 절반, 기본 0.25)
        float footOffset = 0.25f;
        BoxCollider2D boxCol = col as BoxCollider2D;
        if (boxCol != null)
        {
            footOffset = boxCol.size.y * 0.5f - boxCol.offset.y;
        }
        else if (col != null)
        {
            footOffset = col.bounds.extents.y;  // extents = bounds의 절반 크기
        }

        // 아래로 짧게(0.6f) 레이를 쏴서 현재 층의 바닥만 정확히 감지
        RaycastHit2D groundHit = Physics2D.Raycast(transform.position, Vector2.down, 0.6f, groundLayer);
        if (groundHit.collider != null)
        {
            float targetY = groundHit.point.y + footOffset;  // 바닥에 발 붙인 중심 Y
            Vector2 targetPos = new Vector2(transform.position.x, targetY);
            transform.position = targetPos;  // 트랜스폼 위치도
            rb.position = targetPos;         // 리지드바디 위치도 동시에 맞춰줌 (안 그러면 다음 물리틱에 원위치)
        }

        // 사다리 탈출 직후 걷기 상태로 자연스럽게 이어지게 속도와 중력 복구
        rb.velocity = new Vector2(axisH * speed, 0f);
        rb.gravityScale = fallGravity;
    }

    // PonpokoJumpRoutine() — 점프 전 과정 관리 코루틴 (핵심!)
    // (1)왜 코루틴으로 구현했나: 점프는 "시작→상승→정점→하강→착지"라는 시간에 따른 흐름이 있는데,
    //   이걸 Update에서 if문으로 상태 관리하면 변수가 10개는 필요함. 코루틴은 "한 함수 안에 흐름을 쭉 적고
    //   yield로 프레임 넘기면서" 처리할 수 있어서 훨씬 직관적.
    // (2)하는 일: ①점프 직후 X방향·Y초기속도 "고정"(원작 아케이드 스타일)
    //   ②애니메이터를 "프레임 수동 조작 모드"로 전환(anim.speed=0 + Play(정규화시간) + Update(0))
    //   ③매 FixedUpdate마다 현재 물리 상태(상승률/높이) 보고 점프 애니메이션 프레임을 5→10→15→20→25 키프레임에 정확히 매핑
    //   ④착지하면 코루틴 종료, 달리기/Idle로 자연스럽게 복귀
    // (3)★★★ 애니메이션 프레임 수동 제어 기법 (잘 안 쓰는 기법이니 기억해둘 것) ★★★
    //   - anim.speed = 0f: 애니메이터의 "자동 재생"을 멈춤. 시간이 흘러도 프레임이 안 바뀜.
    //   - anim.Play(해시, 레이어, normalizedTime): 지정한 "정규화 시간(0~1)" 위치로 강제 이동.
    //     여기선 5f/30f = "30fps 기준 5프레임 지점". 즉 첫 5프레임(서 있는 Idle 구간)은 건너뛰고 공중 첫 자세부터 시작.
    //   - anim.Update(0f): 애니메이터를 "0초만큼 진행"시킴. 즉 방금 Play로 지정한 프레임을 "이번 프레임에 즉시 적용"하라는
    //     강제 갱신 호출. 이걸 안 하면 다음 프레임에나 반영됨.
    //   - 이 셋트(speed=0 → Play(normalizedTime) → Update(0))를 매 프레임 반복하면 "시간이 아니라 내가 원하는 값으로
    //     애니메이션 프레임을 움직일" 수 있음. (영상 편집의 "스크럽(scrub)"과 같음. 물리 높이에 따라 프레임 싱크시킬 때 씀)
    IEnumerator PonpokoJumpRoutine()
    {
        isJumping = true;
        jumpStartY = transform.position.y;

        source.Stop();
        source.PlayOneShot(jump);

        // 1. X축 속도 고정 (공중 방향전환 차단)
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
            else dirX = (transform.localScale.x < 0f) ? 1f : -1f;
        }

        float fixedVelX = dirX * fixedJumpSpeedX;

        // 2. 숏/롱 점프를 Y축 초기 속도로 분단
        float selectedJumpY = (Mathf.Abs(axisH) > 0.1f) ? longJumpPowerY : shortJumpPowerY;

        int jumpStateHash = anim.HasState(0, Animator.StringToHash("jump"))
            ? Animator.StringToHash("jump")
            : Animator.StringToHash("Jump");

        // 첫 공중 자세 키프레임(4프레임)에서 시작
        anim.speed = 0f;
        anim.Play(jumpStateHash, 0, 4f / 30f);
        anim.Update(0f);

        rb.gravityScale = riseGravity;
        rb.velocity = new Vector2(fixedVelX, selectedJumpY);

        // ★ 총 공중 시간을 중력 상수로부터 정확히 역산 (3단계 중력 고려)
        //   상승1: v0 -> thr 까지 riseGravity / 상승2(체공): thr -> 0 까지 peakHangGravity
        //   하강1(체공): 0 -> thr 까지 peakHangGravity / 하강2: 나머지 높이를 fallGravity로
        //   이렇게 구한 totalAirTime 동안 4/30 -> 30/30 을 등속으로 재생하면
        //   착지 순간 정확히 마지막 프레임(30, 서 있는 자세)에서 끝나 스냅이 없음.
        float g = Mathf.Abs(Physics2D.gravity.y);
        float thr = Mathf.Max(0.01f, peakVelocityThreshold);
        float v0 = Mathf.Max(thr + 0.01f, selectedJumpY);

        float riseT1 = (v0 - thr) / (g * riseGravity);
        float riseH1 = (v0 * v0 - thr * thr) / (2f * g * riseGravity);
        float riseT2 = thr / (g * peakHangGravity);
        float riseH2 = (thr * thr) / (2f * g * peakHangGravity);
        float peakH = riseH1 + riseH2;

        float fallT1 = thr / (g * peakHangGravity);
        float fallH2 = peakH - (thr * thr) / (2f * g * peakHangGravity); // 하강2 구간 높이
        float fallT2 = (Mathf.Sqrt(thr * thr + 2f * g * fallGravity * Mathf.Max(0.001f, fallH2)) - thr) / (g * fallGravity);

        float totalAirTime = Mathf.Max(0.2f, riseT1 + riseT2 + fallT1 + fallT2);
        float jumpStartTime = Time.time;

        yield return new WaitForFixedUpdate();

        bool hasReachedPeak = false;  // 착지 판정용으로만 사용 (애니메이션 매핑에는 쓰지 않음)

        while (true)
        {
            if (isDead) yield break;

            // 정점 도달은 착지 브레이크 조건에만 씀 (최초 1회만 기록)
            if (!hasReachedPeak && rb.velocity.y <= 0f)
            {
                hasReachedPeak = true;
            }

            // ★ 단일 등속 매핑: 경과 시간 비율로 4프레임 -> 30프레임을 선형 보간
            //   구간 전환(상승/하강)이 없어 정점에서 끊김 없고, 추정 오차도 없음.
            float t = Mathf.Clamp01((Time.time - jumpStartTime) / totalAirTime);
            float animNormalizedTime = Mathf.Lerp(4f / 30f, 30f / 30f, t);

            rb.velocity = new Vector2(fixedVelX, rb.velocity.y);
            anim.Play(jumpStateHash, 0, animNormalizedTime);
            anim.Update(0f);

            if (hasReachedPeak && onGround)
            {
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        // 사다리 구멍 위 착지 시 점프 시작 높이로 정렬
        bool hitSolidGround = Physics2D.Linecast(transform.position, transform.position - (transform.up * 0.25f), groundLayer);
        if (!hitSolidGround && (isNearLadder || isLadderTop))
        {
            transform.position = new Vector3(transform.position.x, jumpStartY, transform.position.z);
        }

        isStunned = false;
        isJumping = false;
        anim.speed = 1f;

        if (Mathf.Abs(axisH) > 0.1f && !ladderTrigger)
        {
            rb.velocity = new Vector2(axisH * speed, 0f);
            anim.SetBool("IsGrounded", true);
            anim.SetBool("IsRunning", true);
        }
        else
        {
            rb.velocity = Vector2.zero;
            anim.Play("Idle", 0);
        }
    }



    // [위층 트랩 예외 처리] 점프 중 캐릭터의 머리(중심보다 위쪽)가 천장 쪽(jumpStartY + 0.20f 이상) 트랩 밑면에 닿았을 때만 무시
    // (1)왜 필요하나: 폰포코 맵은 5층 구조라서, 플레이어가 3층에서 점프해 올라가다가 "4층 바닥에 달린 트랩(스파이크)"의
    //   "밑면"에 머리를 부딪힐 수 있음. 그런데 트랩은 OnTriggerEnter로 "닿으면 죽음" 처리돼 있어서, 머리가 밑면에
    //   스치기만 해도 죽어버림. 원작은 "트랩은 밟아야(위에서 닿아야) 죽지, 밑에서 머리 부딪힌 건 안 죽는다"는 룰이라서
    //   "위쪽(머리)에서 닿은 트랩 충돌은 무시"하는 예외 로직이 필요한 것.
    // (2)판정 방법: ①점프 중이어야 함 ②Physics2D.Distance(col, trapCol): 두 콜라이더 사이의 "가장 가까운 점들(pointA, pointB)"과
    //   거리를 계산해주는 메소드. ColliderDistance2D 구조체 리턴. ③접촉 지점(contactY)이 "캐릭터 중심보다 위쪽"=머리 부분이고
    //   ④접촉 지점이 "점프 시작 높이보다 0.2유닛 이상 위"=즉 "위층"에 있는 트랩이다 → 위 두 조건 만족하면 무시(true 리턴).
    // (3)Physics2D.Distance vs OnCollision: 트리거(Trigger)는 물리 충돌이 아니라 "겹침"이라서 collision.contacts(접촉점 정보)가
    //   없음. 그래서 두 콜라이더의 가장 가까운 점을 Distance()로 직접 계산해서 접촉 위치를 추정하는 것. (트리거의 한계를 우회하는 기법)
    bool IsUpperFloorTrap(Collider2D trapCol)
    {
        if (!isJumping || col == null) return false;

        ColliderDistance2D dist = Physics2D.Distance(col, trapCol);
        if (!dist.isValid) return false;  // (1)isValid: 계산이 유효한지. 콜라이더가 비활성이면 false.

        // 실제 충돌이 일어난 지점의 Y좌표 (두 점 중 더 위에 있는 점이 접촉 지점)
        float contactY = Mathf.Max(dist.pointA.y, dist.pointB.y);
        float playerCenterY = col.bounds.center.y;

        // 1) 충돌 지점이 캐릭터 몸통 중심보다 위쪽(머리)이고
        // 2) 점프를 시작한 바닥의 캐릭터 Y좌표(jumpStartY)보다 0.20유닛 이상 높은 천장 위치일 때만 위층 트랩으로 판정
        return (contactY > playerCenterY) && (contactY > jumpStartY + 0.20f);
    }

    // OnTriggerEnter2D / OnTriggerStay2D / OnTriggerExit2D — 트리거 콜라이더 겹침 이벤트 콜백 (2D 전용!)
    // (1)트리거(Trigger)란: 콜라이더의 "Is Trigger" 체크박스 켜면 물리 충돌(밀어내기)이 없어지고 그냥 "겹쳤다"는
    //   이벤트만 받음. 아이템·사다리·트랩처럼 "닿았음을 감지만" 하면 되는 것들에 씀.
    // (2)세 콜백 차이: Enter="이번 프레임에 막 겹치기 시작했을 때" 1회 / Stay="겹쳐있는 동안" 매 프레임 / Exit="벗어났을 때" 1회.
    // (3)왜 monster/trap/fall은 Enter와 Stay 둘 다에 넣었나: Enter는 한 번만 호출되니까, 만약 그 한 프레임에 isDead 가드나
    //   다른 로직으로 씹히면 죽음 처리 누락됨. Stay에서도 계속 체크해서 "닿아있는 한 확실히 죽게" 만든 것.
    //   (중복 호출은 Die() 안의 if(isDead) return 가드가 막아줌)
    // (4)태그 목록 (씬에서 미리 Tags & Layers에 등록해야 함): ladder(사다리 몸통) / laddertop(사다리 꼭대기) /
    //   monster(몬스터) / trap(트랩/스파이크) / fall(낙사 구멍)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag.Equals("ladder"))  // tag.Equals: 문자열 비교. tag는 string이라 ==으로도 되지만 여기선 Equals 사용.
        {
            isNearLadder = true;
            ladderCenterX = GetLadderCenterX(collision);  // 사다리 정중앙 계산해 저장
        }
        else if (collision.tag.Equals("laddertop"))
        {
            isLadderTop = true;
        }
        else if (collision.tag.Equals("monster"))
        {
            Die(DeathType.OnFloor);  // 몬스터 닿음 = 제자리 사망(OnFloor)
        }
        else if (collision.tag.Equals("trap"))
        {
            if (IsUpperFloorTrap(collision)) return;  // 위층 트랩 밑면에 머리 부딪힌 거면 무시
            Die(DeathType.OnFloor);
        }
        else if (collision.tag.Equals("fall"))
        {
            if (IsUpperFloorTrap(collision)) return;
            Die(DeathType.FallDown);  // 낙사 구멍에 빠짐 = 추락사(FallDown)
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Enter와 로직 동일. "계속 닿아있는 동안" 확실히 처리하기 위해 중복 구현.
        if (collision.tag.Equals("ladder") && !ladderTrigger)
        {
            isNearLadder = true;
            ladderCenterX = GetLadderCenterX(collision);
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
            isNearLadder = false;
            if (ladderTrigger)
            {
                ExitLadderToGround();  // 사다리 범위 벗어났는데 아직 타는 중이면 강제 탈출
            }
            ladderTrigger = false;
            currentFacing = FacingState.Front;  // 사다리 벗어나면 일단 정면 상태로 (이후 이동 시 다시 전환)
        }
        else if (collision.tag.Equals("laddertop"))
        {
            isLadderTop = false;
        }
    }

    // GetCurrentFloorByY() — Y좌표로 "지금 몇 층인지" 판정
    // (1)판정 로직: 위에서부터(5층→1층) 차례로 비교. y >= floor5MinY면 5층, 아니면 y >= floor4MinY면 4층...
    //   어디에도 안 걸리면 1층. (if-else if 체인)
    // (2)왜 위에서부터 비교하나: floor5MinY가 가장 크고 floor2MinY가 가장 작음. 만약 아래(1층)부터 비교하면
    //   "y >= -2.12" 같은 낮은 기준에 전부 걸려서 5층에 있어도 1층으로 판정됨. 그래서 "가장 높은 기준"부터 비교해야 함.
    // (3)이 값은 사망 음원 고를 때만 씀. (floor1~5Clip 선택)
    int GetCurrentFloorByY(float y)
    {
        if (y >= floor5MinY) return 5;
        else if (y >= floor4MinY) return 4;
        else if (y >= floor3MinY) return 3;
        else if (y >= floor2MinY) return 2;
        else return 1;
    }

    // 사망 유형 열거형: OnFloor(제자리/트랩/몬스터 사망) / FallDown(추락사)
    // (1)왜 enum으로 나누나: 사망 연출이 완전히 다름. OnFloor는 그 자리에서 죽는 척(애니메이션 0프레임에 고정)이고,
    //   FallDown은 맨 아래까지 회전하며 떨어지는 연출. 그래서 유형을 나눠 Die() 안에서 분기 처리.
    public enum DeathType { OnFloor, FallDown }

    // Die() — 사망 처리 (가장 복잡한 메소드)
    // (1)처리 순서: ①중복 사망 방지 가드 ②사망 상태 세팅 + 모든 코루틴 중지 + 목숨 차감 + 애니메이터/오디오 정지
    //   ③사망한 층 판정 → 층별 사망 음원 선택 ④사망 유형별 연출 분기
    //     - OnFloor 또는 1층: 그 자리에서 중력 0, death 애니메이션 0프레임에 고정
    //     - FallDown(2층 이상): "음원 길이에 맞춘 낙하 시간" 계산 → 그 시간 안에 firstFloorY(맨 아래)까지 도달하도록
    //       중력을 "역산"해서 설정 → 회전 낙하 코루틴 시작
    //   ⑤목숨 0이면 게임오버(데이터 리셋 + basic 씬), 아니면 점수 롤백 + 현재 씬 리로드
    // (2)★★★ 중력 역산 공식 (물리 유도 — 나중에 비슷한 거 만들 때 써먹을 것) ★★★
    //   등가속도 운동 공식: s = v₀·t + ½·a·t²
    //   낙하는 초기속도 v₀ = 0 이므로 → s = ½·a·t² → a = 2s / t²
    //   - s = 낙하 거리 = startY - firstFloorY
    //   - t = 낙하 시간 = 음원길이 - crashSoundDuration (충돌음 직전까지 도달해야 하니까)
    //   - a = 필요한 중력가속도
    //   유니티의 실제 중력가속도 = Physics2D.gravity.y × gravityScale  (Physics2D.gravity.y는 기본 -9.81. 음수라 크기만 쓸 거니 Abs)
    //   따라서 gravityScale = a / |Physics2D.gravity.y|
    //   → 이렇게 하면 "음원의 비명이 끝나는 타이밍 = 바닥에 도착하는 타이밍"이 정확히 일치한다.
    // (3)gameManager 참조 방식이 혼합되어 있음 (주의):
    //   - gameManager.currentLives: "정적(static) 필드"로 직접 접근 (인스턴스 안 통함)
    //   - gameManager.Instance.ResetAllData() / RollbackScore(): "싱글톤 인스턴스"로 접근
    //   둘 다 같은 클래스인데 접근 방식이 다름. gameManager 쪽에서 currentLives만 static으로 선언한 듯.
    //   일관성은 없지만 작동은 함.
    public void Die(DeathType deathType = DeathType.OnFloor)
    {
        if (isDead) return;  // 중복 사망 방지 (OnTriggerStay에서 계속 불리니까 필수)

        isDead = true;
        StopAllCoroutines();  // (1)의미: 이 MonoBehaviour에서 돌아가는 "모든 코루틴"을 즉시 중단.
                              //   점프 코루틴이 돌아가는 중에 죽으면 그것도 강제 종료시켜야 하니까.
                              // (2)주의: "이 스크립트의" 코루틴만 멈춤. 다른 오브젝트의 코루틴은 안 멈춤.

        if (life != null) life.LoseLife();  // 목숨 UI에서 목숨 하나 차감 (null 체크는 Find 실패 대비)

        // 애니메이터 파라미터 전부 초기화 (사망 애니메이션으로 깔끔하게 전환되게)
        anim.SetBool("IsRunning", false);
        anim.SetBool("IsClimbing", false);
        anim.SetBool("IsGrounded", false);
        source.Stop();  // 걷기/점프 소리 끄기

        // 사망한 층 판정 (사망 음원 선택용)
        int currentFloor = GetCurrentFloorByY(transform.position.y);
        Debug.Log($"사망 Y좌표: {transform.position.y:F2} -> {currentFloor}층 판정 (유형: {deathType})");
        // (1)보간 문자열 $"": C# 6.0부터 지원. 변수를 { } 안에 넣으면 문자열에 박아줌. :F2는 소수점 둘째자리까지 = ToString("F2").

        AudioClip selectedClip = over;  // 기본값 = over (폴백)

        /* ---------- 사망 유형 분기 ---------- */
        if (deathType == DeathType.OnFloor || currentFloor == 1)
        {
            // 제자리 사망 또는 1층 사망: 그 자리에서 움직임 완전히 멈추고 사망 자세로 고정
            rb.velocity = Vector2.zero;
            rb.gravityScale = 0f;  // 중력 끄기 (떨어지지 않게)

            // death 애니메이션의 "0프레임(첫 자세)"에 고정. speed=0으로 자동재생 멈춤.
            anim.Play("death", 0, 0f);
            anim.Update(0f);
            anim.speed = 0f;

            selectedClip = (floor1Clip != null) ? floor1Clip : over;  // 1층 음원, 없으면 over
            if (selectedClip != null) source.PlayOneShot(selectedClip);
        }
        else if (deathType == DeathType.FallDown)
        {
            // 추락사 (2층~5층): 회전하며 맨 아래까지 떨어지는 연출
            rb.velocity = Vector2.zero;  // 일단 가속도 0에서 시작 (중력으로만 떨어지게)

            // 층별 음원 선택 (없으면 over로 폴백)
            switch (currentFloor)
            {
                case 2: selectedClip = (floor2Clip != null) ? floor2Clip : over; break;
                case 3: selectedClip = (floor3Clip != null) ? floor3Clip : over; break;
                case 4: selectedClip = (floor4Clip != null) ? floor4Clip : over; break;
                case 5: selectedClip = (floor5Clip != null) ? floor5Clip : over; break;
            }

            if (selectedClip != null) source.PlayOneShot(selectedClip);

            // 낙하 시간 = 음원 전체 길이 - 마지막 충돌음 길이
            // (1)왜 충돌음을 빼나: MP3 끝부분의 "콰직!"은 바닥에 부딪히는 순간에 나야 하니까, 그 직전까지가 "낙하 구간".
            //   충돌음 길이만큼을 미리 빼줘야 싱크가 맞음.
            // (2)Mathf.Max(0.25f, ...): 음원이 너무 짧아도 최소 0.25초는 확보 (0초 낙하 방지)
            float fallDuration = 0.5f;
            if (selectedClip != null)
            {
                fallDuration = Mathf.Max(0.25f, selectedClip.length - crashSoundDuration);
            }

            // 낙하 거리 = 시작 Y - 맨 아래 바닥 Y
            float startY = transform.position.y;
            float targetY = firstFloorY;
            float fallDistance = Mathf.Max(0.2f, startY - targetY);  // 최소 거리 확보 (0 나누기 방지)

            // ★ 중력 역산: a = 2s / t²  (위에서 유도한 공식)
            float requiredGravity = (2f * fallDistance) / (fallDuration * fallDuration);
            // 유니티 중력 = Physics2D.gravity.y × gravityScale 이므로, 필요한 배율 = a / |g|
            rb.gravityScale = requiredGravity / Mathf.Abs(Physics2D.gravity.y);

            // 회전 횟수: 층이 높을수록 많이 회전. (currentFloor - 1)번, 최소 1번.
            int rotations = Mathf.Max(1, currentFloor - 1);
            StartCoroutine(FallDeathLandingRoutine(fallDuration, rotations));
        }

        /* ---------- 씬 전환 대기 시간 = 사망 음원 길이 ----------
         * (1)왜 음원 길이만큼 기다리나: 사망음이 다 끝나기 전에 씬이 바뀌면 소리가 "뚝" 끊김.
         *   음원이 끝날 때까지 기다렸다가 씬을 전환해야 자연스러움.
         */
        float waitTime = 2.0f;
        if (selectedClip != null)
        {
            waitTime = selectedClip.length;
        }

        // 목숨이 0 이하면 게임오버 처리: 전체 데이터 리셋 + basic(게임오버/타이틀) 씬으로
        if (gameManager.currentLives <= 0)
        {
            gameManager.Instance.ResetAllData();
            StartCoroutine(LoadSceneRoutine("basic", waitTime));
        }
        else
        {
            // 목숨이 남았으면: 점수를 사망 직전으로 롤백 + 현재 씬 리로드(부활)
            gameManager.Instance.RollbackScore();
            string activeSceneName = SceneManager.GetActiveScene().name;  // 현재 씬 이름 가져오기
            StartCoroutine(LoadSceneRoutine(activeSceneName, waitTime));
        }
    }

    // FallDeathLandingRoutine() — 추락사 시 "회전하며 떨어지는" 연출 코루틴
    // (1)하는 일: ①애니메이터 자동재생 정지(speed=0) → death 애니메이션 프레임을 수동으로 돌림
    //   ②콜라이더 비활성화 → 떨어지는 동안 바닥/벽에 안 걸리고 맨 아래까지 통과
    //   ③매 프레임 progress(낙하 진행률) 계산해서, 그에 비례하게 death 애니메이션 프레임을 0~0.98까지 회전시킴.
    //     deathSpinMultiplier로 회전 속도 배율.
    //   ④바닥(firstFloorY)에 도달하거나 시간 초과하면 루프 종료 → 콜라이더 복구, 사망 자세(0프레임)로 고정.
    // (2)회전 연출 원리: death 애니메이션 클립 자체가 "빙글 도는" 프레임으로 구성돼 있고, 그걸 0~1 사이를
    //   "여러 번" 반복하게 만드는 것. cycle = (progress × 회전수 × 배율) % 1 → % 1(나머지 연산)으로 0~1을 계속 반복.
    //   (1)예: progress=0.5, rotations=2, multiplier=2 → 0.5×2×2=2 → 2%1=0 → 프레임 0. 즉 두 바퀴 돌고 다시 첫 자세로.
    // (3)왜 콜라이더를 끄나: 추락 중에 중간 층 바닥에 부딪히면 거기서 멈춰버림. 원작은 "뚫고" 맨 아래까지 떨어져야 하니까
    //   콜라이더를 잠시 끄는 것. (대신 while 조건으로 y > firstFloorY일 때만 돌고 도달하면 멈춤)
    IEnumerator FallDeathLandingRoutine(float fallDuration, int rotations)
    {
        anim.speed = 0f;  // 자동 재생 정지 → 프레임 수동 조작 모드

        if (col != null)
        {
            col.enabled = false;  // 콜라이더 비활성화 (중간 층 뚫고 내려가게)
            // (1)enabled: Component 클래스의 프로퍼티. 컴포넌트를 "임시로 끔".
            //   SetActive는 오브젝트 자체를 끄는 거고 enabled는 컴포넌트 하나만 끔. 차이 알아둘 것.
        }

        float elapsed = 0f;  // 경과 시간 누적용

        // 바닥에 도달할 때까지 매 프레임 회전 프레임 갱신
        while (transform.position.y > firstFloorY)
        {
            elapsed += Time.deltaTime;  // (1)Time.deltaTime: 지난 프레임과의 시간 간격(초). 누적하면 경과 시간이 됨.

            float progress = Mathf.Clamp01(elapsed / fallDuration);  // 낙하 진행률 0~1 (Clamp01로 0~1 제한)
            // 회전 속도 및 횟수를 deathSpinMultiplier 배율만큼 빠르게 회전하도록 수정
            float cycle = (progress * rotations * deathSpinMultiplier) % 1.0f;  // 0~1 반복 = 회전

            anim.Play("death", 0, cycle * 0.98f);  // 0.98 곱하는 이유: 클립 마지막 프레임(1.0)은 루프 끊김 현상 있을 수 있어서 직전에서 멈춤
            anim.Update(0f);

            if (elapsed >= fallDuration + 0.5f) break;  // 안전장치: 시간 초과하면 강제 종료 (무한루프 방지)

            yield return null;  // 다음 프레임까지 대기 (Update 주기로 돌림)
        }

        // 바닥 도달 후 정리
        if (col != null) col.enabled = true;  // 콜라이더 복구 (씬 리로드 직전이지만 혹시 모르니)

        rb.velocity = Vector2.zero;
        rb.gravityScale = 0f;

        // 사망 자세(0프레임)에 고정
        anim.Play("death", 0, 0f);
        anim.Update(0f);
        anim.speed = 0f;
    }

    // LoadSceneRoutine() — 딜레이 후 씬 전환하는 간단 코루틴
    // (1)하는 일: WaitForSeconds(초)만큼 기다렸다가 SceneManager.LoadScene(씬이름) 호출.
    // (2)왜 코루틴으로 하나: "기다렸다가 씬 전환"은 비동기 처리라서 코루틴이 가장 간단.
    //   Invoke("메소드명", 초)로도 가능하지만 문자열로 메소드명을 넘겨야 해서 불안정.
    // (3)SceneManager.LoadScene(): 이름으로 씬 로드. Build Settings에 등록된 씬만 로드 가능.
    //   등록 안 된 씬 이름 넣으면 에러. (basic 씬과 현재 씬이 Build Settings에 있어야 함)
    IEnumerator LoadSceneRoutine(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}
