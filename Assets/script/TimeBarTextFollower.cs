using UnityEngine;
using UnityEngine.UI;

public class TimeBarText : MonoBehaviour
{
    // 다른 스크립트의 데이터를 가져오기 위해 연결하는 참조 변수임
    // 이렇게 객체 지향적으로 데이터와 뷰의 책임을 분리해두면 나중에 코드가 커져도 유지보수하기 훨씬 편함
    [SerializeField] timeBar targetTimeBar;

    private Text myText;

    // RectTransform 개념
    // 유니티 캔버스 위에서 UI 요소의 위치나 크기 피벗 등을 픽셀 단위로 정밀하게 제어할 때 무조건 써야 하는 UI 전용 트랜스폼 컴포넌트
    private RectTransform textRect;
    private RectTransform barRect;
    private Image timebarImage;

    // SerializeField 속성
    [SerializeField] bool reverseDirection = false;

    // 해상도가 달라지거나 폰트 픽셀 크기가 애매할 때 화면에서 텍스트 위치를 미세하게 보정하려고 열어둔 여유 변수임
    [SerializeField] float xOffset = 0f;

    void Start()
    {
        // 캐싱 처리
        // Update처럼 매 프레임마다 도는 곳에서 컴포넌트를 찾으면 연산이 낭비되니까 스크립트가 켜질 때 한 번만 메모리 주소를 찾아두고 재사용하는 최적화
        myText = GetComponent<Text>();
        textRect = GetComponent<RectTransform>();

        if (targetTimeBar != null)
        {
            timebarImage = targetTimeBar.GetComponent<Image>();
            barRect = targetTimeBar.GetComponent<RectTransform>();
        }
        else
        {
            // 방어적 에러 핸들링
            // 에디터에서 실수로 스크립트 연결을 까먹었을 때 널 레퍼런스 에러로 게임이 뻗는 걸 막고 디버그 창에 원인을 띄워주는 아주 중요한 안전 장치임
            Debug.LogWarning("TimeBarText targetTimeBar가 연결되지 않았습니다!");
        }
    }

    void Update()
    {
        // 널 체크
        // UI 컴포넌트들이 정상적으로 메모리에 다 올라와 있을 때만 아래 좌표 계산 로직이 돌게 묶어둠
        if (timebarImage != null && textRect != null && barRect != null)
        {
            // fillAmount 개념
            // 0부터 1 사이의 소수점 값으로 현재 이미지 게이지가 얼마나 채워져 있는지 비율을 나타내는 핵심 UI 데이터임
            float fillRatio = timebarImage.fillAmount;

            // UI 게이지가 줄어드는 방향이 반대일 경우 1에서 현재 비율을 빼서 위치 계산을 정반대로 뒤집어주는 수학적 꼼수임
            if (reverseDirection)
            {
                fillRatio = 1f - fillRatio;
            }

            float barWidth = barRect.rect.width;
            float currentX = 0f;

            // 피벗 Pivot 개념
            // UI 이미지의 중심점이 어디에 찍혀있는지 나타내는 값임
            // 중심이 정가운데 0.5 인지 왼쪽 끝 0 인지 오른쪽 끝 1 인지에 따라 좌표의 시작 영점이 완전히 달라지기 때문에 분기문으로 나눠서 텍스트가 바의 끝부분을 정확히 따라가게 동적으로 계산하는 렌더링 로직임
            if (barRect.pivot.x == 0.5f)
            {
                currentX = (-barWidth / 2f) + (barWidth * fillRatio) + xOffset;
            }
            else if (barRect.pivot.x == 0f)
            {
                currentX = (barWidth * fillRatio) + xOffset;
            }
            else
            {
                currentX = (-barWidth) + (barWidth * fillRatio) + xOffset;
            }

            // anchoredPosition 개념
            // 캔버스 상에서 부모의 앵커 즉 닻을 기준으로 삼는 UI 전용 상대 좌표임
            // 일반 위치 값을 쓰면 해상도가 바뀔 때 텍스트가 화면 밖으로 날아가니까 무조건 이걸로 위치를 잡아줘야 UI 반응형 대응이 가능해짐
            Vector2 pos = textRect.anchoredPosition;
            pos.x = currentX;
            textRect.anchoredPosition = pos;
        }

        if (targetTimeBar != null && myText != null)
        {
            // 형변환 캐스팅 개념
            // 타임바 스크립트에서 실시간으로 흘러가는 소수점 시간을 정수형 int 로 강제 변환해서 소수점을 날리고 깔끔한 초 단위 정수만 남기는 거임
            int displayTime = (int)targetTimeBar.tt;

            // 특정 예외 처리 연출
            // 화면에 보여줄 시간이 7이거나 0일 때는 텍스트를 아예 빈 문자열로 덮어씌워서 화면에서 숨겨버리는 조건문임
            if (displayTime == 7 || displayTime == 0)
            {
                myText.text = "";
            }
            else
            {
                // 숫자를 화면에 띄우기 위해 문자열 변환 함수를 써서 텍스트 UI에 꽂아 넣음
                myText.text = displayTime.ToString();
            }
        }
    }
}