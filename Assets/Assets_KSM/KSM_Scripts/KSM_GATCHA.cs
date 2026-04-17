using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSM_GATCHA
/// 로그 확인용 단독 가챠 테스트 스크립트.
/// 
/// 목적:
/// 1. 기본 뽑기 로그 출력
/// 2. 색상별 뽑기 로그 출력
/// 3. 발전 종류별 뽑기 로그 출력
/// 4. 발전 타입 뽑기 로그 출력
/// 5. 블록 크기(1칸 / 2칸 / 3칸)까지 함께 관리
/// 
/// 현재 단계에서는 실제 블록 지급, 자원 차감, 인벤토리 연동 없이
/// "어떤 블록이 뽑혔는지"만 콘솔 로그로 확인하기 위한 테스트용 구조다.
/// 
/// 나중에 확장할 때:
/// - ResourceManager 연결
/// - DraggableBlock.AddBlock() 연결
/// - 비용 차감
/// - 결과 UI 표시
/// 등을 추가하면 된다.
/// </summary>
public class KSM_GATCHA : MonoBehaviour
{
    /// <summary>
    /// 블록의 색상 테마 분류.
    /// </summary>
    public enum BlockColorTheme
    {
        None,
        Red,
        Blue,
        Yellow
    }

    /// <summary>
    /// 블록의 발전 종류 분류.
    /// </summary>
    public enum BlockGenerationTheme
    {
        None,
        Thermal,
        Hydro,
        Solar
    }

    /// <summary>
    /// 발전 타입(특수 타입) 분류.
    /// 현재는 임시로 구름 생성 / 구름 제거만 사용한다.
    /// </summary>
    public enum BlockDevelopmentType
    {
        None,
        CloudCreate,
        CloudRemove
    }

    /// <summary>
    /// 블록이 어느 풀에 속하는지 구분하는 용도.
    /// Regular     : 일반 발전 블록
    /// Development : 발전 타입(특수 타입) 블록
    /// </summary>
    public enum BlockPoolType
    {
        Regular,
        Development
    }

    /// <summary>
    /// 가챠에 등록되는 블록 데이터 1개 단위.
    /// 나중에 확장 시 여기 필드를 늘리면 된다.
    /// 예:
    /// - 희귀도
    /// - 아이콘
    /// - 비용 배수
    /// - 실제 인벤토리 참조
    /// </summary>
    [Serializable]
    public class GatchaBlockEntry
    {
        [Header("기본 정보")]
        [Tooltip("콘솔에 출력할 블록 이름")]
        public string blockName;

        [Tooltip("현재 엔트리를 사용할지 여부")]
        public bool isEnabled = true;

        [Header("분류 정보")]
        [Tooltip("일반 블록인지, 특수 발전 타입 블록인지 구분")]
        public BlockPoolType poolType = BlockPoolType.Regular;

        [Tooltip("색상 테마")]
        public BlockColorTheme colorTheme = BlockColorTheme.None;

        [Tooltip("발전 종류 테마")]
        public BlockGenerationTheme generationTheme = BlockGenerationTheme.None;

        [Tooltip("특수 발전 타입")]
        public BlockDevelopmentType developmentType = BlockDevelopmentType.None;

        [Header("블록 크기 정보")]
        [Range(1, 3)]
        [Tooltip("블록 크기. 1, 2, 3 중 하나만 사용한다.")]
        public int blockSize = 1;

        [Header("뽑기 설정")]
        [Tooltip("기본 뽑기 대상에 포함할지 여부")]
        public bool includeInBasicDraw = true;

        [Min(1)]
        [Tooltip("가중치 값. 높을수록 더 자주 뽑힌다.")]
        public int weight = 1;
    }

    [Header("가챠 데이터 목록")]
    [Tooltip("비워두면 Awake에서 기본 데이터를 자동 생성한다.")]
    [SerializeField]
    private List<GatchaBlockEntry> blockEntries = new List<GatchaBlockEntry>();

    [Header("로그 옵션")]
    [Tooltip("플레이 시작 시 현재 등록된 블록 목록을 로그로 출력할지 여부")]
    [SerializeField]
    private bool printAllEntriesOnStart = true;

    [Tooltip("뽑기 결과를 자세하게 로그로 출력할지 여부")]
    [SerializeField]
    private bool printVerboseLog = true;

