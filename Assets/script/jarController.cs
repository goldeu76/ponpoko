using UnityEngine; // 유니티 엔진의 기본 클래스와 물리, 오디오, 랜덤 함수 등을 쓰기 위한 네임스페이스임

// 항아리 오브젝트에 부착되어 플레이어와 닿았을 때 확률적으로 점수를 주거나 몬스터(뱀)를 소환하는 스크립트임
public class JarController : MonoBehaviour
{
    // Header와 Range 어트리뷰트(Attribute) 개념과 원리
    // [Header]는 유니티 인스펙터 창에 굵은 글씨로 제목 줄을 만들어 변수들을 카테고리별로 보기 좋게 묶어주는 기능임
    // [Range(최소, 최대)]는 숫자 입력 칸을 슬라이더(막대) UI로 바꿔서 0 미만이거나 100을 초과하는 잘못된 값이 들어가는 휴먼 에러를 원천 차단함
    [Header("확률 설정 (합이 100이 되도록 설정)")]
    [Range(0, 100)][SerializeField] int scoreChance = 50;
    [Range(0, 100)][SerializeField] int monsterChance = 50;

    // Prefab(프리팹) 개념과 원리
    // 씬(화면)에 아직 존재하지 않더라도, 미리 만들어 파일 형태로 저장해 둔 게임 오브젝트의 '설계도(원본 틀)'를 담아두는 변수임
    // 게임 도중 필요한 순간에 이 설계도를 복제(Instantiate)해서 화면에 실시간으로 찍어낼 때 사용함
    [Header("몬스터 설정")]
    [SerializeField] GameObject monsterPrefab;

    [Header("효과음 설정")]
    [SerializeField] AudioClip Trapaudioclip; // 함정(몬스터 등장)일 때 재생할 음원 데이터
    [SerializeField] AudioClip Itemaudioclip; // 점수 획득일 때 재생할 음원 데이터

    // [삭제] 오디오 소스를 가지고 있던 항아리가 바로 삭제되므로 오디오 컴포넌트 캐싱은 지웁니다.
    // 원리 보충 설명: 오브젝트가 Destroy로 파괴되면 그 안에 붙어 있던 AudioSource 컴포넌트도 즉시 메모리에서 같이 증발해 버림
    // 그래서 소리가 시작되자마자 0.01초 만에 뚝 끊기는 현상이 발생하므로, 항아리 자체의 AudioSource는 쓰지 않는 것이 맞음
    // private AudioSource source; 

    // 중복 실행 방지 플래그(Flag) 개념과 원리
    // 유니티의 Destroy() 함수는 호출 즉시 오브젝트를 지우는 게 아니라 '이번 프레임이 완전히 끝날 때' 지우도록 예약만 걸어둠
    // 만약 플레이어의 콜라이더가 여러 개거나 한 프레임에 충돌이 두 번 감지되면, 항아리가 미처 사라지기 전에 보상을 두 번 주거나 뱀이 두 마리 나오는 심각한 버그가 생기는데 이를 막아주는 안전장치임
    private bool isOpened = false;

    // Trigger 충돌 감지 원리
    // 항아리의 Collider2D에 'Is Trigger'가 체크되어 있어서, 플레이어가 항아리에 부딪혀 막히지 않고 자연스럽게 겹치는 순간 엔진이 이 함수를 자동 호출함
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // CompareTag 개념과 메모리 최적화 원리
        // collision.tag == "Player" 처럼 문자열을 직접 비교하면 비교할 때마다 새로운 문자열 메모리가 생성되어 가비지 컬렉터(GC)에 부담을 줌
        // 반면 CompareTag("Player")는 유니티 내부의 해시값을 이용해 메모리 할당 없이 초고속으로 태그를 비교함
        // 이미 열린 항아리(isOpened)이거나 부딪힌 대상이 플레이어가 아니면 아무것도 하지 않고 즉시 함수를 종료(Early Return)함
        if (isOpened || !collision.CompareTag("Player")) return;

        // 일단 한 번 작동하기 시작했으면 즉시 스위치를 켜서 두 번 다시 이 아래 코드가 실행되지 못하게 잠금
        isOpened = true;

