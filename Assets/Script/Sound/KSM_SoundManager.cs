using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// KSM_BgmType
/// 
/// 역할:
/// 1. 프로젝트에서 사용하는 BGM 종류를 enum으로 관리한다.
/// 2. 실제 AudioClip을 직접 하드코딩하지 않고 enum -> 인스펙터 매핑 방식으로 연결할 수 있게 한다.
/// 3. 현재 프로젝트 기준으로는 Title / Main 두 종류만 사용한다.
/// 
/// 주의:
/// - GameOver는 이번 구조에서 BGM이 아니라 SFX로 분리했다.
/// - 따라서 게임오버 순간 배경음 전환이 아니라, 효과음만 재생하도록 설계했다.
/// </summary>
public enum KSM_BgmType
{
    None = 0,
    Title,
    Main
}

/// <summary>
/// KSM_SfxType
/// 
/// 역할:
/// 1. 프로젝트에서 사용하는 모든 효과음을 enum으로 관리한다.
/// 2. 실제 mp3 파일명과 최대한 1:1로 대응되게 이름을 정리했다.
/// 3. 인스펙터에서 enum만 보고도 어떤 사운드를 넣어야 하는지 바로 알 수 있게 한다.
/// 
/// 현재 파일 기준 매핑:
/// - Click.mp3                 -> Click
/// - PickUp.mp3                -> PickUp
/// - PutDown.mp3               -> PutDown
/// - RotateBlock.mp3           -> RotateBlock
/// - Change Block.mp3          -> ChangeBlock
/// - Produce Block(long).mp3   -> ProduceBlock
/// - Expansion.mp3             -> Expansion
/// - Construction2.mp3         -> Construction
/// - MoneyExchange.mp3         -> MoneyExchange
/// - Goal2.mp3                 -> Goal
/// - GameOver.mp3              -> GameOver
/// </summary>
public enum KSM_SfxType
{
    Click,
    PickUp,
    PutDown,
    RotateBlock,
    ChangeBlock,
    ProduceBlock,
    Expansion,
    Construction,
    MoneyExchange,
    MoneyAll,
    Goal,
    GameOver
}

/// <summary>
/// KSM_BgmEntry
/// 
/// 역할:
/// 1. 특정 BGM enum과 실제 AudioClip을 연결한다.
/// 2. 기본 볼륨과 루프 여부를 함께 관리한다.
/// 3. 인스펙터에서 BGM 데이터 테이블처럼 사용된다.
/// </summary>
[Serializable]
public class KSM_BgmEntry
{
    [Tooltip("이 엔트리가 담당하는 BGM 타입.")]
    public KSM_BgmType bgmType = KSM_BgmType.Main;

    [Tooltip("실제로 재생할 BGM AudioClip.")]
    public AudioClip clip;

    [Tooltip("이 BGM의 기본 볼륨.")]
    [Range(0f, 1f)] public float defaultVolume = 1f;

    [Tooltip("이 BGM을 반복 재생할지 여부.")]
    public bool loop = true;
}

/// <summary>
/// KSM_SfxEntry
/// 
/// 역할:
/// 1. 특정 SFX enum과 실제 AudioClip을 연결한다.
/// 2. 기본 볼륨, 기본 피치, 랜덤 피치 범위를 관리한다.
/// 3. 필요하면 특정 효과음만 별도 MixerGroup으로 출력할 수 있게 한다.
/// </summary>
[Serializable]
public class KSM_SfxEntry
{
    [Tooltip("이 엔트리가 담당하는 SFX 타입.")]
    public KSM_SfxType sfxType = KSM_SfxType.Click;

    [Tooltip("실제로 재생할 효과음 AudioClip.")]
    public AudioClip clip;

    [Tooltip("이 효과음의 기본 볼륨.")]
    [Range(0f, 1f)] public float defaultVolume = 1f;

    [Tooltip("이 효과음의 기본 피치.")]
    [Range(0.5f, 1.5f)] public float defaultPitch = 1f;

    [Tooltip("재생할 때 피치를 랜덤으로 살짝 흔들 범위. 0이면 고정 피치.")]
    [Range(0f, 0.3f)] public float randomPitchRange = 0f;