    [Header("현재 선택된 크기 필터")]
    [Tooltip("true면 아래 requestedBlockSize를 현재 뽑기에 공통 적용한다.")]
    [SerializeField]
    private bool useRequestedBlockSizeFilter = false;

    [Range(1, 3)]
    [Tooltip("현재 선택된 블록 크기. 1, 2, 3 중 하나.")]
    [SerializeField]
    private int requestedBlockSize = 1;

    /// <summary>
    /// Awake
    /// 씬 시작 시 blockEntries가 비어 있다면
    /// 바로 테스트할 수 있도록 기본 블록 데이터를 자동 생성한다.
    /// </summary>
    private void Awake()
    {
        if (blockEntries == null)
        {
            blockEntries = new List<GatchaBlockEntry>();
        }

        requestedBlockSize = NormalizeBlockSize(requestedBlockSize);

        if (blockEntries.Count == 0)
        {
            CreateDefaultEntries();
        }
    }

    /// <summary>
    /// Start
    /// 현재 등록된 블록 목록을 콘솔에 출력한다.
    /// 이 로그로 데이터가 제대로 들어갔는지 확인할 수 있다.
    /// </summary>
    private void Start()
    {
        if (printAllEntriesOnStart)
        {
            Debug.Log($"[KSM_GATCHA] 등록된 블록 수 : {blockEntries.Count}");
            PrintAllEntries();
        }
    }

    /// <summary>
    /// 인스펙터에서 값이 바뀔 때 범위를 보정한다.
    /// </summary>
    private void OnValidate()
    {
        requestedBlockSize = NormalizeBlockSize(requestedBlockSize);

        if (blockEntries == null)
        {
            return;
        }

        for (int i = 0; i < blockEntries.Count; i++)
        {
            GatchaBlockEntry entry = blockEntries[i];

            if (entry == null)
            {
                continue;
            }

            entry.blockSize = NormalizeBlockSize(entry.blockSize);

            if (entry.weight < 1)
            {
                entry.weight = 1;
            }
        }
    }

    /// <summary>
    /// 외부 UI 버튼이나 스크립트에서
    /// "현재 선택된 블록 크기"를 1칸으로 설정할 때 사용한다.
    /// </summary>
    public void SetBlockSize1()
    {
        SetRequestedBlockSize(1);
    }

    /// <summary>
    /// 외부 UI 버튼이나 스크립트에서
    /// "현재 선택된 블록 크기"를 2칸으로 설정할 때 사용한다.
    /// </summary>
    public void SetBlockSize2()
    {
        SetRequestedBlockSize(2);
    }

    /// <summary>
    /// 외부 UI 버튼이나 스크립트에서
    /// "현재 선택된 블록 크기"를 3칸으로 설정할 때 사용한다.
    /// </summary>
    public void SetBlockSize3()
    {
        SetRequestedBlockSize(3);
    }

    /// <summary>
    /// 외부에서 현재 사용할 블록 크기를 직접 전달한다.
    /// 전달값은 1~3으로 보정되고, 이후 기존 Draw 함수들에 공통 적용된다.
    /// </summary>
    /// <param name="blockSize">원하는 블록 크기</param>
    public void SetRequestedBlockSize(int blockSize)
    {
        requestedBlockSize = NormalizeBlockSize(blockSize);
        useRequestedBlockSizeFilter = true;

        Debug.Log($"[KSM_GATCHA] 현재 블록 크기 필터 설정 -> {requestedBlockSize}칸");
    }

    /// <summary>
    /// 현재 설정된 블록 크기 필터를 해제한다.
    /// 해제 후에는 크기 구분 없이 뽑는다.
    /// </summary>
    public void ClearRequestedBlockSizeFilter()
    {
        useRequestedBlockSizeFilter = false;
        Debug.Log("[KSM_GATCHA] 블록 크기 필터 해제 -> 모든 크기 허용");
    }

    /// <summary>
    /// 기본 뽑기 버튼 연결용 함수.
    /// 일반 블록(Regular) 중 includeInBasicDraw == true 인 엔트리들만 대상으로 랜덤 선택한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawBasicBlock()
    {
        int sizeFilter = GetCurrentSizeFilter();

        TryDraw(
            entry => entry.poolType == BlockPoolType.Regular &&
                     entry.includeInBasicDraw &&
                     MatchesBlockSize(entry, sizeFilter),
            BuildDrawLabel("기본 뽑기", sizeFilter)
        );
    }

