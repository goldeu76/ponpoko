using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class itemData : MonoBehaviour
{
    // 아이템을 획득했을 때 기본적으로 부여할 점수 값을 저장하는 변수
    [SerializeField] private int scoreValue = 10;

    // 아이템을 먹었을 때 재생할 오디오 클립(효과음) 데이터를 담아두는 변수
    [SerializeField] AudioClip eat;

    // 아이템을 먹었을 때 실제로 올라갈 점수 수치를 설정하는 변수
    [SerializeField] int score = 100;

    // 씬 내부에 존재하는 UI 레이아웃 관리 컴포넌트를 찾아와서 담아둘 변수
    private gruopLayoutUI fruit;

    void Start()
    {
       
        fruit = FindObjectOfType<gruopLayoutUI>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (fruit != null)
            {
                fruit.AddFruit();
            }

            if (eat != null)
            {
                AudioSource.PlayClipAtPoint(eat, transform.position);
            }

            //매니저에게 내 현재 위치(transform.position)를 같이 넘김
            if (gameManager.Instance != null)
            {
                gameManager.Instance.OnEatFruit(transform.position);
            }

            gameObject.SetActive(false);
        }
    }

}