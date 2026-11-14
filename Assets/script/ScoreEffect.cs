using UnityEngine; // 유니티 기본 컴포넌트(MonoBehaviour, SpriteRenderer 등)와 파괴 함수를 쓰기 위한 네임스페이스임

// 과일이나 항아리를 획득했을 때 화면에 점수 숫자(예: 100, 200, 500점 등) 이미지를 잠깐 띄웠다가 사라지게 하는 이펙트 전용 스크립트임
public class ScoreEffect : MonoBehaviour
{
    // 화면에 점수 이미지를 실제로 그려줄 렌더러 컴포넌트를 담아둘 변수임
    private SpriteRenderer spriteRenderer;

    // Awake 생명주기 개념과 Start 대신 Awake를 쓴 결정적 원리
    // Awake는 Start보다도 더 먼저, 오브젝트가 생성(Instantiate)되는 바로 그 즉시 실행되는 최우선 초기화 함수임
    // 만약 이 캐싱 코드를 Start에 넣으면 심각한 버그가 발생함:
    // 외부(gameManager)에서 Instantiate로 이펙트를 생성하자마자 같은 줄에서 바로 Setup() 함수를 호출할 때, Start는 아직 실행되기 전이라 spriteRenderer가 비어 있는(null) 상태로 판정되어 이미지가 안 바뀌기 때문임
    // 따라서 생성 직후 외부 스크립트와 즉시 통신해야 하는 프리팹은 반드시 Awake에서 컴포넌트를 먼저 찾아둬야 안전함
    private void Awake()
    {
        // 자기 자신에게 붙어 있는 SpriteRenderer 컴포넌트의 메모리 주소를 미리 찾아 변수에 캐싱해 둠
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // XML 문서 주석(/// <summary>) 개념과 원리
    // 일반 주석(//)과 달리 슬래시 3개(///)로 태그를 달아두면, 비주얼 스튜디오(IDE)가 이를 공식 설명서 데이터로 인식함
    // 나중에 다른 스크립트(gameManager 등)에서 이 Setup 함수 위에 마우스 커서를 올렸을 때, 요약 설명이 툴팁(말풍선)으로 자동 표시되어 협업이나 유지보수할 때 코드를 일일이 열어보지 않아도 되게 해줌
    /// <summary>
    /// 외부(gameManager)에서 획득한 점수 스프라이트를 넘겨받아 세팅하는 함수
    /// </summary>
    public void Setup(Sprite scoreSprite)
    {
        //오브젝트 풀링

        // Null 체크 방어 코드: 렌더러 컴포넌트가 정상적으로 있고, 외부에서 넘겨준 이미지 데이터도 비어 있지 않을 때만 안전하게 이미지를 교체함
        if (spriteRenderer != null && scoreSprite != null)
        {
            spriteRenderer.sprite = scoreSprite;
        }

        // Destroy 함수 오버로딩(Overloading)과 지연 파괴 원리
        // Destroy(대상의 gameObject, 지연 시간) 형태로 두 번째 인자에 초 단위 시간(0.5f)을 넣어주면, 코루틴이나 타이머 변수를 따로 안 만들어도 유니티 엔진 내부 타이머가 알아서 0.5초를 센 뒤 오브젝트를 자동 삭제해 줌
        // 이펙트 오브젝트를 안 지우고 방치하면 화면에는 안 보여도 하이어라키(메모리)에 수백 개씩 계속 쌓여서 게임이 느려지는 메모리 누수(Memory Leak)가 발생하므로, 연출 시간(0.5초)이 끝나면 반드시 스스로 소멸하게 만들어야 함
        // 0.5초 뒤에 자동으로 이펙트 오브젝트를 파괴 (애니메이션 재생 시간에 맞춰 조절)
        Destroy(gameObject, 0.5f);
    }
}