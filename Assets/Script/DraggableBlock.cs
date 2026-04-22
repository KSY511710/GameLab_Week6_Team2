using Prediction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// DraggableBlock
/// 
/// 역할:
/// 1. 인벤토리에서 블럭을 드래그해서 맵에 배치하는 기능을 담당한다.
/// 2. 드래그 중에는 프리뷰 고스트를 생성해 배치 가능/불가능 상태를 보여준다.
/// 3. R 키 입력으로 블럭 모양을 회전한다.
/// 4. 드래그 종료 시 실제 배치 가능 여부와 비용 지불 여부를 검사한다.
/// 5. 사운드 매니저와 연결하여 집기 / 회전 / 생산(레일) SFX를 재생한다.
/// 
/// 사운드 연결 규칙:
/// - 드래그 시작 시 PickUp
/// - 회전 시 RotateBlock
/// - 실제 배치 성공 시 ProduceBlock
/// 
/// 주의:
/// - 현재 설계상 ProduceBlock 은 "레일이 한 번 돌아가는 소리"로 사용한다.
/// - 따라서 배치 성공 시점에 1회만 재생한다.
/// - 배치 실패 시에는 현재 프로젝트에 전용 실패 SFX enum이 없으므로 소리를 재생하지 않는다.
/// - 돈 부족으로 설치 실패한 경우도 ProduceBlock 을 재생하지 않는다.
/// </summary>
public class DraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Block Attributes")]
    [Tooltip("이 블럭의 회사 색상 정보.")]
    public KSM_GATCHA.CompanyColor companyColor = KSM_GATCHA.CompanyColor.Red;

    [Tooltip("이 블럭의 심볼 타입 정보.")]
    public KSM_GATCHA.BlockSymbolType symbolType = KSM_GATCHA.BlockSymbolType.Symbol01;

    [Tooltip("블럭 크기. 현재 설계상 2~4 범위를 사용 중이다.")]
    [Range(2, 4)] public int blockSize = 2;

    [Header("Visual Prefabs")]
    [Tooltip("블럭의 중심 조각으로 사용할 프리팹.")]
    public GameObject centerBlockPrefab;

    [Tooltip("블럭의 나머지 조각으로 사용할 프리팹.")]
    public GameObject sideBlockPrefab;

    [Header("Shape Settings")]
    [Tooltip("현재 블럭이 차지하는 상대 좌표 배열.")]
    public Vector2Int[] shapeCoords = { new Vector2Int(0, 0) };

    /// <summary>
    /// 현재 위치에 배치 가능할 때 고스트에 적용할 색상이다.
    /// </summary>
    private Color validTint = new Color(1f, 1f, 1f, 0.5f);

    /// <summary>
    /// 현재 위치에 배치 불가능할 때 고스트에 적용할 색상이다.
    /// </summary>
    private Color invalidTint = new Color(0.2f, 0.2f, 0.2f, 0.7f);

    /// <summary>
    /// 시작 위치를 저장하는 변수다.
    /// 현재 코드에서는 직접 사용하지 않지만, 향후 복귀 로직 확장 시 활용 가능하다.
    /// </summary>
    private Vector3 startPos;

    /// <summary>
    /// 최초 블럭 형태를 저장해두는 배열이다.
    /// 배치 실패 시 원래 모양으로 되돌릴 때 사용한다.
    /// </summary>
    private Vector2Int[] originalShape;

    /// <summary>
    /// 최초 회전값을 저장한다.
    /// 배치 실패 시 원래 회전값으로 복구할 때 사용한다.
    /// </summary>
    private Quaternion originalRotation;

    /// <summary>
    /// 맵 배치/좌표 계산을 담당하는 GridManager 참조다.
    /// </summary>
    private GridManager gridManager;

    /// <summary>
    /// 마우스 스크린 좌표를 월드 좌표로 바꾸기 위한 메인 카메라 참조다.
    /// </summary>
    private Camera mainCam;

    /// <summary>
    /// 현재 UI 이미지 컴포넌트 참조다.
    /// 드래그 시작 시 원본 아이콘을 숨길 때 사용한다.
    /// </summary>
    private Image img;

    /// <summary>
    /// 현재 드래그 중인지 여부다.
    /// R 키 회전 입력 허용 여부 판단에 사용한다.
    /// </summary>
    private bool isDragging = false;
    public static bool IsAnyBlockDragging = false;
    private bool isCancelled = false;

    /// <summary>
    /// 드래그 중 생성되는 프리뷰 고스트 오브젝트다.
    /// </summary>
    private GameObject previewGhost;

    /// <summary>
    /// 고스트 내부 SpriteRenderer 배열이다.
    /// 배치 가능/불가능 색상 갱신에 사용한다.
    /// </summary>
    private SpriteRenderer[] ghostRenderers;

    /// <summary>
    /// 마지막으로 검사한 셀 위치다.
    /// 셀이 바뀌었을 때만 유효성 갱신과 브로드캐스트를 하도록 최적화하는 데 사용한다.
    /// </summary>
    private Vector3Int lastCellPos;

    [Header("Block Type Settings")]
    [Tooltip("이 블럭을 배치할 때의 비용값. 현재 실제 결제는 ResourceManager 쪽 정책을 따르므로 참고용이다.")]
    public int placementCost = 10;

    /// <summary>
    /// 시작 시 필요한 참조를 캐싱하고,
    /// 현재 블럭의 원본 형태/회전값을 저장한다.
    /// </summary>
    private void Start()
    {
        gridManager = Object.FindAnyObjectByType<GridManager>();
        mainCam = Camera.main;
        img = GetComponent<Image>();

        if (shapeCoords != null)
        {
            originalShape = (Vector2Int[])shapeCoords.Clone();
            originalRotation = transform.rotation;
        }
    }

    /// <summary>
    /// 블럭 데이터 초기화 함수다.
    /// 
    /// 역할:
    /// 1. 색상/심볼/크기 정보를 저장한다.
    /// 2. 회사 색상에 따라 UI 이미지 색을 바꾼다.
    /// 3. 크기에 따라 기본 shapeCoords를 다시 세팅한다.
    /// 4. 원본 형태와 원본 회전값을 갱신한다.
    /// </summary>
    /// <param name="c">회사 색상</param>
    /// <param name="s">블럭 심볼 타입</param>
    /// <param name="size">블럭 크기</param>
    public void InitializeBlock(KSM_GATCHA.CompanyColor c, KSM_GATCHA.BlockSymbolType s, int size)
    {
        companyColor = c;
        symbolType = s;
        blockSize = size;

        img = GetComponent<Image>();

        if (c == KSM_GATCHA.CompanyColor.Red)
        {
            img.color = new Color(1f, 0.2f, 0.2f);
        }
        else if (c == KSM_GATCHA.CompanyColor.Blue)
        {
            img.color = new Color(0.2f, 0.4f, 1f);
        }
        else if (c == KSM_GATCHA.CompanyColor.Yellow)
        {
            img.color = new Color(0.2f, 1f, 0.2f);
        }

        if (size == 1)
        {
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0) };
        }
        else if (size == 2)
        {
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        }
        else if (size == 3)
        {
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };
        }

        originalShape = (Vector2Int[])shapeCoords.Clone();
        originalRotation = transform.rotation;
    }

    /// <summary>
    /// 매 프레임 입력을 확인한다.
    /// 
    /// 역할:
    /// - 드래그 중일 때만 R 키로 회전 입력을 받는다.
    /// </summary>
    private void Update()
    {
        if (isDragging)
        {
            // 기존 R/Q 회전 로직
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            {
                RotateShape();
            }

            // 🌟 2. 마우스 우클릭 감지 시 강제 취소!
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelDrag();
            }
        }
    }

    /// <summary>
    /// 현재 블럭의 shape와 시각적 회전을 90도 회전시킨다.
    /// 
    /// 역할:
    /// 1. 논리 좌표(shapeCoords)를 회전한다.
    /// 2. 인벤토리 아이콘을 회전한다.
    /// 3. 프리뷰 고스트가 있으면 같이 회전시킨다.
    /// 4. 회전 시 SFX를 재생한다.
    /// 
    /// 주의:
    /// - 현재는 드래그 중에만 호출되도록 Update에서 제한하고 있다.
    /// </summary>
    private void RotateShape()
    {
        for (int i = 0; i < shapeCoords.Length; i++)
        {
            int x = shapeCoords[i].x;
            int y = shapeCoords[i].y;
            shapeCoords[i] = new Vector2Int(y, -x);
        }

        transform.Rotate(0f, 0f, -90f);

        if (previewGhost != null)
        {
            previewGhost.transform.Rotate(0f, 0f, -90f);

            Transform center = previewGhost.transform.Find("CenterPiece");
            if (center != null)
            {
                center.rotation = Quaternion.identity;
            }
        }

        PlayRotateSfx();
        UpdateGhostValidity(lastCellPos);
        PlacementInteractionHub.BroadcastDragMoved(lastCellPos, shapeCoords, null, (int)companyColor, (int)symbolType);
    }
    private void CancelDrag()
    {
        isCancelled = true;         // 취소 모드 ON!
        isDragging = false;
        IsAnyBlockDragging = false; // 인벤토리 매니저에게도 드래그 끝났다고 알림

        PlacementInteractionHub.BroadcastDragEnded();

        // 고스트(프리뷰) 즉시 파괴
        if (previewGhost != null)
        {
            Destroy(previewGhost);
            previewGhost = null;
            ghostRenderers = null;
        }

        // 블록 모양과 회전 원상 복구
        ResetToOriginalState();

        // 인벤토리에서 숨겨놨던 원본 아이콘 다시 켜기
        if (img != null)
        {
            img.enabled = true;
        }

        Debug.Log("우클릭으로 블록 배치를 취소했습니다.");
    }

    /// <summary>
    /// 드래그 시작 시 호출된다.
    /// 
    /// 역할:
    /// 1. 드래그 상태를 true로 전환한다.
    /// 2. 원본 UI 이미지를 숨긴다.
    /// 3. 프리뷰 고스트를 생성한다.
    /// 4. 집기 SFX를 재생한다.
    /// 5. 즉시 OnDrag를 1회 호출해 첫 프레임부터 고스트 위치를 맞춘다.
    /// </summary>
    /// <param name="eventData">드래그 이벤트 데이터</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        isCancelled = false;
        isDragging = true;
        IsAnyBlockDragging = true;
        if (img != null)
        {
            img.enabled = false;
        }

        previewGhost = gridManager.CreateModularPreview(shapeCoords, centerBlockPrefab, sideBlockPrefab, invalidTint);
        ghostRenderers = previewGhost.GetComponentsInChildren<SpriteRenderer>();

        PlayPickUpSfx();

        OnDrag(eventData);
    }

    /// <summary>
    /// 드래그 중 호출된다.
    /// 
    /// 역할:
    /// 1. 마우스 위치를 월드 좌표로 변환한다.
    /// 2. 해당 좌표를 셀 좌표로 변환한다.
    /// 3. 프리뷰 고스트를 스냅 위치로 이동시킨다.
    /// 4. 셀 위치가 바뀐 경우에만 배치 가능 여부와 인터랙션 허브를 갱신한다.
    /// </summary>
    /// <param name="eventData">드래그 이벤트 데이터</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (isCancelled) return;
        if (previewGhost == null)
        {
            return;
        }

        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        Vector3Int cellPos = gridManager.GetCellPositionFromMouse(mouseWorldPos);
        Vector3 snapPos = gridManager.groundTilemap.GetCellCenterWorld(cellPos);
        previewGhost.transform.position = snapPos;

        if (cellPos != lastCellPos)
        {
            lastCellPos = cellPos;
            UpdateGhostValidity(cellPos);
            PlacementInteractionHub.BroadcastDragMoved(cellPos, shapeCoords, null, (int)companyColor, (int)symbolType);
        }
    }

    /// <summary>
    /// 현재 셀 위치 기준으로 배치 가능 여부를 판단하고,
    /// 프리뷰 고스트의 색상을 갱신한다.
    /// </summary>
    /// <param name="cellPos">검사할 셀 좌표</param>
    private void UpdateGhostValidity(Vector3Int cellPos)
    {
        if (previewGhost == null || ghostRenderers == null)
        {
            return;
        }

        bool canPlace = gridManager.CanPlaceShape(cellPos, shapeCoords);
        Color targetTint = canPlace ? validTint : invalidTint;

        foreach (SpriteRenderer sr in ghostRenderers)
        {
            if (sr != null)
            {
                sr.color = targetTint;
            }
        }
    }

    /// <summary>
    /// 드래그 종료 시 호출된다.
    /// 
    /// 역할:
    /// 1. 드래그 상태를 종료한다.
    /// 2. 고스트를 제거한다.
    /// 3. 현재 셀 위치에서 실제 배치 가능 여부를 다시 검사한다.
    /// 4. 배치 가능하면 비용을 지불하고 블럭을 설치한다.
    /// 5. 설치 성공 시 ProduceBlock SFX를 재생한다.
    /// 6. 이후 인벤토리 슬롯을 당기고 새 블럭을 채운다.
    /// 7. 배치 실패 또는 비용 부족이면 원래 상태로 복구한다.
    /// </summary>
    /// <param name="eventData">드래그 이벤트 데이터</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (isCancelled)
        {
            isCancelled = false; // 다음 드래그를 위해 리셋
            return;
        }
        isDragging = false;
        IsAnyBlockDragging = false;
        PlacementInteractionHub.BroadcastDragEnded();

        if (previewGhost != null)
        {
            Destroy(previewGhost);
            previewGhost = null;
            ghostRenderers = null;
        }

        Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0f;

        Vector3Int cellPos = gridManager.GetCellPositionFromMouse(worldPos);

        if (gridManager.CanPlaceShape(cellPos, shapeCoords))
        {
            if (!ResourceManager.Instance.TryPayForBasicDraw())
            {
                Debug.Log("돈이 부족해서 블록을 설치할 수 없습니다!");
                ResetToOriginalState();

                if (img != null)
                {
                    img.enabled = true;
                }

                return;
            }

            gridManager.PlaceShape(
                cellPos,
                shapeCoords,
                (int)companyColor,
                (int)symbolType,
                centerBlockPrefab,
                sideBlockPrefab
            );

            PlayProduceBlockSfx();

            if (InventoryManager.Instance != null)
            {
                transform.SetParent(null);
                InventoryManager.Instance.ProcessGravityAndRefill();
            }

            Destroy(gameObject);
        }
        else
        {
            ResetToOriginalState();

            if (img != null)
            {
                img.enabled = true;
            }
        }
    }

    /// <summary>
    /// 블럭의 논리 shape와 회전값을 원래 상태로 복구한다.
    /// 
    /// 역할:
    /// - 배치 실패 시 다음 드래그를 위해 기준 상태를 보존한다.
    /// </summary>
    private void ResetToOriginalState()
    {
        shapeCoords = (Vector2Int[])originalShape.Clone();
        transform.rotation = originalRotation;
    }

    /// <summary>
    /// 블럭 집기 SFX를 안전하게 재생한다.
    /// 
    /// 역할:
    /// - 사운드 매니저가 없을 때도 오류 없이 넘어가게 한다.
    /// </summary>
    private void PlayPickUpSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlayPickUp();
        }
    }

    /// <summary>
    /// 레일 회전음(ProduceBlock)을 안전하게 재생한다.
    /// 
    /// 역할:
    /// - 실제 배치 성공 직후에만 호출한다.
    /// - 블럭을 놓는 순간 생산 라인이 한 번 도는 느낌의 사운드로 사용한다.
    /// </summary>
    private void PlayProduceBlockSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlaySfx(KSM_SfxType.ProduceBlock);
        }
    }

    /// <summary>
    /// 블럭 회전 SFX를 안전하게 재생한다.
    /// 
    /// 역할:
    /// - R 키 회전이 성공적으로 처리된 직후에 호출한다.
    /// </summary>
    private void PlayRotateSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlayRotateBlock();
        }
    }
}