    [Tooltip("비워두면 기본 SFX MixerGroup을 사용한다. 지정하면 해당 그룹으로 출력한다.")]
    public AudioMixerGroup outputGroupOverride;
}

/// <summary>
/// KSM_SoundManager
/// 
/// 역할:
/// 1. 게임 전체의 BGM / SFX 재생을 담당하는 중앙 사운드 매니저다.
/// 2. BGM은 2개 이상의 AudioSource를 사용해 크로스페이드할 수 있다.
/// 3. SFX는 여러 채널을 풀처럼 돌려 쓰는 방식으로 동시 재생을 처리한다.
/// 4. 고레벨 게임 이벤트(GameOver / 목표 전기 달성)를 직접 구독한다.
/// 5. 나머지 세부 타이밍(클릭, 드래그, 배치, 회전, 확장)은 외부 스크립트가 직접 호출하면 된다.
/// 6. MoneyExchange는 일반 PlayOneShot이 아니라 전용 AudioSource로 시작/중지를 제어한다.
/// 
/// 사용 방식:
/// - 씬에 하나만 두고 DontDestroyOnLoad로 유지한다.
/// - BGM / SFX 엔트리는 인스펙터에서 enum에 맞게 클립을 연결한다.
/// - 다른 스크립트에서는 KSM_SoundManager.Instance.PlaySfx(...) 를 호출해서 사용한다.
/// - 정산 UI는 KSM_SoundManager.Instance.StartMoneyExchangeSfx() / StopMoneyExchangeSfx()를 호출한다.
/// 
/// 주의:
/// - 현재 프로젝트 기준으로 BGM은 Title / Main만 사용한다.
/// - GameOver는 BGM이 아니라 SFX다.
/// - Goal SFX는 세션 전환이 아니라, 목표 전기량을 달성해 Skip 가능 상태가 되는 순간 재생한다.
/// </summary>
public class KSM_SoundManager : MonoBehaviour
{
    /// <summary>
    /// 전역 접근용 싱글톤 인스턴스다.
    /// </summary>
    public static KSM_SoundManager Instance { get; private set; }

    [Header("Mixer")]
    [Tooltip("마스터 오디오 믹서.")]
    [SerializeField] private AudioMixer mixer;