    /// <summary>
    /// 외부에서 크기값을 직접 넘겨 기본 뽑기를 실행한다.
    /// 예: DrawBasicBlockBySize(2)
    /// </summary>
    /// <param name="blockSize">뽑고 싶은 블록 크기</param>
    public void DrawBasicBlockBySize(int blockSize)
    {
        int normalizedSize = NormalizeBlockSize(blockSize);

        TryDraw(
            entry => entry.poolType == BlockPoolType.Regular &&
                     entry.includeInBasicDraw &&
                     MatchesBlockSize(entry, normalizedSize),
            BuildDrawLabel("기본 뽑기", normalizedSize)
        );
    }

    /// <summary>
    /// 빨강 색상 블록만 대상으로 뽑기한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawRedTheme()
    {
        DrawByColor(BlockColorTheme.Red, GetCurrentSizeFilter());
    }

    /// <summary>
    /// 파랑 색상 블록만 대상으로 뽑기한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawBlueTheme()
    {
        DrawByColor(BlockColorTheme.Blue, GetCurrentSizeFilter());
    }

    /// <summary>
    /// 노랑 색상 블록만 대상으로 뽑기한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawYellowTheme()
    {
        DrawByColor(BlockColorTheme.Yellow, GetCurrentSizeFilter());
    }

    /// <summary>
    /// 화력 발전 블록만 대상으로 뽑기한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawThermalTheme()
    {
        DrawByGeneration(BlockGenerationTheme.Thermal, GetCurrentSizeFilter());
    }

    /// <summary>
    /// 수력 발전 블록만 대상으로 뽑기한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawHydroTheme()
    {
        DrawByGeneration(BlockGenerationTheme.Hydro, GetCurrentSizeFilter());
    }

    /// <summary>
    /// 태양광 발전 블록만 대상으로 뽑기한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawSolarTheme()
    {
        DrawByGeneration(BlockGenerationTheme.Solar, GetCurrentSizeFilter());
    }

    /// <summary>
    /// 발전 타입(특수 타입) 블록만 대상으로 뽑기한다.
    /// 현재 크기 필터가 켜져 있으면 그 크기만 뽑는다.
    /// </summary>
    public void DrawDevelopmentBlock()
    {
        int sizeFilter = GetCurrentSizeFilter();

        TryDraw(
            entry => entry.poolType == BlockPoolType.Development &&
                     MatchesBlockSize(entry, sizeFilter),
            BuildDrawLabel("발전 타입 뽑기", sizeFilter)
        );
    }

    /// <summary>
    /// 외부에서 크기값을 직접 넘겨 발전 타입 뽑기를 실행한다.
    /// </summary>
    /// <param name="blockSize">뽑고 싶은 블록 크기</param>
    public void DrawDevelopmentBlockBySize(int blockSize)
    {
        int normalizedSize = NormalizeBlockSize(blockSize);

        TryDraw(
            entry => entry.poolType == BlockPoolType.Development &&
                     MatchesBlockSize(entry, normalizedSize),
            BuildDrawLabel("발전 타입 뽑기", normalizedSize)
        );
    }

    /// <summary>
    /// 특정 색상 테마 블록만 대상으로 뽑기하는 내부 함수.
    /// </summary>
    /// <param name="colorTheme">대상 색상 테마</param>
    /// <param name="blockSize">대상 블록 크기. 0 이하면 모든 크기 허용</param>
    private void DrawByColor(BlockColorTheme colorTheme, int blockSize)
    {
        TryDraw(
            entry => entry.poolType == BlockPoolType.Regular &&
                     entry.colorTheme == colorTheme &&
                     MatchesBlockSize(entry, blockSize),
            BuildDrawLabel($"{GetColorLabel(colorTheme)} 색상 뽑기", blockSize)
        );
    }

