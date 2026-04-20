using Special.Integration;
using UnityEngine;
using System.Collections;

public class LeverMoveDown : MonoBehaviour
{
    [Header("애니메이션 설정")]
    public float moveDistance = 30f; // 내려갈 거리
    public float moveSpeed = 0.4f;   // 왕복 시간
    public float waitTime = 0.2f;    // 밑에서 머무는 기본 시간

    [Header("움찔(Twitch) 효과 설정")]
    public float twitchDistance = 3f;  // 연타 시 얼마나 더 눌릴지 (미세하게)
    public float twitchSpeed = 0.05f;  // 움찔거리는 속도 (매우 빠르게)

    // 레버의 현재 상태를 추적하기 위한 열거형(Enum)
    private enum LeverState { Idle, Moving, WaitingDown }
    private LeverState currentState = LeverState.Idle;

    private Vector3 startPos;
    private float currentWaitTimer = 0f; // 바닥에서 머무는 남은 시간
    private bool isTwitching = false;    // 움찔거리는 중인지 체크

    private void Start()
    {
        startPos = transform.localPosition;
    }
    private void OnEnable()
    {
        // 🌟 "버튼 눌림" 방송을 구독 (시작할 때)
        SpecialGachaController.OnButtonPressed += PullLever;
        InventoryManager.OnButtonPressed += PullLever;
    }

    private void OnDisable()
    {
        // 🌟 방송 구독 해지 (오브젝트 꺼질 때 필수!)
        SpecialGachaController.OnButtonPressed -= PullLever;
        InventoryManager.OnButtonPressed -= PullLever;
    }

    private void PullLever()
    {
        if (currentState == LeverState.Idle)
        {
            // 1. 가만히 있을 때 누르면 -> 정상적으로 내려가기 시작
            StartCoroutine(PullAnimation());
        }
        else if (currentState == LeverState.WaitingDown)
        {
            // 2. 바닥에 내려가 있을 때 연타하면 -> 대기 시간 초기화 + 움찔 효과!
            currentWaitTimer = waitTime; // 다시 처음부터 기다림 (연타하면 계속 밑에 있음)

            if (!isTwitching)
            {
                StartCoroutine(TwitchAnimation());
            }
        }
    }
    private IEnumerator PullAnimation()
    {
        currentState = LeverState.Moving;

        // 1. 내려가기
        Vector3 targetPos = startPos + new Vector3(0, -moveDistance, 0);
        yield return StartCoroutine(MoveToPosition(targetPos, moveSpeed / 2f));

        // 2. 바닥에서 대기 (상태 변경)
        currentState = LeverState.WaitingDown;
        currentWaitTimer = waitTime;

        // 🌟 타이머가 남아있거나, 아직 움찔거리는 중이면 올라가지 않고 계속 기다림
        while (currentWaitTimer > 0f || isTwitching)
        {
            // Update처럼 매 프레임 시간을 깎아냅니다.
            currentWaitTimer -= Time.deltaTime;
            yield return null;
        }

        // 3. 다시 원래 위치로 스르륵 올라오기
        currentState = LeverState.Moving;
        yield return StartCoroutine(MoveToPosition(startPos, moveSpeed / 2f));

        currentState = LeverState.Idle;
    }

    // 🌟 연타 시 아주 빠르게 덜그럭거리는 효과
    private IEnumerator TwitchAnimation()
    {
        isTwitching = true;

        Vector3 originalDownPos = startPos + new Vector3(0, -moveDistance, 0);
        Vector3 twitchPos = originalDownPos + new Vector3(0, -twitchDistance, 0); // 밑으로 살짝 더 짓누름

        // 아래로 팍!
        float elapsed = 0f;
        while (elapsed < twitchSpeed)
        {
            transform.localPosition = Vector3.Lerp(originalDownPos, twitchPos, elapsed / twitchSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 원래 바닥 위치로 튕겨 올라옴
        elapsed = 0f;
        while (elapsed < twitchSpeed)
        {
            transform.localPosition = Vector3.Lerp(twitchPos, originalDownPos, elapsed / twitchSpeed);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalDownPos; // 오차 방지
        isTwitching = false;
    }

    private IEnumerator MoveToPosition(Vector3 target, float duration)
    {
        Vector3 currentPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = t * t * (3f - 2f * t); // 부드러운 가감속

            transform.localPosition = Vector3.Lerp(currentPos, target, smoothT);
            yield return null;
        }

        transform.localPosition = target;
    }
}
