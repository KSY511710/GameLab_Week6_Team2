using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using Special.Data;
using Special.Integration;

/// <summary>
/// KSM_BgmType
/// 
/// 역할:
/// 1. 프로젝트에서 사용하는 BGM 종류를 이름으로 관리한다.
/// 2. 배열 인덱스가 아니라 "이름" 기준으로 BGM을 찾게 해준다.
/// 3. 나중에 씬/상태가 늘어나도 enum만 추가하면 확장 가능하다.
/// </summary>
public enum KSM_BgmType
{
    None = 0,
    Main,
    Build,
    Result,
    GameOver
}

/// <summary>
/// KSM_SfxType
/// 
/// 역할:
/// 1. 프로젝트에서 사용하는 효과음 종류를 이름으로 관리한다.
/// 2. UI / 배치 / 확장 / 연출 / 게임 상태 변화용 사운드를 분리한다.
/// 3. 현재 필요한 핵심 항목 위주로 넣었고, 추후 쉽게 추가 가능하다.
/// </summary>
public enum KSM_SfxType
{
    UIHover,
    UIClick,

    BlockDragStart,
    BlockRotate,
    BlockPlaceSuccess,
    BlockPlaceFail,

    ExpandHover,
    ExpandSuccess,
    ExpandFail,

    DrawBasic,
    DrawTheme,
    DrawSpecial,

    GroupCompletePulse,
    GroupCompleteFinal,

    DayEndTick,
    DayEndTotal,

    SpecialPlaceStart,
    SpecialPlaceStep,
    SpecialPlaceFinal,

    SkipSuccess,
    SessionAdvance,
    GameOver
}

/// <summary>
/// KSM_BgmEntry
/// 
/// 역할:
/// 1. 특정 BGM 타입과 실제 AudioClip을 연결한다.
/// 2. 루프 여부와 기본 볼륨을 함께 관리한다.
/// 3. 인스펙터에서 안전하게 매핑하기 위한 데이터 클래스다.
/// </summary>
[Serializable]
public class KSM_BgmEntry
{
    [Tooltip("이 엔트리가 담당하는 BGM 타입.")]
    public KSM_BgmType bgmType = KSM_BgmType.Main;

    [Tooltip("실제로 재생할 BGM 클립.")]
    public AudioClip clip;

    [Tooltip("이 BGM의 기본 볼륨 배율.")]
    [Range(0f, 1f)] public float defaultVolume = 1f;

    [Tooltip("반복 재생 여부.")]
    public bool loop = true;
}

/// <summary>
/// KSM_SfxEntry
/// 
/// 역할:
/// 1. 특정 SFX 타입과 실제 AudioClip을 연결한다.
/// 2. 기본 볼륨 / 기본 피치 / 랜덤 피치 범위를 관리한다.
/// 3. 필요하면 개별 MixerGroup override도 가능하게 한다.
/// </summary>
[Serializable]
public class KSM_SfxEntry
{
    [Tooltip("이 엔트리가 담당하는 SFX 타입.")]
    public KSM_SfxType sfxType = KSM_SfxType.UIClick;

    [Tooltip("실제로 재생할 효과음 클립.")]
    public AudioClip clip;

    [Tooltip("이 효과음의 기본 볼륨 배율.")]
    [Range(0f, 1f)] public float defaultVolume = 1f;

    [Tooltip("이 효과음의 기본 피치.")]
    [Range(0.5f, 1.5f)] public float defaultPitch = 1f;

    [Tooltip("피치에 랜덤 흔들림을 줄 범위. 0이면 고정 피치.")]
    [Range(0f, 0.3f)] public float randomPitchRange = 0f;

    [Tooltip("비워두면 기본 SFX 그룹 사용. 지정하면 이 그룹으로 출력.")]
    public AudioMixerGroup outputGroupOverride;
}

