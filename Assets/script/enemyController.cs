using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class enemyController : MonoBehaviour
{
    // 몬스터의 이동 속도를 결정하는 변수이며 인스펙터 창에서 값을 직접 조절할 수 있음
    public float speed = 3.0f;

    // 몬스터가 현재 어떤 방향으로 이동하고 있는지 상태를 저장하는 문자열 변수
    public string direction = "left";

    // 몬스터가 제자리에서 왕복 운동을 할 때 최대 이동 거리를 지정하는 변수
    public float range = 0.0f;

    // 몬스터가 게임 세상에 처음 생성되었을 때의 원래 위치 좌표를 기억해두는 벡터 변수
    Vector3 defPos;

    void Start()
    {
        // 만약 처음 시작 방향이 오른쪽으로 설정되어 있다면 스프라이트의 좌우 스케일을 반전시켜 오른쪽을 보게 만듦
        if (direction.Equals("right"))
        {
            transform.localScale = new Vector2(-1, 1);
        }

        // 몬스터가 맨 처음 배치된 월드 좌표를 defPos에 저장하여 나중에 왕복 범위 계산의 기준점으로 삼음
        defPos = transform.position;
    }

    void Update()
    {
        // 왕복 범위 값이 0보다 크게 설정되어 있다면 지정된 거리 안에서만 움직이도록 체크함
        if (range > 0.0f)
        {
            // 현재 몬스터의 x좌표가 기준점에서 왼쪽으로 허용된 범위를 넘어섰는지 검사함
            if (transform.position.x < defPos.x - (range / 2))
            {
                // 이동 방향을 오른쪽으로 변경하고 스프라이트도 오른쪽을 보도록 뒤집음
                direction = "right";
                transform.localScale = new Vector2(-1, 1);
            }

            // 현재 몬스터의 x좌표가 기준점에서 오른쪽으로 허용된 범위를 넘어섰는지 검사함
            if (transform.position.x > defPos.x + (range / 2))
            {
                // 이동 방향을 왼쪽으로 변경하고 스프라이트도 원래 상태로 되돌림
                direction = "left";
                transform.localScale = new Vector2(1, 1);
            }
        }
    }

    private void FixedUpdate()
    {
        // 몬스터 오브젝트에 부착된 리지드바디 컴포넌트를 가져와서 물리 연산 제어 변수에 담음
        Rigidbody2D rbody = GetComponent<Rigidbody2D>();

        // 현재 저장된 방향 값에 따라 수평 방향으로 일정한 속도를 부여하여 실제로 움직이게 만듦
        if (direction.Equals("right"))
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
            // 벽이나 장애물 등의 트리거 콜라이더 영역에 부딪히는 순간 이동 방향을 정반대로 뒤집어줌
            if (direction.Equals("right"))
            {
                direction = "left";
                transform.localScale = new Vector2(1, 1);
            }
            else
            {
                direction = "right";
                transform.localScale = new Vector2(-1, 1);
            }
        }
       
    }
}