    [Tooltip("BGM용 기본 MixerGroup.")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;

    [Tooltip("SFX용 기본 MixerGroup.")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Tooltip("Mixer의 Master 노출 파라미터 이름.")]
    [SerializeField] private string masterVolumeParameter = "Master";

    [Tooltip("Mixer의 BGM 노출 파라미터 이름.")]
    [SerializeField] private string bgmVolumeParameter = "BGM";

    [Tooltip("Mixer의 SFX 노출 파라미터 이름.")]
    [SerializeField] private string sfxVolumeParameter = "SFX";

    [Header("BGM")]
    [Tooltip("BGM용 AudioSource 개수. 크로스페이드를 위해 2 이상 권장.")]
    [SerializeField, Min(2)] private int bgmChannelCount = 2;

    [Tooltip("BGM 전환 시 기본 페이드 시간.")]
    [SerializeField, Min(0f)] private float defaultBgmFadeSeconds = 1.2f;

    [Tooltip("BGM enum과 AudioClip 매핑 목록.")]
    [SerializeField] private List<KSM_BgmEntry> bgmEntries = new List<KSM_BgmEntry>();

    [Header("SFX")]
    [Tooltip("동시에 재생 가능한 SFX AudioSource 개수.")]
    [SerializeField, Min(1)] private int sfxChannelCount = 12;

    [Tooltip("SFX enum과 AudioClip 매핑 목록.")]
    [SerializeField] private List<KSM_SfxEntry> sfxEntries = new List<KSM_SfxEntry>();

    [Header("Money Exchange SFX")]
    [Tooltip("MoneyExchange 정지 시 짧게 줄이며 끊을 기본 시간.")]
    [SerializeField, Min(0f)] private float defaultMoneyExchangeFadeOutSeconds = 0.05f;

    [Header("Defaults")]
    [Tooltip("게임 시작 시 자동 재생할 기본 BGM.")]
    [SerializeField] private KSM_BgmType startBgm = KSM_BgmType.Main;

    [Tooltip("시작 시 마스터 볼륨.")]
    [Range(0f, 1f)][SerializeField] private float initialMasterVolume = 1f;

    [Tooltip("시작 시 BGM 볼륨.")]
    [Range(0f, 1f)][SerializeField] private float initialBgmVolume = 1f;

    [Tooltip("시작 시 SFX 볼륨.")]
    [Range(0f, 1f)][SerializeField] private float initialSfxVolume = 1f;

    [Header("Debug")]
    [Tooltip("주요 사운드 이벤트를 콘솔에 출력할지 여부.")]
    [SerializeField] private bool verboseLog = false;

    /// <summary>
    /// BGM enum -> 엔트리 lookup 캐시다.
    /// 인스펙터 리스트를 런타임에서 빠르게 찾기 위해 딕셔너리로 변환해 둔다.
    /// </summary>
    private readonly Dictionary<KSM_BgmType, KSM_BgmEntry> bgmEntryMap = new Dictionary<KSM_BgmType, KSM_BgmEntry>();

    /// <summary>
    /// SFX enum -> 엔트리 lookup 캐시다.
    /// </summary>
    private readonly Dictionary<KSM_SfxType, KSM_SfxEntry> sfxEntryMap = new Dictionary<KSM_SfxType, KSM_SfxEntry>();

    /// <summary>
    /// BGM 재생용 AudioSource 배열이다.
    /// 현재 트랙과 다음 트랙을 번갈아 사용하며 크로스페이드한다.
    /// </summary>
    private AudioSource[] bgmPlayers;

    /// <summary>
    /// SFX 재생용 AudioSource 배열이다.
    /// 여러 효과음이 동시에 나올 수 있도록 풀처럼 순환 사용한다.
    /// </summary>
    private AudioSource[] sfxPlayers;

    /// <summary>
    /// MoneyExchange 전용 재생기다.
    /// 일반 PlayOneShot과 분리해서 시작/정지를 직접 제어한다.
    /// </summary>
    private AudioSource moneyExchangePlayer;

    /// <summary>
    /// 현재 활성 BGM 플레이어 인덱스다.
    /// -1이면 현재 재생 중인 BGM이 없다는 뜻이다.
    /// </summary>
    private int activeBgmPlayerIndex = -1;

    /// <summary>
    /// 다음 SFX 재생에 사용할 플레이어 인덱스다.
    /// 라운드로빈 방식으로 한 칸씩 넘긴다.
    /// </summary>
    private int nextSfxPlayerIndex = 0;

    /// <summary>
    /// 현재 진행 중인 BGM 페이드 코루틴 참조다.
    /// 새 BGM 전환 시 이전 코루틴을 끊기 위해 저장한다.
    /// </summary>
    private Coroutine bgmFadeCoroutine;

    /// <summary>
    /// MoneyExchange 종료 시 짧은 페이드아웃에 사용하는 코루틴 참조다.
    /// </summary>
    private Coroutine moneyExchangeFadeCoroutine;

    /// <summary>
    /// 너무 자주 반복되면 거슬리는 SFX의 마지막 재생 시간을 저장한다.
    /// hover류 사운드를 스팸하지 않기 위해 사용한다.
    /// </summary>
    private readonly Dictionary<KSM_SfxType, float> lastSfxPlayTime = new Dictionary<KSM_SfxType, float>();

    /// <summary>
    /// 게임 이벤트를 현재 구독 중인지 저장한다.
    /// 중복 구독 방지용이다.
    /// </summary>
    private bool isGameplayEventsBound = false;

    /// <summary>
    /// 싱글톤 생성과 런타임 오디오 준비를 담당한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildEntryLookupTables();
        InitializeAudioSources();
    }

    /// <summary>
    /// 시작 시 초기 볼륨을 Mixer에 적용하고,
    /// startBgm 값이 None이 아니면 자동 재생한다.
    /// </summary>
    private void Start()
    {
        SetMasterVolume(initialMasterVolume);
        SetBgmVolume(initialBgmVolume);
        SetSfxVolume(initialSfxVolume);

        if (startBgm != KSM_BgmType.None)
        {
            PlayBgm(startBgm, 0f);
        }
    }

