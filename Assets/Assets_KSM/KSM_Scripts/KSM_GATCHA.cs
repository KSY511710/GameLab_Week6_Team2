using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MinePlinko_KSM_GATCHA
/// 
/// 현재 프로젝트용 가챠 테스트 스크립트.
/// 
/// 이번 버전의 기준:
/// 1. 회사(company) = 빨강 / 파랑 / 노랑 3개
/// 2. 부품(part) = 기호 블럭 9개
/// 3. 블록 크기 = 1칸 / 2칸 / 3칸
/// 
/// 뽑기 방식:
/// - 일반 뽑기
///   : 전체 블록 중 무작위 1개
/// 
/// - 기업 블럭 뽑기
///   : 특정 회사(색상)를 먼저 선택
///   : 선택 회사 60%, 나머지 두 회사는 각 20%
////  : 그 후 선택된 회사 안에서 기호 9종 중 하나를 랜덤
/// 
/// - 부품 뽑기
///   : 특정 부품(기호)을 먼저 선택
///   : 선택 부품 40%, 나머지 8개는 각각 7.5%
///   : 색상은 선택과 무관하며, 최종 색상은 빨/파/노 1/3 확률
/// 
/// 현재 단계에서는 "로그 확인용" 테스트 스크립트로 유지했다.
/// 실제 인벤토리 지급, 비용 차감, UI 결과창 연결은 아직 넣지 않았다.
/// </summary>
public class KSM_GATCHA : MonoBehaviour
{
    /// <summary>
    /// 회사(기업) 분류.
    /// 유저가 말한 company = 색 3개를 의미한다.
    /// </summary>
    public enum CompanyColor
    {
        None,
        Red,
        Blue,
        Yellow
    }

    /// <summary>
    /// 부품(기호) 분류.
    /// 현재는 9개의 기호 블럭을 의미한다.
    /// 실제 프로젝트에서 이름이 정해지면 Symbol01 ~ Symbol09 대신
    /// 원하는 이름으로 바꿔도 된다.
    /// </summary>
    public enum BlockSymbolType
    {
        None,
        Symbol01,
        Symbol02,
        Symbol03,
        Symbol04,
        Symbol05,
        //Symbol06,
        //Symbol07,
        //Symbol08,
        //Symbol09
    }

    /// <summary>
    /// 가챠에 등록되는 블록 데이터 1개.
    /// 하나의 엔트리는
    /// "색상 1개 + 기호 1개 + 크기 1개" 조합을 뜻한다.
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
        [Tooltip("회사(색상)")]
        public CompanyColor companyColor = CompanyColor.Red;

        [Tooltip("부품(기호)")]
        public BlockSymbolType symbolType = BlockSymbolType.Symbol01;

        [Header("블록 크기 정보")]
        [Range(1, 3)]
        [Tooltip("블록 크기. 1, 2, 3 중 하나만 사용한다.")]
        public int blockSize = 1;

        [Header("뽑기 포함 여부")]
        [Tooltip("일반 뽑기 대상에 포함할지 여부")]
        public bool includeInGeneralDraw = true;
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

    [Header("현재 선택된 회사(기업)")]
    [Tooltip("기업 블럭 뽑기에서 사용할 현재 선택 회사")]
    [SerializeField]
    private CompanyColor selectedCompany = CompanyColor.None;