    /// <summary>
    /// 특정 발전 종류 블록만 대상으로 뽑기하는 내부 함수.
    /// </summary>
    /// <param name="generationTheme">대상 발전 종류</param>
    /// <param name="blockSize">대상 블록 크기. 0 이하면 모든 크기 허용</param>
    private void DrawByGeneration(BlockGenerationTheme generationTheme, int blockSize)
    {
        TryDraw(
            entry => entry.poolType == BlockPoolType.Regular &&
                     entry.generationTheme == generationTheme &&
                     MatchesBlockSize(entry, blockSize),
            BuildDrawLabel($"{GetGenerationLabel(generationTheme)} 발전 뽑기", blockSize)
        );
    }

    /// <summary>
    /// 실제 뽑기 공통 함수.
    /// 1. 조건에 맞는 후보를 모은다.
    /// 2. 후보가 없으면 로그를 출력한다.
    /// 3. 후보가 있으면 가중치 랜덤으로 1개를 뽑는다.
    /// 4. 결과를 로그로 출력한다.
    /// </summary>
    /// <param name="filter">후보 엔트리를 고르는 조건</param>
    /// <param name="drawLabel">현재 뽑기 이름</param>
    /// <returns>선택된 엔트리. 실패하면 null</returns>
    private GatchaBlockEntry TryDraw(Predicate<GatchaBlockEntry> filter, string drawLabel)
    {
        List<GatchaBlockEntry> candidates = GetCandidates(filter);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[KSM_GATCHA] {drawLabel} 후보가 없습니다.");
            return null;
        }