    /// <summary>
    /// 오브젝트 활성화 시 고레벨 게임 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        BindGameplayEvents();
    }

    /// <summary>
    /// 오브젝트 비활성화 시 구독했던 이벤트를 해제한다.
    /// 또한 MoneyExchange 전용 채널이 남아 있으면 즉시 정지한다.
    /// </summary>
    private void OnDisable()
    {
        UnbindGameplayEvents();
        StopMoneyExchangeSfx(0f);
    }

    /// <summary>
    /// 파괴 시 이벤트와 싱글톤 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            StopMoneyExchangeSfx(0f);
            UnbindGameplayEvents();
            Instance = null;
        }
    }

    /// <summary>
    /// 인스펙터에서 입력한 BGM / SFX 리스트를 딕셔너리 캐시로 변환한다.
    /// 
    /// 주의:
    /// - 동일 enum이 중복 등록되면 뒤쪽 항목이 앞쪽 항목을 덮어쓴다.
    /// - clip이 비어 있는 항목은 캐시에 넣지 않는다.
    /// </summary>
    private void BuildEntryLookupTables()
    {
        bgmEntryMap.Clear();
        sfxEntryMap.Clear();

        for (int i = 0; i < bgmEntries.Count; i++)
        {
            KSM_BgmEntry entry = bgmEntries[i];
            if (entry == null) continue;
            if (entry.clip == null) continue;

            bgmEntryMap[entry.bgmType] = entry;
        }

        for (int i = 0; i < sfxEntries.Count; i++)
        {
            KSM_SfxEntry entry = sfxEntries[i];
            if (entry == null) continue;
            if (entry.clip == null) continue;

            sfxEntryMap[entry.sfxType] = entry;
        }
    }

    /// <summary>
    /// BGM / SFX용 AudioSource들을 생성하고 기본 속성을 세팅한다.
    /// 
    /// BGM:
    /// - loop 기본 활성
    /// - priority 높음
    /// - 2D 사운드
    /// 
    /// SFX:
    /// - loop 비활성
    /// - priority 일반
    /// - 2D 사운드
    /// 
    /// MoneyExchange:
    /// - loop 활성
    /// - 정산 UI가 구간별 시작/정지를 직접 제어
    /// </summary>
    private void InitializeAudioSources()
    {
        bgmChannelCount = Mathf.Max(2, bgmChannelCount);
        sfxChannelCount = Mathf.Max(1, sfxChannelCount);

        GameObject bgmRoot = new GameObject("KSM_BGM_Channels");
        bgmRoot.transform.SetParent(transform, false);

        bgmPlayers = new AudioSource[bgmChannelCount];
        for (int i = 0; i < bgmPlayers.Length; i++)
        {
            bgmPlayers[i] = bgmRoot.AddComponent<AudioSource>();
            bgmPlayers[i].playOnAwake = false;
            bgmPlayers[i].loop = true;
            bgmPlayers[i].priority = 0;
            bgmPlayers[i].spatialBlend = 0f;
            bgmPlayers[i].outputAudioMixerGroup = bgmMixerGroup;
        }

        GameObject sfxRoot = new GameObject("KSM_SFX_Channels");
        sfxRoot.transform.SetParent(transform, false);

        sfxPlayers = new AudioSource[sfxChannelCount];
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i] = sfxRoot.AddComponent<AudioSource>();
            sfxPlayers[i].playOnAwake = false;
            sfxPlayers[i].loop = false;
            sfxPlayers[i].priority = 128;
            sfxPlayers[i].spatialBlend = 0f;
            sfxPlayers[i].outputAudioMixerGroup = sfxMixerGroup;
        }

        GameObject moneyExchangeRoot = new GameObject("KSM_MoneyExchange_Channel");
        moneyExchangeRoot.transform.SetParent(transform, false);

        moneyExchangePlayer = moneyExchangeRoot.AddComponent<AudioSource>();
        moneyExchangePlayer.playOnAwake = false;
        moneyExchangePlayer.loop = true;
        moneyExchangePlayer.priority = 64;
        moneyExchangePlayer.spatialBlend = 0f;
        moneyExchangePlayer.outputAudioMixerGroup = sfxMixerGroup;
    }

    /// <summary>
    /// ResourceManager 같은 고레벨 이벤트를 구독한다.
    /// 
    /// 여기서 처리하는 것:
    /// - 게임오버
    /// - 목표 전기량 달성으로 Skip 가능 상태가 된 순간
    /// 
    /// 여기서 처리하지 않는 것:
    /// - 클릭
    /// - 드래그
    /// - 회전
    /// - 블럭 배치
    /// - 맵 확장 hover
    /// - MoneyExchange 구간 시작/종료
    /// 
    /// 그런 입력성/연출성 사운드는 실제 스크립트가 직접 호출하는 쪽이 더 정확하다.
    /// </summary>
    private void BindGameplayEvents()
    {
        if (isGameplayEventsBound) return;

        ResourceManager.OnGameOver += HandleGameOver;
        ResourceManager.OnSkipAvailability += HandleSkipAvailabilityChanged;

        isGameplayEventsBound = true;
    }

    /// <summary>
    /// 구독했던 게임 이벤트를 해제한다.
    /// </summary>
    private void UnbindGameplayEvents()
    {
        if (!isGameplayEventsBound) return;

        ResourceManager.OnGameOver -= HandleGameOver;
        ResourceManager.OnSkipAvailability -= HandleSkipAvailabilityChanged;

        isGameplayEventsBound = false;
    }

    /// <summary>
    /// 게임오버 이벤트 수신 시 GameOver SFX를 재생한다.
    /// </summary>
    private void HandleGameOver()
    {
        PlaySfx(KSM_SfxType.GameOver);
    }

    /// <summary>
    /// Skip 가능 상태 변경 이벤트를 수신한다.
    /// canSkip == true 인 순간은 목표 발전량 달성 순간이므로 Goal SFX를 재생한다.
    /// </summary>
    /// <param name="canSkip">현재 Skip 가능 여부</param>
    private void HandleSkipAvailabilityChanged(bool canSkip)
    {
        if (!canSkip)
        {
            return;
        }

        PlaySfx(KSM_SfxType.Goal);
    }

    /// <summary>
    /// 원하는 BGM을 재생한다.
    /// </summary>
    /// <param name="bgmType">재생할 BGM 타입</param>
    /// <param name="fadeSeconds">페이드 시간. 음수면 기본값 사용</param>
    public void PlayBgm(KSM_BgmType bgmType, float fadeSeconds = -1f)
    {
        if (bgmType == KSM_BgmType.None)
        {
            StopBgm(fadeSeconds);
            return;
        }

        if (!bgmEntryMap.TryGetValue(bgmType, out KSM_BgmEntry targetEntry))
        {
            if (verboseLog) Debug.LogWarning($"[KSM_SoundManager] 등록되지 않은 BGM 타입: {bgmType}");
            return;
        }

        float resolvedFade = fadeSeconds < 0f ? defaultBgmFadeSeconds : fadeSeconds;

        AudioSource current = GetCurrentBgmPlayer();
        if (current != null && current.clip == targetEntry.clip)
        {
            return;
        }

        int nextIndex = activeBgmPlayerIndex < 0 ? 0 : (activeBgmPlayerIndex + 1) % bgmPlayers.Length;
        AudioSource next = bgmPlayers[nextIndex];

        next.clip = targetEntry.clip;
        next.loop = targetEntry.loop;
        next.outputAudioMixerGroup = bgmMixerGroup;

        if (current == null || resolvedFade <= 0f)
        {
            if (current != null)
            {
                current.Stop();
                current.volume = 0f;
            }

            next.volume = targetEntry.defaultVolume;
            next.Play();
            activeBgmPlayerIndex = nextIndex;
            return;
        }

        next.volume = 0f;
        next.Play();

        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
        }

        bgmFadeCoroutine = StartCoroutine(CrossFadeBgmCoroutine(current, next, targetEntry.defaultVolume, resolvedFade));
        activeBgmPlayerIndex = nextIndex;
    }

    public void PlayMoneyAll()
    {
        PlaySfx(KSM_SfxType.MoneyAll);
    }

    /// <summary>
    /// 현재 재생 중인 BGM을 정지한다.
    /// </summary>
    /// <param name="fadeSeconds">페이드 시간. 음수면 기본값 사용</param>
    public void StopBgm(float fadeSeconds = -1f)
    {
        AudioSource current = GetCurrentBgmPlayer();
        if (current == null) return;

        float resolvedFade = fadeSeconds < 0f ? defaultBgmFadeSeconds : fadeSeconds;

        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }

        if (resolvedFade <= 0f)
        {
            current.Stop();
            current.volume = 0f;
            activeBgmPlayerIndex = -1;
            return;
        }

        bgmFadeCoroutine = StartCoroutine(FadeOutBgmCoroutine(current, resolvedFade));
        activeBgmPlayerIndex = -1;
    }

    /// <summary>
    /// 일반 SFX를 즉시 재생한다.
    /// 일반적인 짧은 효과음은 모두 PlayOneShot으로 처리한다.
    /// </summary>
    /// <param name="sfxType">재생할 SFX 타입</param>
    /// <param name="volumeScale">추가 볼륨 배율</param>
    /// <returns>실제 재생 성공 여부</returns>
    public bool PlaySfx(KSM_SfxType sfxType, float volumeScale = 1f)
    {
        if (!sfxEntryMap.TryGetValue(sfxType, out KSM_SfxEntry entry))
        {
            if (verboseLog) Debug.LogWarning($"[KSM_SoundManager] 등록되지 않은 SFX 타입: {sfxType}");
            return false;
        }

        if (entry.clip == null || sfxPlayers == null || sfxPlayers.Length == 0)
        {
            return false;
        }

        AudioSource player = GetNextSfxPlayer();
        if (player == null)
        {
            return false;
        }

        player.outputAudioMixerGroup = entry.outputGroupOverride != null ? entry.outputGroupOverride : sfxMixerGroup;

        float randomPitchOffset = entry.randomPitchRange > 0f
            ? UnityEngine.Random.Range(-entry.randomPitchRange, entry.randomPitchRange)
            : 0f;

        player.pitch = Mathf.Clamp(entry.defaultPitch + randomPitchOffset, 0.25f, 3f);

        float finalVolume = Mathf.Clamp01(entry.defaultVolume * volumeScale);
        player.PlayOneShot(entry.clip, finalVolume);

        if (verboseLog)
        {
            Debug.Log($"[KSM_SoundManager] SFX 재생: {sfxType}");
        }

        return true;
    }

    /// <summary>
    /// MoneyExchange SFX를 시작한다.
    /// 
    /// 역할:
    /// 1. 일반 PlayOneShot이 아니라 전용 AudioSource로 재생한다.
    /// 2. 이미 재생 중이면 재시작하지 않는다.
    /// 3. 정산 UI에서 각 기업 구간 시작 시 호출한다.
    /// </summary>
    /// <param name="volumeScale">추가 볼륨 배율</param>
    /// <returns>실제 재생 성공 여부</returns>
    public bool StartMoneyExchangeSfx(float volumeScale = 1f)
    {
        if (!sfxEntryMap.TryGetValue(KSM_SfxType.MoneyExchange, out KSM_SfxEntry entry))
        {
            if (verboseLog) Debug.LogWarning("[KSM_SoundManager] MoneyExchange SFX가 등록되지 않았습니다.");
            return false;
        }

        if (entry.clip == null || moneyExchangePlayer == null)
        {
            return false;
        }

        if (moneyExchangeFadeCoroutine != null)
        {
            StopCoroutine(moneyExchangeFadeCoroutine);
            moneyExchangeFadeCoroutine = null;
        }

        moneyExchangePlayer.outputAudioMixerGroup =
            entry.outputGroupOverride != null ? entry.outputGroupOverride : sfxMixerGroup;

        moneyExchangePlayer.clip = entry.clip;
        moneyExchangePlayer.pitch = Mathf.Clamp(entry.defaultPitch, 0.25f, 3f);
        moneyExchangePlayer.volume = Mathf.Clamp01(entry.defaultVolume * volumeScale);

        if (!moneyExchangePlayer.isPlaying)
        {
            moneyExchangePlayer.Play();

            if (verboseLog)
            {
                Debug.Log("[KSM_SoundManager] MoneyExchange 시작");
            }
        }

        return true;
    }

    /// <summary>
    /// MoneyExchange SFX를 정지한다.
    /// 
    /// 역할:
    /// 1. 각 기업 정산이 0이 되는 순간 호출한다.
    /// 2. 너무 딱 끊기지 않게 짧은 페이드아웃도 지원한다.
    /// </summary>
    /// <param name="fadeSeconds">정지 전 짧은 페이드 시간. 음수면 기본값 사용</param>
    public void StopMoneyExchangeSfx(float fadeSeconds = -1f)
    {
        if (moneyExchangePlayer == null)
        {
            return;
        }

        float resolvedFade = fadeSeconds < 0f ? defaultMoneyExchangeFadeOutSeconds : fadeSeconds;

        if (moneyExchangeFadeCoroutine != null)
        {
            StopCoroutine(moneyExchangeFadeCoroutine);
            moneyExchangeFadeCoroutine = null;
        }

        if (!moneyExchangePlayer.isPlaying)
        {
            moneyExchangePlayer.Stop();
            moneyExchangePlayer.clip = null;
            return;
        }

        if (resolvedFade <= 0f)
        {
            float restoreVolume = moneyExchangePlayer.volume;
            moneyExchangePlayer.Stop();
            moneyExchangePlayer.clip = null;
            moneyExchangePlayer.volume = restoreVolume;
            return;
        }

        moneyExchangeFadeCoroutine = StartCoroutine(FadeOutMoneyExchangeCoroutine(resolvedFade));
    }

    /// <summary>
    /// 동일한 SFX가 너무 자주 반복 재생되지 않도록 쿨다운을 둔 재생 함수다.
    /// </summary>
    /// <param name="sfxType">재생할 SFX 타입</param>
    /// <param name="cooldownSeconds">최소 재생 간격</param>
    /// <param name="volumeScale">추가 볼륨 배율</param>
    /// <returns>실제로 재생되었는지 여부</returns>
    public bool PlaySfxThrottled(KSM_SfxType sfxType, float cooldownSeconds, float volumeScale = 1f)
    {
        float now = Time.unscaledTime;

        if (lastSfxPlayTime.TryGetValue(sfxType, out float lastTime))
        {
            if (now - lastTime < cooldownSeconds)
            {
                return false;
            }
        }

        bool played = PlaySfx(sfxType, volumeScale);
        if (played)
        {
            lastSfxPlayTime[sfxType] = now;
        }

        return played;
    }

    /// <summary>
    /// UI 버튼 클릭용 편의 함수다.
    /// </summary>
    public void PlayUiClick()
    {
        PlaySfx(KSM_SfxType.Click);
    }

    /// <summary>
    /// 블럭 잡기용 편의 함수다.
    /// </summary>
    public void PlayPickUp()
    {
        PlaySfx(KSM_SfxType.PickUp);
    }

    /// <summary>
    /// 블럭 놓기 성공용 편의 함수다.
    /// 현재는 유지하지만 실사용 여부는 외부 스크립트 정책에 따른다.
    /// </summary>
    public void PlayPutDown()
    {
        PlaySfx(KSM_SfxType.PutDown);
    }

    /// <summary>
    /// 블럭 회전용 편의 함수다.
    /// </summary>
    public void PlayRotateBlock()
    {
        PlaySfx(KSM_SfxType.RotateBlock);
    }

    /// <summary>
    /// 맵 확장 성공용 편의 함수다.
    /// </summary>
    public void PlayExpansion()
    {
        PlaySfx(KSM_SfxType.Expansion);
    }

    /// <summary>
    /// 마스터 볼륨을 0~1 값으로 받아 Mixer에 dB 값으로 반영한다.
    /// </summary>
    /// <param name="value">0~1 범위의 선형 볼륨 값</param>
    public void SetMasterVolume(float value)
    {
        if (mixer == null) return;
        mixer.SetFloat(masterVolumeParameter, ConvertLinearToDb(value));
    }

    /// <summary>
    /// BGM 볼륨을 0~1 값으로 받아 Mixer에 dB 값으로 반영한다.
    /// </summary>
    /// <param name="value">0~1 범위의 선형 볼륨 값</param>
    public void SetBgmVolume(float value)
    {
        if (mixer == null) return;
        mixer.SetFloat(bgmVolumeParameter, ConvertLinearToDb(value));
    }

    /// <summary>
    /// SFX 볼륨을 0~1 값으로 받아 Mixer에 dB 값으로 반영한다.
    /// </summary>
    /// <param name="value">0~1 범위의 선형 볼륨 값</param>
    public void SetSfxVolume(float value)
    {
        if (mixer == null) return;
        mixer.SetFloat(sfxVolumeParameter, ConvertLinearToDb(value));
    }

    /// <summary>
    /// 현재 활성 BGM 플레이어를 반환한다.
    /// </summary>
    /// <returns>현재 재생 중인 BGM AudioSource, 없으면 null</returns>
    private AudioSource GetCurrentBgmPlayer()
    {
        if (bgmPlayers == null || bgmPlayers.Length == 0) return null;
        if (activeBgmPlayerIndex < 0 || activeBgmPlayerIndex >= bgmPlayers.Length) return null;
        return bgmPlayers[activeBgmPlayerIndex];
    }

    /// <summary>
    /// 다음 SFX 재생에 사용할 AudioSource를 반환한다.
    /// 라운드로빈 방식으로 한 칸씩 순환한다.
    /// </summary>
    /// <returns>선택된 SFX AudioSource</returns>
    private AudioSource GetNextSfxPlayer()
    {
        if (sfxPlayers == null || sfxPlayers.Length == 0) return null;

        AudioSource selected = sfxPlayers[nextSfxPlayerIndex];
        nextSfxPlayerIndex = (nextSfxPlayerIndex + 1) % sfxPlayers.Length;
        return selected;
    }

    /// <summary>
    /// 두 BGM 플레이어를 부드럽게 크로스페이드하는 코루틴이다.
    /// </summary>
    private IEnumerator CrossFadeBgmCoroutine(AudioSource from, AudioSource to, float targetVolume, float duration)
    {
        float elapsed = 0f;
        float fromStartVolume = from != null ? from.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (from != null)
            {
                from.volume = Mathf.Lerp(fromStartVolume, 0f, t);
            }

            if (to != null)
            {
                to.volume = Mathf.Lerp(0f, targetVolume, t);
            }

            yield return null;
        }

        if (from != null)
        {
            from.Stop();
            from.volume = 0f;
        }

        if (to != null)
        {
            to.volume = targetVolume;
        }

        bgmFadeCoroutine = null;
    }

    /// <summary>
    /// 현재 BGM을 서서히 줄이면서 정지하는 코루틴이다.
    /// </summary>
    private IEnumerator FadeOutBgmCoroutine(AudioSource target, float duration)
    {
        if (target == null)
        {
            bgmFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        float startVolume = target.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            target.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        target.Stop();
        target.volume = 0f;
        bgmFadeCoroutine = null;
    }

    /// <summary>
    /// MoneyExchange 전용 AudioSource를 짧게 줄이면서 정지한다.
    /// </summary>
    private IEnumerator FadeOutMoneyExchangeCoroutine(float duration)
    {
        if (moneyExchangePlayer == null)
        {
            moneyExchangeFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        float startVolume = moneyExchangePlayer.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            moneyExchangePlayer.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        moneyExchangePlayer.Stop();
        moneyExchangePlayer.clip = null;
        moneyExchangePlayer.volume = startVolume;
        moneyExchangeFadeCoroutine = null;

        if (verboseLog)
        {
            Debug.Log("[KSM_SoundManager] MoneyExchange 정지");
        }
    }

    /// <summary>
    /// 0~1 선형 볼륨 값을 오디오 믹서용 dB 값으로 변환한다.
    /// </summary>
    private float ConvertLinearToDb(float value)
    {
        return Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
    }
}