    [Header("현재 선택된 부품(기호)")]
    [Tooltip("부품 뽑기에서 사용할 현재 선택 부품")]
    [SerializeField]
    private BlockSymbolType selectedPartSymbol = BlockSymbolType.None;

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
    /// blockEntries가 비어 있다면
    /// 3색 x 9기호 x 3크기 조합을 자동 생성한다.
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
    /// 현재 등록된 엔트리 목록을 로그에 출력한다.
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
    /// 인스펙터 값이 수정될 때 잘못된 값을 보정한다.
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
        }
    }

    #region 회사 선택

    /// <summary>
    /// 회사를 빨강으로 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetCompanyRed()
    {
        SetSelectedCompany(CompanyColor.Red);
    }

    /// <summary>
    /// 회사를 파랑으로 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetCompanyBlue()
    {
        SetSelectedCompany(CompanyColor.Blue);
    }

    /// <summary>
    /// 회사를 노랑으로 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetCompanyYellow()
    {
        SetSelectedCompany(CompanyColor.Yellow);
    }

    /// <summary>
    /// int 값으로 회사를 선택한다.
    /// 1 = 빨강, 2 = 파랑, 3 = 노랑
    /// 유니티 Button OnClick(int) 연결용으로 사용 가능하다.
    /// </summary>
    /// <param name="companyIndex">1~3 회사 번호</param>
    public void SetSelectedCompanyByIndex(int companyIndex)
    {
        switch (companyIndex)
        {
            case 1:
                SetSelectedCompany(CompanyColor.Red);
                break;
            case 2:
                SetSelectedCompany(CompanyColor.Blue);
                break;
            case 3:
                SetSelectedCompany(CompanyColor.Yellow);
                break;
            default:
                Debug.LogWarning("[KSM_GATCHA] 회사 선택 실패 - 1(빨강), 2(파랑), 3(노랑) 중 하나를 전달해야 합니다.");
                break;
        }
    }

    /// <summary>
    /// enum 값으로 회사를 선택한다.
    /// </summary>
    /// <param name="companyColor">선택할 회사 색상</param>
    public void SetSelectedCompany(CompanyColor companyColor)
    {
        if (companyColor == CompanyColor.None)
        {
            Debug.LogWarning("[KSM_GATCHA] CompanyColor.None 은 선택할 수 없습니다.");
            return;
        }

        selectedCompany = companyColor;
        Debug.Log($"[KSM_GATCHA] 현재 선택 회사 -> {GetCompanyLabel(selectedCompany)}");
    }

    #endregion

    #region 부품 선택

    /// <summary>
    /// 1번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol1() { SetSelectedPartSymbol(1); }

    /// <summary>
    /// 2번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol2() { SetSelectedPartSymbol(2); }

    /// <summary>
    /// 3번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol3() { SetSelectedPartSymbol(3); }

    /// <summary>
    /// 4번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol4() { SetSelectedPartSymbol(4); }

    /// <summary>
    /// 5번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol5() { SetSelectedPartSymbol(5); }

    /// <summary>
    /// 6번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol6() { SetSelectedPartSymbol(6); }

    /// <summary>
    /// 7번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol7() { SetSelectedPartSymbol(7); }

    /// <summary>
    /// 8번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol8() { SetSelectedPartSymbol(8); }

    /// <summary>
    /// 9번 부품을 선택한다.
    /// 버튼 연결용.
    /// </summary>
    public void SetPartSymbol9() { SetSelectedPartSymbol(9); }

    /// <summary>
    /// int 값으로 부품을 선택한다.
    /// 1 ~ 9 를 사용한다.
    /// 유니티 Button OnClick(int) 연결용으로 사용 가능하다.
    /// </summary>
    /// <param name="symbolNumber">1~9 부품 번호</param>
    public void SetSelectedPartSymbol(int symbolNumber)
    {
        BlockSymbolType convertedSymbol = ConvertNumberToSymbol(symbolNumber);

        if (convertedSymbol == BlockSymbolType.None)
        {
            Debug.LogWarning("[KSM_GATCHA] 부품 선택 실패 - 1~9 사이 값을 전달해야 합니다.");
            return;
        }

        selectedPartSymbol = convertedSymbol;
        Debug.Log($"[KSM_GATCHA] 현재 선택 부품 -> {GetSymbolLabel(selectedPartSymbol)}");
    }

    #endregion

    #region 크기 선택

    /// <summary>
    /// 외부 UI 버튼에서 1칸 크기를 선택할 때 사용한다.
    /// </summary>
    public void SetBlockSize1()
    {
        SetRequestedBlockSize(1);
    }

    /// <summary>
    /// 외부 UI 버튼에서 2칸 크기를 선택할 때 사용한다.
    /// </summary>
    public void SetBlockSize2()
    {
        SetRequestedBlockSize(2);
    }

    /// <summary>
    /// 외부 UI 버튼에서 3칸 크기를 선택할 때 사용한다.
    /// </summary>
    public void SetBlockSize3()
    {
        SetRequestedBlockSize(3);
    }

    /// <summary>
    /// 현재 사용할 블록 크기를 직접 설정한다.
    /// 이후 뽑기에서 공통으로 해당 크기만 대상으로 사용된다.
    /// </summary>
    /// <param name="blockSize">원하는 블록 크기</param>
    public void SetRequestedBlockSize(int blockSize)
    {
        requestedBlockSize = NormalizeBlockSize(blockSize);
        useRequestedBlockSizeFilter = true;

        Debug.Log($"[KSM_GATCHA] 현재 블록 크기 필터 설정 -> {requestedBlockSize}칸");
    }

    /// <summary>
    /// 현재 크기 필터를 해제한다.
    /// 해제 후에는 1칸/2칸/3칸 전체가 대상이 된다.
    /// </summary>
    public void ClearRequestedBlockSizeFilter()
    {
        useRequestedBlockSizeFilter = false;
        Debug.Log("[KSM_GATCHA] 블록 크기 필터 해제 -> 모든 크기 허용");
    }

    #endregion

    #region 뽑기 실행

    /// <summary>
    /// 일반 뽑기.
    /// 전체 블록 중 랜덤 1개를 뽑는다.
    /// 크기 필터가 켜져 있으면 해당 크기만 대상으로 한다.
    /// </summary>
    public GatchaBlockEntry DrawGeneralBlock()
    {
        int sizeFilter = GetCurrentSizeFilter();

        List<GatchaBlockEntry> candidates = GetCandidates(
            entry => entry.includeInGeneralDraw && MatchesBlockSize(entry, sizeFilter)
        );

        GatchaBlockEntry selectedEntry = GetRandomEntry(candidates);

        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("일반 뽑기", sizeFilter)} 후보가 없습니다.");
            return null; // 📌 return; 을 return null; 로 변경
        }

        PrintDrawResult(BuildDrawLabel("일반 뽑기", sizeFilter), selectedEntry);

        return selectedEntry; // 📌 맨 마지막에 당첨 결과 반환!
    }
    /// <summary>
    /// 기존 DrawBasicBlock 이름을 그대로 쓰고 싶을 때를 위한 호환용 함수.
    /// 현재는 일반 뽑기와 동일하게 동작한다.
    /// </summary>
    public void DrawBasicBlock()
    {
        DrawGeneralBlock();
    }

    /// <summary>
    /// 기업 블럭 뽑기.
    /// 1. 선택된 회사 색상을 기준으로 60 / 20 / 20 확률로 회사 결정
    /// 2. 결정된 회사 안에서 기호를 랜덤 결정
    /// 3. 최종 엔트리 선택
    /// </summary>
    // 📌 void -> GatchaBlockEntry 로 변경
    public GatchaBlockEntry DrawCompanyBlock()
    {
        int sizeFilter = GetCurrentSizeFilter();

        if (selectedCompany == CompanyColor.None)
        {
            Debug.LogWarning("[KSM_GATCHA] 기업 블럭 뽑기 실패 - 먼저 회사를 선택해야 합니다.");
            return null; // 📌 여기부터 나오는 return; 은 전부 return null; 로 변경
        }

        List<GatchaBlockEntry> sizeFilteredCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, sizeFilter)
        );

        if (sizeFilteredCandidates.Count == 0)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("기업 블럭 뽑기", sizeFilter)} 후보가 없습니다.");
            return null;
        }

        CompanyColor finalCompany = SelectCompanyForCompanyDraw(sizeFilteredCandidates, selectedCompany);

        if (finalCompany == CompanyColor.None)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("기업 블럭 뽑기", sizeFilter)} 실패 - 회사 선택 결과가 없습니다.");
            return null;
        }

        BlockSymbolType finalSymbol = SelectRandomSymbolFromCompany(sizeFilteredCandidates, finalCompany);

        if (finalSymbol == BlockSymbolType.None)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("기업 블럭 뽑기", sizeFilter)} 실패 - 기호 선택 결과가 없습니다.");
            return null;
        }

        List<GatchaBlockEntry> finalCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, sizeFilter) &&
                     entry.companyColor == finalCompany &&
                     entry.symbolType == finalSymbol
        );

        GatchaBlockEntry selectedEntry = GetRandomEntry(finalCandidates);

        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("기업 블럭 뽑기", sizeFilter)} 실패 - 최종 엔트리가 없습니다.");
            return null;
        }

        PrintDrawResult(
            BuildDrawLabel("기업 블럭 뽑기", sizeFilter),
            selectedEntry,
            $"선택 회사: {GetCompanyLabel(selectedCompany)} / 최종 회사: {GetCompanyLabel(finalCompany)} / 최종 기호: {GetSymbolLabel(finalSymbol)}"
        );

        return selectedEntry; // 📌 당첨 결과 반환!
    }

    /// <summary>
    /// 부품 뽑기.
    /// 1. 선택된 부품 기호를 기준으로 40% / 7.5 x 8 확률로 기호 결정
    /// 2. 결정된 기호에 대해 색상은 빨/파/노 1/3 확률
    /// 3. 최종 엔트리 선택
    /// </summary>
    // 📌 void -> GatchaBlockEntry 로 변경
    public GatchaBlockEntry DrawPartBlock()
    {
        int sizeFilter = GetCurrentSizeFilter();

        if (selectedPartSymbol == BlockSymbolType.None)
        {
            Debug.LogWarning("[KSM_GATCHA] 부품 뽑기 실패 - 먼저 부품(기호)을 선택해야 합니다.");
            return null; // 📌 마찬가지로 전부 return null; 로 변경
        }

        List<GatchaBlockEntry> sizeFilteredCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, sizeFilter)
        );

        if (sizeFilteredCandidates.Count == 0)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("부품 뽑기", sizeFilter)} 후보가 없습니다.");
            return null;
        }

        BlockSymbolType finalSymbol = SelectSymbolForPartDraw(sizeFilteredCandidates, selectedPartSymbol);

        if (finalSymbol == BlockSymbolType.None)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("부품 뽑기", sizeFilter)} 실패 - 부품 선택 결과가 없습니다.");
            return null;
        }

        CompanyColor finalCompany = SelectRandomCompanyFromSymbol(sizeFilteredCandidates, finalSymbol);

        if (finalCompany == CompanyColor.None)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("부품 뽑기", sizeFilter)} 실패 - 색상 선택 결과가 없습니다.");
            return null;
        }

        List<GatchaBlockEntry> finalCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, sizeFilter) &&
                     entry.symbolType == finalSymbol &&
                     entry.companyColor == finalCompany
        );

        GatchaBlockEntry selectedEntry = GetRandomEntry(finalCandidates);

        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("부품 뽑기", sizeFilter)} 실패 - 최종 엔트리가 없습니다.");
            return null;
        }

        PrintDrawResult(
            BuildDrawLabel("부품 뽑기", sizeFilter),
            selectedEntry,
            $"선택 부품: {GetSymbolLabel(selectedPartSymbol)} / 최종 부품: {GetSymbolLabel(finalSymbol)} / 최종 색상: {GetCompanyLabel(finalCompany)}"
        );

        return selectedEntry; // 📌 당첨 결과 반환!
    }

    /// <summary>
    /// 외부에서 1칸/2칸/3칸을 직접 넘겨 일반 뽑기를 실행하고 싶을 때 사용한다.
    /// </summary>
    /// <param name="blockSize">원하는 크기</param>
    public void DrawGeneralBlockBySize(int blockSize)
    {
        int normalizedSize = NormalizeBlockSize(blockSize);

        List<GatchaBlockEntry> candidates = GetCandidates(
            entry => entry.includeInGeneralDraw && MatchesBlockSize(entry, normalizedSize)
        );

        GatchaBlockEntry selectedEntry = GetRandomEntry(candidates);

        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("일반 뽑기", normalizedSize)} 후보가 없습니다.");
            return;
        }

        PrintDrawResult(BuildDrawLabel("일반 뽑기", normalizedSize), selectedEntry);
    }

    /// <summary>
    /// 외부에서 1칸/2칸/3칸을 직접 넘겨 기업 뽑기를 실행하고 싶을 때 사용한다.
    /// </summary>
    /// <param name="blockSize">원하는 크기</param>
    public void DrawCompanyBlockBySize(int blockSize)
    {
        int normalizedSize = NormalizeBlockSize(blockSize);

        if (selectedCompany == CompanyColor.None)
        {
            Debug.LogWarning("[KSM_GATCHA] 기업 블럭 뽑기 실패 - 먼저 회사를 선택해야 합니다.");
            return;
        }

        List<GatchaBlockEntry> sizeFilteredCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, normalizedSize)
        );

        if (sizeFilteredCandidates.Count == 0)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("기업 블럭 뽑기", normalizedSize)} 후보가 없습니다.");
            return;
        }

        CompanyColor finalCompany = SelectCompanyForCompanyDraw(sizeFilteredCandidates, selectedCompany);
        BlockSymbolType finalSymbol = SelectRandomSymbolFromCompany(sizeFilteredCandidates, finalCompany);

        List<GatchaBlockEntry> finalCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, normalizedSize) &&
                     entry.companyColor == finalCompany &&
                     entry.symbolType == finalSymbol
        );

        GatchaBlockEntry selectedEntry = GetRandomEntry(finalCandidates);

        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("기업 블럭 뽑기", normalizedSize)} 실패 - 최종 엔트리가 없습니다.");
            return;
        }

        PrintDrawResult(
            BuildDrawLabel("기업 블럭 뽑기", normalizedSize),
            selectedEntry,
            $"선택 회사: {GetCompanyLabel(selectedCompany)} / 최종 회사: {GetCompanyLabel(finalCompany)} / 최종 기호: {GetSymbolLabel(finalSymbol)}"
        );
    }

    /// <summary>
    /// 외부에서 1칸/2칸/3칸을 직접 넘겨 부품 뽑기를 실행하고 싶을 때 사용한다.
    /// </summary>
    /// <param name="blockSize">원하는 크기</param>
    public void DrawPartBlockBySize(int blockSize)
    {
        int normalizedSize = NormalizeBlockSize(blockSize);

        if (selectedPartSymbol == BlockSymbolType.None)
        {
            Debug.LogWarning("[KSM_GATCHA] 부품 뽑기 실패 - 먼저 부품(기호)을 선택해야 합니다.");
            return;
        }

        List<GatchaBlockEntry> sizeFilteredCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, normalizedSize)
        );

        if (sizeFilteredCandidates.Count == 0)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("부품 뽑기", normalizedSize)} 후보가 없습니다.");
            return;
        }

        BlockSymbolType finalSymbol = SelectSymbolForPartDraw(sizeFilteredCandidates, selectedPartSymbol);
        CompanyColor finalCompany = SelectRandomCompanyFromSymbol(sizeFilteredCandidates, finalSymbol);

        List<GatchaBlockEntry> finalCandidates = GetCandidates(
            entry => MatchesBlockSize(entry, normalizedSize) &&
                     entry.symbolType == finalSymbol &&
                     entry.companyColor == finalCompany
        );

        GatchaBlockEntry selectedEntry = GetRandomEntry(finalCandidates);

        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {BuildDrawLabel("부품 뽑기", normalizedSize)} 실패 - 최종 엔트리가 없습니다.");
            return;
        }

        PrintDrawResult(
            BuildDrawLabel("부품 뽑기", normalizedSize),
            selectedEntry,
            $"선택 부품: {GetSymbolLabel(selectedPartSymbol)} / 최종 부품: {GetSymbolLabel(finalSymbol)} / 최종 색상: {GetCompanyLabel(finalCompany)}"
        );
    }

    #endregion

    #region 확률 선택 로직

    /// <summary>
    /// 기업 블럭 뽑기용 회사 선택 로직.
    /// 
    /// 규칙:
    /// - 선택 회사가 존재하면 60%
    /// - 나머지 회사들은 남은 40%를 균등 분배
    /// - 기본 데이터가 정상이라면 실제로는 60 / 20 / 20이 된다
    /// </summary>
    /// <param name="candidates">현재 크기 필터까지 적용된 후보 목록</param>
    /// <param name="preferredCompany">유저가 선택한 회사</param>
    /// <returns>최종 선택된 회사</returns>
    private CompanyColor SelectCompanyForCompanyDraw(List<GatchaBlockEntry> candidates, CompanyColor preferredCompany)
    {
        List<CompanyColor> availableCompanies = GetAvailableCompanies(candidates);

        if (availableCompanies.Count == 0)
        {
            return CompanyColor.None;
        }

        bool preferredExists = availableCompanies.Contains(preferredCompany);

        if (!preferredExists)
        {
            return GetRandomCompany(availableCompanies);
        }

        List<CompanyColor> otherCompanies = new List<CompanyColor>();

        for (int i = 0; i < availableCompanies.Count; i++)
        {
            if (availableCompanies[i] != preferredCompany)
            {
                otherCompanies.Add(availableCompanies[i]);
            }
        }

        if (otherCompanies.Count == 0)
        {
            return preferredCompany;
        }

        float roll = UnityEngine.Random.value;

        if (roll < 0.6f)
        {
            return preferredCompany;
        }

        return GetRandomCompany(otherCompanies);
    }

    /// <summary>
    /// 부품 뽑기용 부품 선택 로직.
    /// 
    /// 규칙:
    /// - 선택 부품이 존재하면 40%
    /// - 나머지 부품들은 남은 60%를 균등 분배
    /// - 기본 데이터가 정상이라면 실제로는 40 / 7.5 x 8 이 된다
    /// </summary>
    /// <param name="candidates">현재 크기 필터까지 적용된 후보 목록</param>
    /// <param name="preferredSymbol">유저가 선택한 부품</param>
    /// <returns>최종 선택된 부품</returns>
    private BlockSymbolType SelectSymbolForPartDraw(List<GatchaBlockEntry> candidates, BlockSymbolType preferredSymbol)
    {
        List<BlockSymbolType> availableSymbols = GetAvailableSymbols(candidates);

        if (availableSymbols.Count == 0)
        {
            return BlockSymbolType.None;
        }

        bool preferredExists = availableSymbols.Contains(preferredSymbol);

        if (!preferredExists)
        {
            return GetRandomSymbol(availableSymbols);
        }

        List<BlockSymbolType> otherSymbols = new List<BlockSymbolType>();

        for (int i = 0; i < availableSymbols.Count; i++)
        {
            if (availableSymbols[i] != preferredSymbol)
            {
                otherSymbols.Add(availableSymbols[i]);
            }
        }

        if (otherSymbols.Count == 0)
        {
            return preferredSymbol;
        }

        float roll = UnityEngine.Random.value;

        if (roll < 0.4f)
        {
            return preferredSymbol;
        }

        return GetRandomSymbol(otherSymbols);
    }

    /// <summary>
    /// 특정 회사 안에서 사용 가능한 기호 중 하나를 랜덤으로 선택한다.
    /// 기업 블럭 뽑기에서 사용된다.
    /// </summary>
    /// <param name="candidates">현재 후보 목록</param>
    /// <param name="companyColor">확정된 회사 색상</param>
    /// <returns>해당 회사에서 선택된 기호</returns>
    private BlockSymbolType SelectRandomSymbolFromCompany(List<GatchaBlockEntry> candidates, CompanyColor companyColor)
    {
        HashSet<BlockSymbolType> symbolSet = new HashSet<BlockSymbolType>();

        for (int i = 0; i < candidates.Count; i++)
        {
            GatchaBlockEntry entry = candidates[i];

            if (entry.companyColor == companyColor)
            {
                symbolSet.Add(entry.symbolType);
            }
        }

        List<BlockSymbolType> symbolList = new List<BlockSymbolType>(symbolSet);
        return GetRandomSymbol(symbolList);
    }

    /// <summary>
    /// 특정 부품(기호)에 대해 사용 가능한 회사 색상 중 하나를 랜덤으로 선택한다.
    /// 부품 뽑기에서 색상은 1/3 확률로 선택되어야 하므로,
    /// 기본 데이터가 모두 존재할 경우 빨/파/노 동일 확률이 된다.
    /// </summary>
    /// <param name="candidates">현재 후보 목록</param>
    /// <param name="symbolType">확정된 부품 기호</param>
    /// <returns>해당 기호에서 선택된 회사 색상</returns>
    private CompanyColor SelectRandomCompanyFromSymbol(List<GatchaBlockEntry> candidates, BlockSymbolType symbolType)
    {
        HashSet<CompanyColor> companySet = new HashSet<CompanyColor>();

        for (int i = 0; i < candidates.Count; i++)
        {
            GatchaBlockEntry entry = candidates[i];

            if (entry.symbolType == symbolType)
            {
                companySet.Add(entry.companyColor);
            }
        }

        List<CompanyColor> companyList = new List<CompanyColor>(companySet);
        return GetRandomCompany(companyList);
    }

    #endregion

    #region 후보 수집 / 유효성 검사

    /// <summary>
    /// 조건에 맞는 후보 엔트리 목록을 반환한다.
    /// null, 비활성, 잘못된 값 엔트리는 제외한다.
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

        if (entry.companyColor == CompanyColor.None)
        {
            return false;
        }

        if (entry.symbolType == BlockSymbolType.None)
        {
            return false;
        }

        if (entry.blockSize < 1 || entry.blockSize > 3)
        {
            return false;
        }

        return true;
    }

    #endregion

    #region 랜덤 유틸

    /// <summary>
    /// 후보 엔트리 리스트 중 하나를 균등 확률로 선택한다.
    /// </summary>
    /// <param name="candidates">후보 목록</param>
    /// <returns>선택된 엔트리</returns>
    private GatchaBlockEntry GetRandomEntry(List<GatchaBlockEntry> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[randomIndex];
    }

    /// <summary>
    /// 회사 목록 중 하나를 균등 확률로 선택한다.
    /// </summary>
    /// <param name="companyList">회사 목록</param>
    /// <returns>선택된 회사</returns>
    private CompanyColor GetRandomCompany(List<CompanyColor> companyList)
    {
        if (companyList == null || companyList.Count == 0)
        {
            return CompanyColor.None;
        }

        int randomIndex = UnityEngine.Random.Range(0, companyList.Count);
        return companyList[randomIndex];
    }

    /// <summary>
    /// 부품 기호 목록 중 하나를 균등 확률로 선택한다.
    /// </summary>
    /// <param name="symbolList">기호 목록</param>
    /// <returns>선택된 기호</returns>
    private BlockSymbolType GetRandomSymbol(List<BlockSymbolType> symbolList)
    {
        if (symbolList == null || symbolList.Count == 0)
        {
            return BlockSymbolType.None;
        }

        int randomIndex = UnityEngine.Random.Range(0, symbolList.Count);
        return symbolList[randomIndex];
    }

    /// <summary>
    /// 현재 후보에서 사용 가능한 회사 목록을 만든다.
    /// 중복은 제거한다.
    /// </summary>
    /// <param name="candidates">현재 후보 목록</param>
    /// <returns>사용 가능한 회사 목록</returns>
    private List<CompanyColor> GetAvailableCompanies(List<GatchaBlockEntry> candidates)
    {
        HashSet<CompanyColor> companySet = new HashSet<CompanyColor>();

        for (int i = 0; i < candidates.Count; i++)
        {
            companySet.Add(candidates[i].companyColor);
        }

        return new List<CompanyColor>(companySet);
    }

    /// <summary>
    /// 현재 후보에서 사용 가능한 부품 기호 목록을 만든다.
    /// 중복은 제거한다.
    /// </summary>
    /// <param name="candidates">현재 후보 목록</param>
    /// <returns>사용 가능한 기호 목록</returns>
    private List<BlockSymbolType> GetAvailableSymbols(List<GatchaBlockEntry> candidates)
    {
        HashSet<BlockSymbolType> symbolSet = new HashSet<BlockSymbolType>();

        for (int i = 0; i < candidates.Count; i++)
        {
            symbolSet.Add(candidates[i].symbolType);
        }

        return new List<BlockSymbolType>(symbolSet);
    }

    #endregion

    #region 공통 보조 함수

    /// <summary>
    /// 현재 활성화된 크기 필터를 반환한다.
    /// 필터가 꺼져 있으면 0을 반환하여 "모든 크기 허용"으로 사용한다.
    /// </summary>
    /// <returns>현재 크기 필터 값</returns>
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
    /// sizeFilter가 0 이하이면 모든 크기를 허용한다.
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
    /// 전달된 블록 크기를 1~3 범위로 보정한다.
    /// </summary>
    /// <param name="blockSize">원본 크기 값</param>
    /// <returns>1~3 범위로 보정된 값</returns>
    private int NormalizeBlockSize(int blockSize)
    {
        return Mathf.Clamp(blockSize, 1, 3);
    }

    /// <summary>
    /// 숫자 1~9를 BlockSymbolType으로 변환한다.
    /// </summary>
    /// <param name="symbolNumber">1~9 기호 번호</param>
    /// <returns>변환된 enum 값</returns>
    private BlockSymbolType ConvertNumberToSymbol(int symbolNumber)
    {
        switch (symbolNumber)
        {
            case 1: return BlockSymbolType.Symbol01;
            case 2: return BlockSymbolType.Symbol02;
            case 3: return BlockSymbolType.Symbol03;
            case 4: return BlockSymbolType.Symbol04;
            case 5: return BlockSymbolType.Symbol05;
            //case 6: return BlockSymbolType.Symbol06;
            //case 7: return BlockSymbolType.Symbol07;
            //case 8: return BlockSymbolType.Symbol08;
            //case 9: return BlockSymbolType.Symbol09;
            default: return BlockSymbolType.None;
        }
    }

    /// <summary>
    /// 뽑기 라벨 문자열에 크기 정보를 추가한다.
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
    /// 뽑기 결과를 콘솔에 출력한다.
    /// 추가 설명이 있으면 함께 출력한다.
    /// </summary>
    /// <param name="drawLabel">뽑기 이름</param>
    /// <param name="selectedEntry">선택된 엔트리</param>
    /// <param name="extraInfo">추가 로그 정보</param>
    private void PrintDrawResult(string drawLabel, GatchaBlockEntry selectedEntry, string extraInfo = "")
    {
        if (selectedEntry == null)
        {
            Debug.LogWarning($"[KSM_GATCHA] {drawLabel} 실패 - 결과 엔트리가 null입니다.");
            return;
        }

        if (printVerboseLog)
        {
            string extraLog = string.IsNullOrEmpty(extraInfo) ? "" : $" / {extraInfo}";

            Debug.Log(
                $"[KSM_GATCHA] {drawLabel} 결과 -> " +
                $"이름: {selectedEntry.blockName}, " +
                $"회사: {GetCompanyLabel(selectedEntry.companyColor)}, " +
                $"부품: {GetSymbolLabel(selectedEntry.symbolType)}, " +
                $"크기: {selectedEntry.blockSize}칸" +
                $"{extraLog}"
            );
        }
        else
        {
            Debug.Log(
                $"[KSM_GATCHA] {drawLabel} 결과 -> " +
                $"{selectedEntry.blockName}"
            );
        }
    }

    #endregion

    #region 기본 데이터 생성 / 로그 출력

    /// <summary>
    /// blockEntries가 비어 있을 때 사용할 기본 블록 데이터를 생성한다.
    /// 
    /// 생성 규칙:
    /// - 회사 3개(빨강/파랑/노랑)
    /// - 부품 9개(기호1~기호9)
    /// - 크기 1, 2, 3
    /// 
    /// 총 3 x 9 x 3 = 81개 엔트리가 생성된다.
    /// </summary>
    private void CreateDefaultEntries()
    {
        blockEntries.Clear();

        CompanyColor[] companies =
        {
            CompanyColor.Red,
            CompanyColor.Blue,
            CompanyColor.Yellow
        };

        BlockSymbolType[] symbols =
        {
            BlockSymbolType.Symbol01,
            BlockSymbolType.Symbol02,
            BlockSymbolType.Symbol03,
            BlockSymbolType.Symbol04,
            BlockSymbolType.Symbol05,
            //BlockSymbolType.Symbol06,
            //BlockSymbolType.Symbol07,
            //BlockSymbolType.Symbol08,
            //BlockSymbolType.Symbol09
        };

        for (int size = 1; size <= 3; size++)
        {
            for (int companyIndex = 0; companyIndex < companies.Length; companyIndex++)
            {
                for (int symbolIndex = 0; symbolIndex < symbols.Length; symbolIndex++)
                {
                    AddEntry(
                        $"{size}칸 {GetCompanyLabel(companies[companyIndex])} {GetSymbolLabel(symbols[symbolIndex])} 블록",
                        companies[companyIndex],
                        symbols[symbolIndex],
                        size
                    );
                }
            }
        }

        Debug.Log($"[KSM_GATCHA] 기본 가챠 데이터 {blockEntries.Count}개를 자동 생성했습니다.");
    }

    /// <summary>
    /// 블록 엔트리를 하나 추가한다.
    /// </summary>
    /// <param name="name">블록 이름</param>
    /// <param name="companyColor">회사 색상</param>
    /// <param name="symbolType">부품 기호</param>
    /// <param name="blockSize">블록 크기</param>
    private void AddEntry(string name, CompanyColor companyColor, BlockSymbolType symbolType, int blockSize)
    {
        GatchaBlockEntry entry = new GatchaBlockEntry();

        entry.blockName = name;
        entry.isEnabled = true;
        entry.companyColor = companyColor;
        entry.symbolType = symbolType;
        entry.blockSize = NormalizeBlockSize(blockSize);
        entry.includeInGeneralDraw = true;

        blockEntries.Add(entry);
    }

    /// <summary>
    /// 현재 등록된 전체 엔트리를 로그에 출력한다.
    /// 데이터가 정상 생성되었는지 확인할 때 사용한다.
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
                $"회사: {GetCompanyLabel(entry.companyColor)}, " +
                $"부품: {GetSymbolLabel(entry.symbolType)}, " +
                $"크기: {entry.blockSize}칸, " +
                $"일반뽑기포함: {entry.includeInGeneralDraw}, " +
                $"활성화: {entry.isEnabled}"
            );
        }
    }

    #endregion

    #region 라벨 표시용 문자열

    /// <summary>
    /// 회사 enum을 한글 문자열로 바꾼다.
    /// </summary>
    /// <param name="companyColor">회사 enum</param>
    /// <returns>한글 회사 이름</returns>
    private string GetCompanyLabel(CompanyColor companyColor)
    {
        switch (companyColor)
        {
            case CompanyColor.Red:
                return "빨강";
            case CompanyColor.Blue:
                return "파랑";
            case CompanyColor.Yellow:
                return "노랑";
            default:
                return "없음";
        }
    }

    /// <summary>
    /// 부품 enum을 한글 문자열로 바꾼다.
    /// </summary>
    /// <param name="symbolType">부품 enum</param>
    /// <returns>한글 부품 이름</returns>
    private string GetSymbolLabel(BlockSymbolType symbolType)
    {
        switch (symbolType)
        {
            case BlockSymbolType.Symbol01: return "기호1";
            case BlockSymbolType.Symbol02: return "기호2";
            case BlockSymbolType.Symbol03: return "기호3";
            case BlockSymbolType.Symbol04: return "기호4";
            case BlockSymbolType.Symbol05: return "기호5";
            //case BlockSymbolType.Symbol06: return "기호6";
            //case BlockSymbolType.Symbol07: return "기호7";
            //case BlockSymbolType.Symbol08: return "기호8";
            //case BlockSymbolType.Symbol09: return "기호9";
            default: return "없음";
        }
    }

    #endregion
}