/// <summary>
/// KSM_SoundManager
/// 
/// 역할:
/// 1. 게임 전체의 BGM / SFX 재생을 담당하는 중앙 사운드 매니저다.
/// 2. BGM은 2채널 크로스페이드, SFX는 풀 기반 재생으로 관리한다.
/// 3. ResourceManager / SpecialGachaController 등 일부 고레벨 이벤트를 직접 구독한다.
/// 4. 나머지 입력/연출 타이밍은 외부 스크립트가 명시적으로 호출하는 구조를 권장한다.
/// 
/// 설계 의도:
/// - "사운드 데이터 관리"와 "어느 순간에 사운드를 틀지"를 완전히 한곳에 몰아넣지 않는다.
/// - 매니저는 재생기 역할에 집중하고,
///   실제 타이밍은 DraggableBlock / KSM_MapExpandButton / Sequencer가 결정한다.
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

    [Tooltip("BGM용 기본 믹서 그룹.")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;

    [Tooltip("SFX용 기본 믹서 그룹.")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Tooltip("Mixer의 Master 노출 파라미터 이름.")]
    [SerializeField] private string masterVolumeParameter = "Master";

    [Tooltip("Mixer의 BGM 노출 파라미터 이름.")]
    [SerializeField] private string bgmVolumeParameter = "BGM";

    [Tooltip("Mixer의 SFX 노출 파라미터 이름.")]
    [SerializeField] private string sfxVolumeParameter = "SFX";

    [Header("BGM")]
    [Tooltip("BGM 재생용 AudioSource 개수. 크로스페이드 때문에 2 권장.")]
    [SerializeField, Min(2)] private int bgmChannelCount = 2;

    [Tooltip("BGM 전환 시 기본 페이드 시간.")]
    [SerializeField, Min(0f)] private float defaultBgmFadeSeconds = 1.2f;

    [Tooltip("BGM 타입과 클립 매핑 목록.")]
    [SerializeField] private List<KSM_BgmEntry> bgmEntries = new List<KSM_BgmEntry>();

    [Header("SFX")]
    [Tooltip("동시에 재생 가능한 SFX 채널 수.")]
    [SerializeField, Min(1)] private int sfxChannelCount = 12;

    [Tooltip("SFX 타입과 클립 매핑 목록.")]
    [SerializeField] private List<KSM_SfxEntry> sfxEntries = new List<KSM_SfxEntry>();

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
    [Tooltip("중요한 사운드 이벤트를 콘솔에 출력할지 여부.")]
    [SerializeField] private bool verboseLog = false;

    /// <summary>
    /// 런타임 BGM lookup 캐시다.
    /// key = BGM 타입, value = 엔트리.
    /// </summary>
    private readonly Dictionary<KSM_BgmType, KSM_BgmEntry> bgmEntryMap = new Dictionary<KSM_BgmType, KSM_BgmEntry>();

    /// <summary>
    /// 런타임 SFX lookup 캐시다.
    /// key = SFX 타입, value = 엔트리.
    /// </summary>
    private readonly Dictionary<KSM_SfxType, KSM_SfxEntry> sfxEntryMap = new Dictionary<KSM_SfxType, KSM_SfxEntry>();

    /// <summary>
    /// BGM 재생용 AudioSource 배열이다.
    /// 2개를 번갈아 쓰며 크로스페이드한다.
    /// </summary>
    private AudioSource[] bgmPlayers;

    /// <summary>
    /// SFX 재생용 AudioSource 배열이다.
    /// 라운드로빈 방식으로 돌아가며 사용한다.
    /// </summary>
    private AudioSource[] sfxPlayers;

    /// <summary>
    /// 현재 활성 BGM 플레이어 인덱스다.
    /// -1이면 아직 BGM이 재생되지 않았다는 뜻이다.
    /// </summary>
    private int activeBgmPlayerIndex = -1;

    /// <summary>
    /// 다음 SFX에 사용할 플레이어 인덱스다.
    /// </summary>
    private int nextSfxPlayerIndex = 0;

    /// <summary>
    /// 현재 진행 중인 BGM 페이드 코루틴이다.
    /// 새 BGM 전환 시 이전 코루틴을 끊기 위해 저장한다.
    /// </summary>
    private Coroutine bgmFadeCoroutine;

    /// <summary>
    /// 같은 SFX가 너무 자주 반복 재생되지 않도록 마지막 재생 시각을 기록한다.
    /// hover류 사운드 스팸 방지에 사용한다.
    /// </summary>
    private readonly Dictionary<KSM_SfxType, float> lastSfxPlayTime = new Dictionary<KSM_SfxType, float>();

    /// <summary>
    /// 게임 이벤트 구독 여부를 저장한다.
    /// 중복 구독 방지용이다.
    /// </summary>
    private bool isGameplayEventsBound = false;

    /// <summary>
    /// 싱글톤 생성과 오디오 초기화를 담당한다.
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
    /// 시작 시 초기 볼륨을 적용하고 기본 BGM을 재생한다.
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
    /// 활성화 시 게임 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        BindGameplayEvents();
    }

    /// <summary>
    /// 비활성화 시 게임 이벤트를 해제한다.
    /// </summary>
    private void OnDisable()
    {
        UnbindGameplayEvents();
    }

    /// <summary>
    /// 파괴 시 싱글톤 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnbindGameplayEvents();
            Instance = null;
        }
    }

    /// <summary>
    /// 인스펙터에 입력한 BGM/SFX 엔트리를 딕셔너리로 캐싱한다.
    /// 중복 키가 있으면 뒤의 항목이 앞을 덮어쓴다.
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
    /// BGM/SFX용 AudioSource들을 생성하고 기본 설정을 적용한다.
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
    }

    /// <summary>
    /// ResourceManager / SpecialGachaController 같은 고레벨 이벤트를 구독한다.
    /// 
    /// 주의:
    /// - hover / 클릭 / 드래그 같은 입력성 사운드는 여기서 듣지 않는다.
    /// - 그런 것은 실제 입력 스크립트가 직접 호출하는 편이 더 정확하다.
    /// </summary>
    private void BindGameplayEvents()
    {
        if (isGameplayEventsBound) return;

        ResourceManager.OnGameOver += HandleGameOver;
        ResourceManager.OnSessionAdvanced += HandleSessionAdvanced;
        SpecialGachaController.OnSpecialBlockDrawn += HandleSpecialBlockDrawn;

        isGameplayEventsBound = true;
    }

    /// <summary>
    /// 구독했던 게임 이벤트를 해제한다.
    /// </summary>
    private void UnbindGameplayEvents()
    {
        if (!isGameplayEventsBound) return;

        ResourceManager.OnGameOver -= HandleGameOver;
        ResourceManager.OnSessionAdvanced -= HandleSessionAdvanced;
        SpecialGachaController.OnSpecialBlockDrawn -= HandleSpecialBlockDrawn;

        isGameplayEventsBound = false;
    }

    /// <summary>
    /// 게임 오버 이벤트 수신 시 게임오버 SFX/BGM을 재생한다.
    /// </summary>
    private void HandleGameOver()
    {
        PlaySfx(KSM_SfxType.GameOver);
        PlayBgm(KSM_BgmType.GameOver, 0.6f);
    }

    /// <summary>
    /// 세션 진입 이벤트 수신 시 세션 전환 효과음을 재생한다.
    /// </summary>
    /// <param name="sessionIndex">새 세션 인덱스</param>
    private void HandleSessionAdvanced(int sessionIndex)
    {
        PlaySfx(KSM_SfxType.SessionAdvance);
    }

    /// <summary>
    /// 특수 블럭 뽑기 성공 이벤트 수신 시 전용 효과음을 재생한다.
    /// </summary>
    /// <param name="definition">뽑힌 특수 블럭 정의</param>
    private void HandleSpecialBlockDrawn(SpecialBlockDefinition definition)
    {
        PlaySfx(KSM_SfxType.DrawSpecial);
    }

    /// <summary>
    /// 원하는 BGM을 재생한다.
    /// 현재 BGM과 같으면 무시한다.
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
    /// </summary>
    /// <param name="sfxType">재생할 SFX 타입</param>
    /// <param name="volumeScale">추가 볼륨 배율</param>
    /// <returns>재생 성공 여부</returns>
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
    /// 너무 자주 반복되면 거슬리는 효과음을 쿨다운을 두고 재생한다.
    /// hover류 사운드에 쓰기 좋다.
    /// </summary>
    /// <param name="sfxType">재생할 SFX 타입</param>
    /// <param name="cooldownSeconds">최소 재생 간격</param>
    /// <param name="volumeScale">추가 볼륨 배율</param>
    /// <returns>실제 재생되었는지 여부</returns>
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
    /// Button OnClick에서 직접 연결하기 쉽게 만들었다.
    /// </summary>
    public void PlayUiClick()
    {
        PlaySfx(KSM_SfxType.UIClick);
    }

    /// <summary>
    /// UI hover용 편의 함수다.
    /// 너무 자주 울리지 않도록 내부적으로 쿨다운을 건다.
    /// </summary>
    public void PlayUiHover()
    {
        PlaySfxThrottled(KSM_SfxType.UIHover, 0.04f);
    }

    /// <summary>
    /// 마스터 볼륨을 0~1 값으로 받아 믹서에 dB로 적용한다.
    /// </summary>
    /// <param name="value">0~1 범위 볼륨 값</param>
    public void SetMasterVolume(float value)
    {
        if (mixer == null) return;
        mixer.SetFloat(masterVolumeParameter, ConvertLinearToDb(value));
    }

    /// <summary>
    /// BGM 볼륨을 0~1 값으로 받아 믹서에 dB로 적용한다.
    /// </summary>
    /// <param name="value">0~1 범위 볼륨 값</param>
    public void SetBgmVolume(float value)
    {
        if (mixer == null) return;
        mixer.SetFloat(bgmVolumeParameter, ConvertLinearToDb(value));
    }

    /// <summary>
    /// SFX 볼륨을 0~1 값으로 받아 믹서에 dB로 적용한다.
    /// </summary>
    /// <param name="value">0~1 범위 볼륨 값</param>
    public void SetSfxVolume(float value)
    {
        if (mixer == null) return;
        mixer.SetFloat(sfxVolumeParameter, ConvertLinearToDb(value));
    }

    /// <summary>
    /// 현재 활성 BGM 플레이어를 반환한다.
    /// </summary>
    /// <returns>현재 BGM AudioSource 또는 null</returns>
    private AudioSource GetCurrentBgmPlayer()
    {
        if (bgmPlayers == null || bgmPlayers.Length == 0) return null;
        if (activeBgmPlayerIndex < 0 || activeBgmPlayerIndex >= bgmPlayers.Length) return null;
        return bgmPlayers[activeBgmPlayerIndex];
    }

    /// <summary>
    /// 다음 SFX 재생에 사용할 AudioSource를 반환한다.
    /// 채널을 라운드로빈 방식으로 순환한다.
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
    /// 두 BGM 플레이어 사이를 부드럽게 크로스페이드한다.
    /// </summary>
    /// <param name="from">기존 BGM 플레이어</param>
    /// <param name="to">새 BGM 플레이어</param>
    /// <param name="targetVolume">새 BGM 목표 볼륨</param>
    /// <param name="duration">페이드 길이</param>
    /// <returns>코루틴</returns>
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
    /// 현재 BGM을 서서히 줄이며 정지한다.
    /// </summary>
    /// <param name="target">정지할 BGM 플레이어</param>
    /// <param name="duration">페이드 길이</param>
    /// <returns>코루틴</returns>
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
    /// 0~1 선형 볼륨 값을 오디오 믹서용 dB 값으로 변환한다.
    /// </summary>
    /// <param name="value">0~1 선형 볼륨</param>
    /// <returns>dB 값</returns>
    private float ConvertLinearToDb(float value)
    {
        return Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
    }
}