        // Random.Range(int min, int max)
        // 정수(int)를 넣는 Random.Range(0, 100)은 최소값(0)은 포함하고 최대값(100)은 제외한 '0부터 99까지' 총 100개의 정수 중 하나를 무작위로 뽑아줌
        // 뽑힌 숫자가 scoreChance(예: 50)보다 작을 확률은 0~49까지 정확히 50개이므로 깔끔하게 50% 확률 분기가 만들어짐
        int randomValue = Random.Range(0, 100);

        if (randomValue < scoreChance)
        {
            // [원작 변경] 랜덤 점수 대신, 게임 매니저에게 너구리 공식 항아리 점수를 요청합니다.
            // Null 체크 방어 코드: 혹시라도 씬에 gameManager가 없을 때 게임이 멈추는(NullReferenceException) 현상을 예방함
            if (gameManager.Instance != null)
            {
                int ponpokoJarScore = gameManager.Instance.GetJarScore();
                GiveScore(ponpokoJarScore);
            }
        }
        else
        {
            // 뽑힌 난수가 scoreChance 이상이면(나머지 확률 구간) 몬스터 소환 함수를 실행함
            SpawnMonster();
        }

        // Destroy 개념과 메모리 해제 원리
        // 소문자 gameObject는 '이 스크립트가 붙어 있는 자기 자신(항아리 오브젝트)'을 가리키는 예약어임
        // 보상이나 함정 처리가 끝났으니 현재 프레임의 마지막 단계에 메모리와 씬에서 항아리 오브젝트를 완전히 지워버림
        Destroy(gameObject);
    }

    void GiveScore(int score)
    {
        // 문자열 보간($) 개념
        // 문자열 앞에 $ 기호를 붙이면 더하기(+) 기호로 문자열을 복잡하게 이어 붙이지 않고도 중괄호 {변수명} 안에 변수 값을 깔끔하게 넣어서 출력할 수 있음
        Debug.Log($"항아리 획득! 너구리 원작 규칙 (현재 과일 점수의 2배): {score} 점 획득!");

        // [사운드 버그 수정] 항아리가 Destroy 되어도 공중에서 소리가 끝까지 날 수 있게 PlayClipAtPoint를 사용합니다.
        // AudioSource.PlayClipAtPoint 개념과 내부 동작 원리
        // 이 함수를 호출하면 유니티가 지정된 좌표(transform.position)에 보이지 않는 '임시 빈 오브젝트 + AudioSource'를 현장에서 즉석 생성해 소리를 재생함
        // 그리고 음원 재생이 끝나는 순간 그 임시 오브젝트까지 알아서 자동 삭제(Destroy)해 주기 때문에, 항아리가 당장 사라져도 소리가 끊기지 않고 끝까지 깔끔하게 울려 퍼짐
        if (Itemaudioclip != null)
        {
            AudioSource.PlayClipAtPoint(Itemaudioclip, transform.position);
        }

        // 전역으로 관리되는 싱글톤 매니저를 불러와서 점수 데이터를 실제로 올려줌
        gameManager.Instance.AddScore(score);
    }

    void SpawnMonster()
    {
        // 유니티 에디터 인스펙터 창에서 깜빡하고 몬스터 프리팹을 안 넣었을 때 에러로 게임이 뻗는 대신 노란색 경고창만 띄우고 함수를 안전하게 빠져나가는 방어 코드임
        if (monsterPrefab == null)
        {
            Debug.LogWarning("항아리 몬스터 프리팹이 지정되지 않았습니다!");
            return;
        }

        // Instantiate(원본 프리팹, 생성 위치, 회전값) 개념과 Quaternion.identity 원리
        // 프로젝트 폴더에 있던 프리팹 설계도를 복제해서 실제 게임 세상(현재 항아리가 있는 위치 transform.position)에 실체화(인스턴스화)하는 함수임
        // 유니티 내부에서는 3차원 회전이 꼬이는 '짐벌락(Gimbal Lock)' 현상을 막기 위해 각도를 사원수(Quaternion)라는 수학 체계로 계산하며, Quaternion.identity는 '기울어짐 없이 회전이 전혀 없는 원래 방향(0, 0, 0)'을 의미함
        Instantiate(monsterPrefab, transform.position, Quaternion.identity);

        // [사운드 버그 수정] 뱀이 소환될 때도 항아리가 바로 사라지므로 안전하게 좌표 기반 재생을 씀.
        if (Trapaudioclip != null)
        {
            AudioSource.PlayClipAtPoint(Trapaudioclip, transform.position);
        }
    }
}