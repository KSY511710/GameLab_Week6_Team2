using System.Collections;
using UnityEngine;
using UnityEngine.UI; // 🌟 Image 컴포넌트를 조작하기 위해 필수!
using Special.Integration;
using Special.Data;

public class GachaDispenser : MonoBehaviour
{
    [Header("UI 연결")]
    [Tooltip("투명하게 잘라주는 RectMask2D가 있는 영역")]
    public RectTransform maskArea;
    public RectTransform startPoint;  // 시작 위치 (기계 안쪽)
    public RectTransform endPoint;    // 도착 위치 (영수증 끝부분)

    [Header("프리팹 설정")]
    [Tooltip("아까 만든 하얀색 네모 '빈 껍데기' 프리팹을 여기에 넣으세요")]
    public GameObject presentationPrefab;

    [Header("애니메이션 설정")]
    public float slideSpeed = 0.5f;   // 내려오는 시간
    public float waitTime = 1.5f;     // 다 내려오고 멈춰서 보여주는 시간

    private void OnEnable()
    {
        // 🌟 "뽑기 당첨!" 방송을 구독합니다.
        SpecialGachaController.OnSpecialBlockDrawn += PlayDispenseAnimation;
    }

    private void OnDisable()
    {
        SpecialGachaController.OnSpecialBlockDrawn -= PlayDispenseAnimation;
    }

    private void PlayDispenseAnimation(SpecialBlockDefinition drawnItem)
    {
        StartCoroutine(SlideOutRoutine(drawnItem));
    }

    private IEnumerator SlideOutRoutine(SpecialBlockDefinition drawnItem)
    {
        // 🌟 1. 수엽님이 찾아내신 마법 주문! 껍데기를 마스크 안쪽(maskArea)에 소환합니다.
        GameObject dummyInstance = Instantiate(presentationPrefab, maskArea);
        RectTransform itemRect = dummyInstance.GetComponent<RectTransform>();

        // 🌟 2. 가짜 껍데기에 '진짜 당첨된 아이템의 그림'을 덮어씌웁니다.
        Image dummyImage = dummyInstance.GetComponent<Image>();
        if (dummyImage != null && drawnItem != null)
        {
            // ⚠️ 주의: drawnItem.icon 부분은 수엽님의 SpecialBlockDefinition에 
            // 이미지를 저장해둔 변수 이름(예: image, blockSprite 등)으로 바꿔주세요!
            dummyImage.sprite = drawnItem.icon;
        }

        // 3. 시작 위치로 셋팅 (기계 안쪽에 숨기기)
        itemRect.position = startPoint.position;
        itemRect.localScale = Vector3.one;

        // 4. 지이잉~ 영수증을 타고 내려오는 애니메이션
        Vector3 startPos = startPoint.position;
        Vector3 endPos = endPoint.position;
        float elapsed = 0f;

        while (elapsed < slideSpeed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideSpeed;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); // 착! 하고 부드럽게 멈추는 감속 공식

            itemRect.position = Vector3.Lerp(startPos, endPos, smoothT);
            yield return null;
        }

        // 오차 방지를 위해 정확한 도착점에 고정
        itemRect.position = endPos;

        // 5. 플레이어가 "오! 나 이거 뽑았다!" 하고 볼 수 있게 잠깐 멈춰줍니다.
        yield return new WaitForSeconds(waitTime);

        // 6. 연출이 끝났으니 가짜 껍데기는 파괴! (이미 진짜는 가방에 들어있음)
        Destroy(dummyInstance);
    }
}