        GatchaBlockEntry selectedEntry = GetWeightedRandomEntry(candidates);

        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {drawLabel} 실패 - 선택 결과가 null입니다.");
            return null;
        }

        if (printVerboseLog)
        {
            Debug.Log(
                $"[KSM_GATCHA] {drawLabel} 결과 -> " +
                $"이름: {selectedEntry.blockName}, " +
                $"크기: {selectedEntry.blockSize}칸, " +
                $"풀: {selectedEntry.poolType}, " +
                $"색상: {selectedEntry.colorTheme}, " +
                $"발전: {selectedEntry.generationTheme}, " +
                $"발전타입: {selectedEntry.developmentType}"
            );
        }
        else
        {
            Debug.Log(
                $"[KSM_GATCHA] {drawLabel} 결과 -> " +
                $"{selectedEntry.blockName} / {selectedEntry.blockSize}칸"
            );
        }

        return selectedEntry;
    }

    /// <summary>
    /// 조건에 맞는 후보 엔트리 목록을 반환한다.
    /// 비활성 엔트리, null 엔트리, weight 0 이하 엔트리,
    /// 잘못된 blockSize 엔트리는 제외한다.
    /// </summary>
    /// <param name="filter">후보 필터 조건</param>
    /// <returns>사용 가능한 후보 목록</returns>
    private List<GatchaBlockEntry> GetCandidates(Predicate<GatchaBlockEntry> filter)
    {
        List<GatchaBlockEntry> candidates = new List<GatchaBlockEntry>();

        for (int i = 0; i < blockEntries.Count; i++)
        {
            GatchaBlockEntry entry = blockEntries[i];

            if (!IsValidEntry(entry))
            {
                continue;
            }

            if (filter != null && !filter(entry))
            {
                continue;
            }

            candidates.Add(entry);
        }

        return candidates;
    }

    /// <summary>
    /// 엔트리가 뽑기 대상으로 유효한지 검사한다.
    /// </summary>
    /// <param name="entry">검사할 엔트리</param>
    /// <returns>유효하면 true</returns>
    private bool IsValidEntry(GatchaBlockEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        if (!entry.isEnabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.blockName))
        {
            return false;
        }

        if (entry.weight <= 0)
        {
            return false;
        }

        if (entry.blockSize < 1 || entry.blockSize > 3)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 후보 목록에서 가중치 기반 랜덤으로 1개를 선택한다.
    /// </summary>
    /// <param name="candidates">후보 목록</param>
    /// <returns>선택된 블록 엔트리</returns>
    private GatchaBlockEntry GetWeightedRandomEntry(List<GatchaBlockEntry> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        int totalWeight = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].weight;
        }

        int randomValue = UnityEngine.Random.Range(0, totalWeight);
        int accumulatedWeight = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            accumulatedWeight += candidates[i].weight;

            if (randomValue < accumulatedWeight)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    /// <summary>
    /// 현재 선택된 크기 필터를 반환한다.
    /// 필터가 꺼져 있으면 0을 반환해서 "모든 크기 허용"으로 처리한다.
    /// </summary>
    /// <returns>현재 활성 크기 필터</returns>
    private int GetCurrentSizeFilter()
    {
        if (!useRequestedBlockSizeFilter)
        {
            return 0;
        }

        return NormalizeBlockSize(requestedBlockSize);
    }

    /// <summary>
    /// 특정 엔트리가 원하는 블록 크기와 일치하는지 검사한다.
    /// sizeFilter가 0 이하면 모든 크기를 허용한다.
    /// </summary>
    /// <param name="entry">검사할 엔트리</param>
    /// <param name="sizeFilter">원하는 크기</param>
    /// <returns>조건 만족 여부</returns>
    private bool MatchesBlockSize(GatchaBlockEntry entry, int sizeFilter)
    {
        if (entry == null)
        {
            return false;
        }

        if (sizeFilter <= 0)
        {
            return true;
        }

        return entry.blockSize == sizeFilter;
    }

    /// <summary>
    /// 전달된 블록 크기 값을 1~3 범위로 보정한다.
    /// </summary>
    /// <param name="blockSize">원본 크기 값</param>
    /// <returns>1~3 범위로 보정된 값</returns>
    private int NormalizeBlockSize(int blockSize)
    {
        return Mathf.Clamp(blockSize, 1, 3);
    }

    /// <summary>
    /// 뽑기 라벨 문자열에 크기 정보를 추가해서 반환한다.
    /// </summary>
    /// <param name="baseLabel">기본 라벨</param>
    /// <param name="blockSize">크기 필터</param>
    /// <returns>표시용 라벨</returns>
    private string BuildDrawLabel(string baseLabel, int blockSize)
    {
        if (blockSize <= 0)
        {
            return baseLabel;
        }

        return $"{baseLabel} ({blockSize}칸)";
    }

    /// <summary>
    /// 현재 등록된 전체 엔트리를 로그에 출력한다.
    /// blockEntries 자동 생성이 잘 됐는지 확인할 때 사용한다.
    /// </summary>
    private void PrintAllEntries()
    {
        for (int i = 0; i < blockEntries.Count; i++)
        {
            GatchaBlockEntry entry = blockEntries[i];

            if (entry == null)
            {
                Debug.LogWarning($"[KSM_GATCHA] Entry {i} -> null");
                continue;
            }

            Debug.Log(
                $"[KSM_GATCHA] Entry {i} -> " +
                $"이름: {entry.blockName}, " +
                $"크기: {entry.blockSize}칸, " +
                $"풀: {entry.poolType}, " +
                $"색상: {entry.colorTheme}, " +
                $"발전: {entry.generationTheme}, " +
                $"발전타입: {entry.developmentType}, " +
                $"기본뽑기포함: {entry.includeInBasicDraw}, " +
                $"가중치: {entry.weight}"
            );
        }
    }

    /// <summary>
    /// blockEntries가 비어 있을 때 사용할 기본 블록 데이터를 생성한다.
    /// 
    /// 일반 블록:
    /// - 빨강 / 파랑 / 노랑
    /// - 화력 / 수력 / 태양광
    /// - 각 조합마다 1칸 / 2칸 / 3칸
    /// 
    /// 발전 타입 블록:
    /// - 구름 생성
    /// - 구름 제거
    /// - 각각 1칸 / 2칸 / 3칸
    /// </summary>
    private void CreateDefaultEntries()
    {
        blockEntries.Clear();

        for (int size = 1; size <= 3; size++)
        {
            AddRegularEntry($"{size}칸 빨강 화력 발전 블록", BlockColorTheme.Red, BlockGenerationTheme.Thermal, size);
            AddRegularEntry($"{size}칸 빨강 수력 발전 블록", BlockColorTheme.Red, BlockGenerationTheme.Hydro, size);
            AddRegularEntry($"{size}칸 빨강 태양광 발전 블록", BlockColorTheme.Red, BlockGenerationTheme.Solar, size);

            AddRegularEntry($"{size}칸 파랑 화력 발전 블록", BlockColorTheme.Blue, BlockGenerationTheme.Thermal, size);
            AddRegularEntry($"{size}칸 파랑 수력 발전 블록", BlockColorTheme.Blue, BlockGenerationTheme.Hydro, size);
            AddRegularEntry($"{size}칸 파랑 태양광 발전 블록", BlockColorTheme.Blue, BlockGenerationTheme.Solar, size);

            AddRegularEntry($"{size}칸 노랑 화력 발전 블록", BlockColorTheme.Yellow, BlockGenerationTheme.Thermal, size);
            AddRegularEntry($"{size}칸 노랑 수력 발전 블록", BlockColorTheme.Yellow, BlockGenerationTheme.Hydro, size);
            AddRegularEntry($"{size}칸 노랑 태양광 발전 블록", BlockColorTheme.Yellow, BlockGenerationTheme.Solar, size);

            AddDevelopmentEntry($"{size}칸 구름 생성 블록", BlockDevelopmentType.CloudCreate, size);
            AddDevelopmentEntry($"{size}칸 구름 제거 블록", BlockDevelopmentType.CloudRemove, size);
        }

        Debug.Log($"[KSM_GATCHA] 기본 가챠 데이터 {blockEntries.Count}개를 자동 생성했습니다.");
    }

    /// <summary>
    /// 일반 블록 엔트리를 하나 추가한다.
    /// </summary>
    /// <param name="name">블록 이름</param>
    /// <param name="colorTheme">색상 분류</param>
    /// <param name="generationTheme">발전 종류 분류</param>
    /// <param name="blockSize">블록 크기</param>
    private void AddRegularEntry(
        string name,
        BlockColorTheme colorTheme,
        BlockGenerationTheme generationTheme,
        int blockSize)
    {
        GatchaBlockEntry entry = new GatchaBlockEntry();

        entry.blockName = name;
        entry.isEnabled = true;
        entry.poolType = BlockPoolType.Regular;
        entry.colorTheme = colorTheme;
        entry.generationTheme = generationTheme;
        entry.developmentType = BlockDevelopmentType.None;
        entry.blockSize = NormalizeBlockSize(blockSize);
        entry.includeInBasicDraw = true;
        entry.weight = 1;

        blockEntries.Add(entry);
    }

    /// <summary>
    /// 발전 타입(특수 타입) 블록 엔트리를 하나 추가한다.
    /// </summary>
    /// <param name="name">블록 이름</param>
    /// <param name="developmentType">발전 타입 분류</param>
    /// <param name="blockSize">블록 크기</param>
    private void AddDevelopmentEntry(
        string name,
        BlockDevelopmentType developmentType,
        int blockSize)
    {
        GatchaBlockEntry entry = new GatchaBlockEntry();

        entry.blockName = name;
        entry.isEnabled = true;
        entry.poolType = BlockPoolType.Development;
        entry.colorTheme = BlockColorTheme.None;
        entry.generationTheme = BlockGenerationTheme.None;
        entry.developmentType = developmentType;
        entry.blockSize = NormalizeBlockSize(blockSize);
        entry.includeInBasicDraw = false;
        entry.weight = 1;

        blockEntries.Add(entry);
    }

    /// <summary>
    /// 색상 enum을 한글 문자열로 바꾼다.
    /// </summary>
    /// <param name="colorTheme">색상 enum</param>
    /// <returns>한글 색상 이름</returns>
    private string GetColorLabel(BlockColorTheme colorTheme)
    {
        switch (colorTheme)
        {
            case BlockColorTheme.Red:
                return "빨강";
            case BlockColorTheme.Blue:
                return "파랑";
            case BlockColorTheme.Yellow:
                return "노랑";
            default:
                return "없음";
        }
    }

    /// <summary>
    /// 발전 종류 enum을 한글 문자열로 바꾼다.
    /// </summary>
    /// <param name="generationTheme">발전 종류 enum</param>
    /// <returns>한글 발전 이름</returns>
    private string GetGenerationLabel(BlockGenerationTheme generationTheme)
    {
        switch (generationTheme)
        {
            case BlockGenerationTheme.Thermal:
                return "화력";
            case BlockGenerationTheme.Hydro:
                return "수력";
            case BlockGenerationTheme.Solar:
                return "태양광";
            default:
                return "없음";
        }
    }
}