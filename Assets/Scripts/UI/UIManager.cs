using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using System;
using UnityEngine.Networking;

//Resolved tier for a single win. None => the win is below the floor and no banner plays.
internal enum WinTier { None, Text, Big, Huge, Mega }

//One art beat inside a tier's count-up. A Mega escalates BIG -> HUGE -> MEGA through three of these.
//Each cue owns its own Image object, so the words can carry different rects and cross over each other.
[Serializable]
internal struct BannerCue
{
  public Image image;                       //dedicated BIG / HUGE / MEGA object
  [Range(0f, 1f)] public float threshold;   //t in the count-up at which this word takes over
  public Sound sound;                       //Sound.None => silent
}

[Serializable]
internal struct WinTierSpec
{
  public WinTier tier;
  public string label;            //inspector readability, e.g. "Huge Win"
  public float betMultiplier;     //entry threshold: win >= bet * this
  public float lerpDuration;      //count-up length, seconds
  public Sound openSound;         //played as the banner opens; the Text tier's only sound
  public BannerCue[] cues;        //ascending by threshold. EMPTY => text-only: no slide, art, or fountain
}

public class UIManager : MonoBehaviour
{

  [Header("Menu UI")]
  [SerializeField]
  internal Button Menu_Button;
  [SerializeField]
  private GameObject Menu_Object;
  [SerializeField]
  private RectTransform Menu_RT;
  [SerializeField]
  private GameObject Info_Object;
  [SerializeField]
  private Button Info_Button;
  [SerializeField]
  private Button Info_Exit;
  [SerializeField]
  private RectTransform Info_RT;

  //[SerializeField]
  //private Button About_Button;
  //[SerializeField]
  //private GameObject About_Object;
  //[SerializeField]
  //private RectTransform About_RT;

  [Header("Settings UI")]
  [SerializeField]
  private Button Settings_Button;
  [SerializeField]
  private GameObject Settings_Object;
  [SerializeField]
  private RectTransform Settings_RT;
  [SerializeField]
  private Button Terms_Button;
  [SerializeField]
  private Button Privacy_Button;

  [SerializeField]
  private Button Exit_Button;
  [SerializeField]
  private GameObject Exit_Object;
  [SerializeField]
  private RectTransform Exit_RT;

  [SerializeField]
  private Button Paytable_Button;
  [SerializeField]
  private GameObject Paytable_Object;
  [SerializeField]
  private RectTransform Paytable_RT;

  [Header("Popus UI")]
  [SerializeField]
  private GameObject MainPopup_Object;

  [SerializeField]
  private Image AboutLogo_Image;
  [SerializeField]
  private Button Support_Button;

  [Header("Paytable Popup")]
  [SerializeField]
  private GameObject PaytablePopup_Object;
  [SerializeField]
  private Button PaytableExit_Button;
  [SerializeField]
  private TMP_Text[] SymbolsText;
  [SerializeField]
  private TMP_Text FreeSpin_Text;
  [SerializeField]
  private TMP_Text Scatter_Text;
  [SerializeField]
  private TMP_Text Jackpot_Text;
  [SerializeField]
  private TMP_Text Bonus_Text;
  [SerializeField]
  private TMP_Text Wild_Text;

  [Header("Settings Popup")]
  [SerializeField]
  private GameObject SettingsPopup_Object;
  [SerializeField]
  private Button SettingsExit_Button;
  [SerializeField]
  private Button Sound_Button;
  [SerializeField]
  private Button Music_Button;

  [SerializeField]
  private GameObject MusicOn_Object;
  [SerializeField]
  private GameObject MusicOff_Object;
  [SerializeField]
  private GameObject SoundOn_Object;
  [SerializeField]
  private GameObject SoundOff_Object;

  [Header("Win Banner")]
  //The panel GameObject must stay ACTIVE in the scene: CoinFountainPool's pool pre-warm runs in Start(),
  //and StartFountain() calls StartCoroutine, which throws on an inactive GameObject. Show/hide via alpha.
  [SerializeField]
  private CanvasGroup WinPopup_CG;
  [SerializeField]
  private TMP_Text Win_Text;
  [SerializeField]
  private CoinFountainPool coinPool;
  [SerializeField]
  private WinTierSpec[] winTiers;   //ascending by betMultiplier: Text, Big, Huge, Mega

  [Header("Win Banner - Timings")]
  [SerializeField] private float panelFadeInDuration = 0.25f;
  [SerializeField] private float panelFadeOutDuration = 0.35f;
  [SerializeField] private float textScaleUpDuration = 0.8f;
  [SerializeField] private float textScaleOutDuration = 0.4f;
  [SerializeField] private float textMoveDuration = 0.4f;
  [SerializeField] private float postLerpHoldDuration = 2f;
  [SerializeField] private float winTextTargetY = -131f;

  //A word's life: pop 0 -> peak (OutBack), settle peak -> pulseMin (InOutSine), then yoyo pulseMin <-> pulseMax
  //forever. Each stage starts exactly where the previous one ended, so nothing ever snaps.
  [Header("Win Banner - Tier Image")]
  [SerializeField] private float bannerScaleUpDuration = 0.5f;
  [SerializeField] private float bannerSettleDuration = 0.35f;   //peak -> pulseMin, hands off to the pulse
  [SerializeField] private float bannerSwapOutDuration = 0.25f;  //outgoing word shrinking away
  [SerializeField] private float bannerPeakScale = 1.2f;
  [SerializeField] private float bannerPulseMin = 0.9f;
  [SerializeField] private float bannerPulseMax = 1.1f;
  [SerializeField] private float bannerPulseDuration = 0.6f;

  [Header("Win Banner - Debug")]
  [SerializeField] private bool enableWinBannerDebugKeys = false;
  [SerializeField] private double debugWinAmount = 250;

  [Header("FreeSpins Popup")]
  [SerializeField]
  private GameObject FreeSpinPopup_Object;
  [SerializeField]
  private TMP_Text Free_Text;
  [SerializeField]
  private Button FreeSpin_Button;

  [Header("Splash Screen")]
  [SerializeField]
  private GameObject Loading_Object;
  [SerializeField]
  private Image Loading_Image;
  [SerializeField]
  private TMP_Text Loading_Text;
  [SerializeField]
  private TMP_Text LoadPercent_Text;
  [SerializeField]
  private Button QuitSplash_button;

  [Header("Disconnection Popup")]
  [SerializeField]
  private Button CloseDisconnect_Button;
  [SerializeField]
  private GameObject DisconnectPopup_Object;

  [Header("AnotherDevice Popup")]
  [SerializeField]
  private Button CloseAD_Button;
  [SerializeField]
  private GameObject ADPopup_Object;

  [Header("Reconnection Popup")]
  [SerializeField]
  private TMP_Text reconnect_Text;
  [SerializeField]
  private GameObject ReconnectPopup_Object;

  [Header("LowBalance Popup")]
  [SerializeField]
  private Button LBExit_Button;
  [SerializeField]
  private GameObject LBPopup_Object;

  [Header("Quit Popup")]
  [SerializeField]
  private GameObject QuitPopup_Object;
  [SerializeField]
  private Button YesQuit_Button;
  [SerializeField]
  private Button NoQuit_Button;
  [SerializeField]
  private Button CrossQuit_Button;


  [SerializeField]
  private AudioController audioController;
  [SerializeField]
  private Button m_AwakeGameButton;

  [SerializeField]
  private Button GameExit_Button;

  [SerializeField]
  private SlotBehaviour slotManager;

  [SerializeField]
  private SocketIOManager socketManager;

  [SerializeField]
  private GameObject m_BetPanel;
  [SerializeField]
  private Button m_ExitBetPanel;
  [SerializeField]
  private List<Button> m_BetButtons;

  [SerializeField]
  private List<float> m_DummyBetValues;

  private bool isMusic = true;
  private bool isSound = true;
  private bool isExit = false;
  private bool MenuOpen = false;

  internal int FreeSpins;

  //Win banner state. isWinAnimating is the single flag every waiter polls: true from tier resolution
  //until the fade-out has fully finished, so a waiter is just WaitWhile(() => isWinAnimating).
  internal bool isWinAnimating;
  private Coroutine _winCoroutine;
  private readonly List<Tween> _winTweens = new List<Tween>();
  private Tween _bannerPop;        //pop+settle sequence on the incoming word
  private Tween _bannerPulse;      //infinite yoyo; also tracked in _winTweens, but needs its own handle
  private Image _activeImage;      //the word currently on screen
  private readonly List<Image> _allBannerImages = new List<Image>();
  private float _winTextOriginalY;
  private double _currentWinAmount;
  private bool _tearingDown;

  private void Awake()
  {
    //Captured before anything can slide the label. Lazy capture would read an already-moved Y.
    if (Win_Text) _winTextOriginalY = Win_Text.rectTransform.anchoredPosition.y;

    //Derived from the tier table rather than a second serialized array: teardown must hide every word,
    //and a hand-maintained list is one the developer can forget to update.
    for (int i = 0; i < winTiers.Length; i++)
    {
      BannerCue[] cues = winTiers[i].cues;
      if (cues == null) continue;
      for (int c = 0; c < cues.Length; c++)
        if (cues[c].image && !_allBannerImages.Contains(cues[c].image)) _allBannerImages.Add(cues[c].image);
    }

    HideBannerInstant();
  }

  private void Start()
  {

    if (Menu_Button) Menu_Button.onClick.RemoveAllListeners();
    if (Menu_Button) Menu_Button.onClick.AddListener(OpenMenu);

    if (Exit_Button) Exit_Button.onClick.RemoveAllListeners();
    if (Exit_Button) Exit_Button.onClick.AddListener(CloseMenu);

    if (Info_Button) Info_Button.onClick.RemoveAllListeners();
    if (Info_Button) Info_Button.onClick.AddListener(delegate { OpenPopup(Info_Object); CloseMenu(); });

    if (Info_Exit) Info_Exit.onClick.RemoveAllListeners();
    if (Info_Exit) Info_Exit.onClick.AddListener(delegate { ClosePopup(Info_Object); });

    if (Paytable_Button) Paytable_Button.onClick.RemoveAllListeners();
    if (Paytable_Button) Paytable_Button.onClick.AddListener(delegate { OpenPopup(PaytablePopup_Object); CloseMenu(); });

    if (PaytableExit_Button) PaytableExit_Button.onClick.RemoveAllListeners();
    if (PaytableExit_Button) PaytableExit_Button.onClick.AddListener(delegate { ClosePopup(PaytablePopup_Object); });

    if (Settings_Button) Settings_Button.onClick.RemoveAllListeners();
    if (Settings_Button) Settings_Button.onClick.AddListener(delegate { OpenPopup(SettingsPopup_Object); CloseMenu(); });

    if (SettingsExit_Button) SettingsExit_Button.onClick.RemoveAllListeners();
    if (SettingsExit_Button) SettingsExit_Button.onClick.AddListener(delegate { ClosePopup(SettingsPopup_Object); });

    if (MusicOn_Object) MusicOn_Object.SetActive(true);
    if (MusicOff_Object) MusicOff_Object.SetActive(false);

    if (SoundOn_Object) SoundOn_Object.SetActive(true);
    if (SoundOff_Object) SoundOff_Object.SetActive(false);

    if (GameExit_Button) GameExit_Button.onClick.RemoveAllListeners();
    if (GameExit_Button) GameExit_Button.onClick.AddListener(delegate
    {
      OpenPopup(QuitPopup_Object);
      CloseMenu();
      Debug.Log("Quit event: pressed Big_X button");

    });

    if (NoQuit_Button) NoQuit_Button.onClick.RemoveAllListeners();
    if (NoQuit_Button) NoQuit_Button.onClick.AddListener(delegate
    {
      ClosePopup(QuitPopup_Object);
      Debug.Log("quit event: pressed NO Button ");
    });

    if (CrossQuit_Button) CrossQuit_Button.onClick.RemoveAllListeners();
    if (CrossQuit_Button) CrossQuit_Button.onClick.AddListener(delegate
    {
      ClosePopup(QuitPopup_Object);
      Debug.Log("quit event: pressed Small_X Button ");

    });

    if (LBExit_Button) LBExit_Button.onClick.RemoveAllListeners();
    if (LBExit_Button) LBExit_Button.onClick.AddListener(delegate { ClosePopup(LBPopup_Object); });

    if (YesQuit_Button) YesQuit_Button.onClick.RemoveAllListeners();
    if (YesQuit_Button) YesQuit_Button.onClick.AddListener(delegate
    {
      CallOnExitFunction();
      Debug.Log("quit event: pressed YES Button ");

    });

    if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.RemoveAllListeners();
    if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.AddListener(delegate { CallOnExitFunction(); socketManager.ReactNativeCallOnFailedToConnect(); }); //BackendChanges

    if (CloseAD_Button) CloseAD_Button.onClick.RemoveAllListeners();
    if (CloseAD_Button) CloseAD_Button.onClick.AddListener(CallOnExitFunction);

    if (FreeSpin_Button) FreeSpin_Button.onClick.RemoveAllListeners();
    if (FreeSpin_Button) FreeSpin_Button.onClick.AddListener(delegate { StartFreeSpins(FreeSpins); });

    if (QuitSplash_button) QuitSplash_button.onClick.RemoveAllListeners();
    if (QuitSplash_button) QuitSplash_button.onClick.AddListener(delegate { OpenPopup(QuitPopup_Object); CloseMenu(); });

    isMusic = true;
    isSound = true;

    if (Sound_Button) Sound_Button.onClick.RemoveAllListeners();
    if (Sound_Button) Sound_Button.onClick.AddListener(ToggleSound);

    if (Music_Button) Music_Button.onClick.RemoveAllListeners();
    if (Music_Button) Music_Button.onClick.AddListener(ToggleMusic);

    if (m_ExitBetPanel) m_ExitBetPanel.onClick.RemoveAllListeners();
    if (m_ExitBetPanel) m_ExitBetPanel.onClick.AddListener(() =>
    {
      m_BetPanel.SetActive(false);
    });
  }

  #region [BET BUTTONS HANDLING]
  internal void AssignBetButtons(List<double> m_bet_items)
  {
    for (int i = 0; i < m_BetButtons.Count; i++)
    {
      Button m_Temp_Bet = m_BetButtons[i];
      if (i < m_bet_items.Count)
      {
        m_Temp_Bet.gameObject.SetActive(true);
        m_Temp_Bet.transform.GetChild(0).GetComponent<TMP_Text>().text = m_bet_items[i].ToString("f3");
        m_Temp_Bet.onClick.RemoveAllListeners();
        m_Temp_Bet.onClick.AddListener(() =>
        {
          m_BetPanel.SetActive(false);
          slotManager.OnBetClicked(GetBetCounter(m_Temp_Bet), double.Parse(m_Temp_Bet.transform.GetChild(0).GetComponent<TMP_Text>().text));
        });
      }
      else
      {
        m_BetButtons[i].gameObject.SetActive(false);
      }
    }
  }

  private int GetBetCounter(Button m_Click_Button)
  {
    for (int _ = 0; _ < m_BetButtons.Count; _++)
    {
      if (m_BetButtons[_] == m_Click_Button)
      {
        return _;
      }
    }
    return 0;
  }

  internal void OpenBetPanel()
  {
    m_BetPanel.SetActive(true);
  }
  #endregion

  private void Update()
  {
    if (!enableWinBannerDebugKeys) return;

    //Keys 1-4 play Text / Big / Huge / Mega directly, bypassing the resolver: synthesising a win amount
    //that lands in the right band silently misfires the moment the betMultiplier ladder is retuned.
    for (int i = 0; i < winTiers.Length && i < 4; i++)
    {
      if (Input.GetKeyDown(KeyCode.Alpha1 + i)) PlayBannerTier(i, debugWinAmount);
    }
    if (Input.GetKeyDown(KeyCode.Alpha5)) ResetWinBanner();
  }

  internal void LowBalPopup()
  {
    OpenPopup(LBPopup_Object);
  }

  internal void DisconnectionPopup()
  {
    if (!isExit)
    {
      isExit = true;
      OpenPopup(DisconnectPopup_Object);
    }
  }
  internal void ReconnectionPopup()
  {
    OpenPopup(ReconnectPopup_Object);
  }

  #region [WIN BANNER]

  //Fire-and-forget from the end of a spin. Manual spins never wait on this; chained modes poll
  //isWinAnimating. Snap-resets first, so a below-floor win still clears the previous banner.
  internal void TriggerWinBanner(double winAmount, double betAmount)
  {
    //A zero bet would make every threshold trivially true, so a zero win must short-circuit first.
    int i = winAmount > 0 ? ResolveTierIndex(winAmount, betAmount) : -1;
    if (i < 0) { SnapResetWinBanner(); return; }   //WinTier.None still clears the previous banner
    PlayBannerTier(i, winAmount);
  }

  //Highest tier whose threshold is met. Because winTiers ascends, precedence lives in the data.
  private int ResolveTierIndex(double winAmount, double betAmount)
  {
    int resolved = -1;
    for (int i = 0; i < winTiers.Length; i++)
      if (winAmount >= betAmount * winTiers[i].betMultiplier) resolved = i;
    return resolved;
  }

  private void PlayBannerTier(int tierIndex, double winAmount)
  {
    if (tierIndex < 0 || tierIndex >= winTiers.Length) return;
    SnapResetWinBanner();
    _winCoroutine = StartCoroutine(WinBannerCoroutine(winAmount, winTiers[tierIndex]));
  }

  private IEnumerator WinBannerCoroutine(double winAmount, WinTierSpec spec)
  {
    isWinAnimating = true;
    _currentWinAmount = winAmount;

    int decimalPlaces = GetSignificantDecimals(winAmount);

    if (WinPopup_CG) _winTweens.Add(WinPopup_CG.DOFade(1f, panelFadeInDuration));
    if (Win_Text)
    {
      Win_Text.text = FormatWin(0, decimalPlaces);
      ScaleInObject(Win_Text.rectTransform, Vector3.one, textScaleUpDuration);
    }
    if (audioController) audioController.PlayWin(spec.openSound);

    BannerCue[] cues = spec.cues ?? System.Array.Empty<BannerCue>();
    float elapsed = 0f;
    int nextCue = 0;



    while (elapsed < spec.lerpDuration)
    {
      elapsed += Time.deltaTime;
      float t = Mathf.Clamp01(elapsed / spec.lerpDuration);

      //Linear, un-eased. The number climbs at a constant rate; only the scale and slide are eased.
      if (Win_Text) Win_Text.text = FormatWin(t * winAmount, decimalPlaces);

      //while, not if: a long frame can cross two thresholds at once, and dropping one would leave
      //the banner showing HUGE after MEGA already fired.
      while (nextCue < cues.Length && t >= cues[nextCue].threshold)
      {
        if (nextCue == 0)
        {
          StartMoveWinTextDown();
          if (coinPool) coinPool.StartFountain();
        }
        ShowBannerCue(cues[nextCue]);
        nextCue++;
      }

      yield return null;
    }

    //Not redundant: a variable frame time overshoots lerpDuration, so the loop exits one frame stale.
    if (Win_Text) Win_Text.text = FormatWin(winAmount, decimalPlaces);

    yield return new WaitForSeconds(postLerpHoldDuration);

    //Yielded as a bare IEnumerator, not StartCoroutine: this keeps the teardown part of THIS coroutine,
    //so StopCoroutine(_winCoroutine) from either reset path actually stops it.
    yield return TearDownBanner();
  }

  //Shared tail for the natural end and the graceful skip.
  private IEnumerator TearDownBanner()
  {
    _tearingDown = true;

    //Kill the pop/pulse before the scale-out: all three write the active word's localScale.
    KillBannerImageTweens();

    if (coinPool) coinPool.FadeOutAllActive(panelFadeOutDuration);
    if (Win_Text) ScaleOutObject(Win_Text.transform);
    if (_activeImage) ScaleOutObject(_activeImage.transform);
    if (WinPopup_CG) _winTweens.Add(WinPopup_CG.DOFade(0f, panelFadeOutDuration));

    yield return new WaitForSeconds(Mathf.Max(panelFadeOutDuration, textScaleOutDuration));

    KillWinTweens();
    HideBannerInstant();
    _winCoroutine = null;
    _tearingDown = false;
    isWinAnimating = false;
  }

  //Instant, no tweens. The pre-arm: a new banner starts clean even if the previous one is mid-flight.
  internal void SnapResetWinBanner()
  {
    if (_winCoroutine != null)
    {
      StopCoroutine(_winCoroutine);
      _winCoroutine = null;
    }
    KillWinTweens();
    HideBannerInstant();
    _tearingDown = false;
    isWinAnimating = false;
  }

  //Graceful skip: show the player the full win, then clear. isWinAnimating stays true until the
  //fade-out finishes, so waiters release at the same point they would on a natural end.
  internal void ResetWinBanner()
  {
    //Already fading out — restarting the teardown would pop the label back to full first.
    if (_tearingDown) return;

    bool wasLerping = _winCoroutine != null;
    if (!wasLerping)
    {
      SnapResetWinBanner();
      return;
    }

    StopCoroutine(_winCoroutine);
    _winCoroutine = null;
    KillWinTweens();

    //The count-up jumps to the final amount, so a skip always shows the player what they won.
    if (Win_Text && _currentWinAmount > 0)
    {
      Win_Text.text = FormatWin(_currentWinAmount);
      Win_Text.transform.localScale = Vector3.one;
    }

    _winCoroutine = StartCoroutine(TearDownBanner());
  }

  //Retires the word on screen and brings the next one in. Each word is its own Image object, so the
  //outgoing one can shrink away while the incoming one pops, instead of one object teleporting to zero.
  private void ShowBannerCue(BannerCue cue)
  {
    if (audioController) audioController.PlayWin(cue.sound);
    Image incoming = cue.image;
    if (!incoming) return;

    //Two cues can land in the same frame on a long one; the outgoing word's pop/pulse must not survive
    //to fight the scale-out over localScale.
    KillBannerImageTweens();

    if (_activeImage && _activeImage != incoming) ScaleOutBannerImage(_activeImage);

    _activeImage = incoming;
    incoming.enabled = true;
    RectTransform rt = incoming.rectTransform;
    rt.localScale = Vector3.zero;

    //Pop then settle, as one sequence. The settle lands exactly on bannerPulseMin, which is where the
    //pulse begins — that handoff is what used to hiccup when the pulse snapped the scale itself.
    Sequence seq = DOTween.Sequence();
    seq.Append(rt.DOScale(bannerPeakScale, bannerScaleUpDuration).SetEase(Ease.OutBack));
    seq.Append(rt.DOScale(bannerPulseMin, bannerSettleDuration).SetEase(Ease.InOutSine));
    seq.OnComplete(StartBannerPulse);

    _bannerPop = seq;
    _winTweens.Add(seq);
  }

  private void StartBannerPulse()
  {
    _bannerPop = null;
    if (!_activeImage) return;

    //Starts from wherever the settle left the scale (bannerPulseMin), so the first frame of the pulse
    //is continuous with the last frame of the settle. Yoyo then swings it back and forth.
    _bannerPulse = _activeImage.rectTransform
        .DOScale(bannerPulseMax, bannerPulseDuration)
        .SetEase(Ease.InOutSine)
        .SetLoops(-1, LoopType.Yoyo);
    _winTweens.Add(_bannerPulse);
  }

  private void ScaleOutBannerImage(Image img)
  {
    if (!img) return;
    Image captured = img;
    _winTweens.Add(captured.rectTransform
        .DOScale(0f, bannerSwapOutDuration)
        .SetEase(Ease.InBack)
        .OnComplete(() => { if (captured) captured.enabled = false; }));
  }

  private void KillBannerImageTweens()
  {
    _bannerPop?.Kill();
    _bannerPop = null;
    _bannerPulse?.Kill();
    _bannerPulse = null;
  }

  //Pure tween. Its only job is geometry — all art lives in ShowBannerCue / HideBannerInstant.
  private void StartMoveWinTextDown()
  {
    if (!Win_Text) return;
    _winTweens.Add(Win_Text.rectTransform
        .DOAnchorPosY(winTextTargetY, textMoveDuration)
        .SetEase(Ease.Linear));
  }

  private Tween ScaleInObject(RectTransform rt, Vector3 target, float duration)
  {
    rt.localScale = Vector3.zero;
    Tween t = rt.DOScale(target, duration).SetEase(Ease.OutBack);
    _winTweens.Add(t);
    return t;
  }

  //The zero-scale early-out is what makes teardown idempotent: both reset paths and the coroutine's
  //own tail may call this, in any order, and only the first does work.
  private void ScaleOutObject(Transform tr)
  {
    if (tr == null || tr.localScale == Vector3.zero) return;
    _winTweens.Add(tr.DOScale(Vector3.zero, textScaleOutDuration).SetEase(Ease.InBack));
  }

  private void KillWinTweens()
  {
    for (int i = 0; i < _winTweens.Count; i++) _winTweens[i]?.Kill();
    _winTweens.Clear();
    _bannerPop = null;
    _bannerPulse = null;
  }

  //Never SetActive(false) the panel: CoinFountainPool needs an active GameObject to pre-warm and to
  //StartCoroutine its spawn loop.
  private void HideBannerInstant()
  {
    if (WinPopup_CG)
    {
      WinPopup_CG.alpha = 0f;
      WinPopup_CG.interactable = false;
      WinPopup_CG.blocksRaycasts = false;   //the whole point: clicks pass through to the spin button
    }
    if (Win_Text)
    {
      Win_Text.transform.localScale = Vector3.zero;
      Vector2 p = Win_Text.rectTransform.anchoredPosition;
      Win_Text.rectTransform.anchoredPosition = new Vector2(p.x, _winTextOriginalY);
    }
    for (int i = 0; i < _allBannerImages.Count; i++)
    {
      Image img = _allBannerImages[i];
      if (!img) continue;
      img.rectTransform.localScale = Vector3.zero;
      img.enabled = false;
    }
    _activeImage = null;
    if (coinPool) coinPool.ClearAll();
    _currentWinAmount = 0;
  }

  private string FormatWin(double value, int decimalPlaces = 0) => value.ToString("F" + GetSignificantDecimals(value, decimalPlaces));
  
  public static int GetSignificantDecimals(double value, int minDecimals = 0)
  {
    string raw = value.ToString("F3");
    int dotIndex = raw.IndexOf('.');
    int places = 0;
    if (dotIndex >= 0)
    {
      string decimals = raw.Substring(dotIndex + 1).TrimEnd('0');
      places = decimals.Length;
    }
    return Mathf.Max(minDecimals, places);
  }
  #endregion

  private void StartFreeSpins(int spins)
  {
    if (MainPopup_Object) MainPopup_Object.SetActive(false);
    if (FreeSpinPopup_Object) FreeSpinPopup_Object.SetActive(false);
    slotManager.FreeSpin(spins);
  }

  internal void FreeSpinProcess(int spins)
  {
    int ExtraSpins = spins - FreeSpins;
    FreeSpins = spins;
    Debug.Log(ExtraSpins);
    if (FreeSpinPopup_Object) FreeSpinPopup_Object.SetActive(true);
    if (Free_Text) Free_Text.text = ExtraSpins.ToString() + " Free spins awarded.";
    if (MainPopup_Object) MainPopup_Object.SetActive(true);
    DOVirtual.DelayedCall(2f, () =>
    {
      StartFreeSpins(spins);
    });
  }

  internal void ADfunction()
  {
    OpenPopup(ADPopup_Object);
  }

  internal void InitialiseUIData(Paylines symbolsText, Features feat)
  {
    PopulateSymbolsPayout(symbolsText, feat);
  }

  private void PopulateSymbolsPayout(Paylines paylines, Features feat)
  {
    for (int i = 0; i < SymbolsText.Length; i++)
    {
      string text = null;
      if (paylines.symbols[i].multiplier[0] != 0)
      {
        text += "<color=yellow>5x - </color> " + paylines.symbols[i].multiplier[0] + "x";
      }
      if (paylines.symbols[i].multiplier[1] != 0)
      {
        text += "<color=yellow>\n4x - </color> " + paylines.symbols[i].multiplier[1] + "x";
      }
      if (paylines.symbols[i].multiplier[2] != 0)
      {
        text += "<color=yellow>\n3x - </color> " + paylines.symbols[i].multiplier[2] + "x";
      }
      if (paylines.symbols[i].multiplier[3] != 0)
      {
        text += "<color=yellow>\n2x - </color> " + paylines.symbols[i].multiplier[3] + "x";
      }
      if (SymbolsText[i]) SymbolsText[i].text = text;
    }

    for (int i = 0; i < paylines.symbols.Count; i++)
    {
      if (paylines.symbols[i].name.ToUpper() == "FREESPIN")
      {
        //if (FreeSpin_Text) FreeSpin_Text.text = paylines.symbols[i].description.ToString();
        string freespintext = $"3x {feat.freeSpin.counts[3]} FREE SPINS \n4x {feat.freeSpin.counts[2]} FREE SPINS \n5x {feat.freeSpin.counts[1]} FREE SPINS \n6x {feat.freeSpin.counts[0]} FREE SPINS";
        if (FreeSpin_Text) FreeSpin_Text.text = freespintext;
      }
      if (paylines.symbols[i].name.ToUpper() == "SCATTER")
      {
        // if (Scatter_Text) Scatter_Text.text = paylines.symbols[i].description.ToString();
      }
      if (paylines.symbols[i].name.ToUpper() == "JACKPOT")
      {
        // if (Jackpot_Text) Jackpot_Text.text = paylines.symbols[i].description.ToString();
      }
      if (paylines.symbols[i].name.ToUpper() == "BONUS")
      {
        // if (Bonus_Text) Bonus_Text.text = paylines.symbols[i].description.ToString();
      }
      if (paylines.symbols[i].name.ToUpper() == "WILD")
      {
        // if (Wild_Text) Wild_Text.text = paylines.symbols[i].description.ToString();
      }
    }
  }

  private void CallOnExitFunction()
  {
    isExit = true;
    audioController.PlayNormalButton();
    slotManager.CallCloseSocket();
  }

  private void OpenMenu()
  {
    MenuOpen = true;
    audioController.PlayNormalButton();
    if (Menu_Object) Menu_Object.SetActive(false);
    if (Exit_Object) Exit_Object.SetActive(true);
    if (Info_RT.gameObject) Info_RT.gameObject.SetActive(true);
    if (Paytable_Object) Paytable_Object.SetActive(true);
    if (Settings_Object) Settings_Object.SetActive(true);

    DOTween.To(() => Info_RT.anchoredPosition, (val) => Info_RT.anchoredPosition = val, new Vector2(Info_RT.anchoredPosition.x, Info_RT.anchoredPosition.y + 375), 0.1f).OnUpdate(() =>
    {
      LayoutRebuilder.ForceRebuildLayoutImmediate(Info_RT);
    });

    DOTween.To(() => Paytable_RT.anchoredPosition, (val) => Paytable_RT.anchoredPosition = val, new Vector2(Paytable_RT.anchoredPosition.x, Paytable_RT.anchoredPosition.y + 125), 0.1f).OnUpdate(() =>
    {
      LayoutRebuilder.ForceRebuildLayoutImmediate(Paytable_RT);
    });

    DOTween.To(() => Settings_RT.anchoredPosition, (val) => Settings_RT.anchoredPosition = val, new Vector2(Settings_RT.anchoredPosition.x, Settings_RT.anchoredPosition.y + 250), 0.1f).OnUpdate(() =>
    {
      LayoutRebuilder.ForceRebuildLayoutImmediate(Settings_RT);
    });
  }

  internal void CloseMenu()
  {
    if (MenuOpen)
    {
      if (audioController) audioController.PlayNormalButton();

      DOTween.To(() => Info_RT.anchoredPosition, (val) => Info_RT.anchoredPosition = val, new Vector2(Info_RT.anchoredPosition.x, Info_RT.anchoredPosition.y - 375), 0.1f).OnUpdate(() =>
      {
        LayoutRebuilder.ForceRebuildLayoutImmediate(Info_RT);
      });

      DOTween.To(() => Paytable_RT.anchoredPosition, (val) => Paytable_RT.anchoredPosition = val, new Vector2(Paytable_RT.anchoredPosition.x, Paytable_RT.anchoredPosition.y - 125), 0.1f).OnUpdate(() =>
      {
        LayoutRebuilder.ForceRebuildLayoutImmediate(Paytable_RT);
      });

      DOTween.To(() => Settings_RT.anchoredPosition, (val) => Settings_RT.anchoredPosition = val, new Vector2(Settings_RT.anchoredPosition.x, Settings_RT.anchoredPosition.y - 250), 0.1f).OnUpdate(() =>
      {
        LayoutRebuilder.ForceRebuildLayoutImmediate(Settings_RT);
      });

      DOVirtual.DelayedCall(0.1f, () =>
       {
         if (Menu_Object) Menu_Object.SetActive(true);
         if (Exit_Object) Exit_Object.SetActive(false);
         if (Info_RT.gameObject) Info_RT.gameObject.SetActive(false);
         if (Paytable_Object) Paytable_Object.SetActive(false);
         if (Settings_Object) Settings_Object.SetActive(false);
       });

      MenuOpen = false;
    }
  }

  private void OpenPopup(GameObject Popup)
  {
    if (Popup == LBPopup_Object)
    {
      if (PaytablePopup_Object.activeSelf) PaytablePopup_Object.SetActive(false);
      if (SettingsPopup_Object.activeSelf) SettingsPopup_Object.SetActive(false);
    }
    if (audioController) audioController.PlayNormalButton();
    if (Popup) Popup.SetActive(true);
    if (MainPopup_Object) MainPopup_Object.SetActive(true);
  }

  private void ClosePopup(GameObject Popup)
  {
    if (audioController) audioController.PlayNormalButton();
    if (Popup) Popup.SetActive(false);
    if (!DisconnectPopup_Object.activeSelf)
    {
      if (MainPopup_Object) MainPopup_Object.SetActive(false);
    }
  }

  internal void CheckAndClosePopups()
  {
    if (ReconnectPopup_Object.activeInHierarchy)
    {
      ClosePopup(ReconnectPopup_Object);
    }
    if (DisconnectPopup_Object.activeInHierarchy)
    {
      ClosePopup(DisconnectPopup_Object);
    }
  }

  private void ToggleMusic()
  {
    isMusic = !isMusic;
    if (isMusic)
    {
      if (MusicOn_Object) MusicOn_Object.SetActive(true);
      if (MusicOff_Object) MusicOff_Object.SetActive(false);
      audioController.MuteUnmute(Sound.Music, false, true);
    }
    else
    {
      if (MusicOn_Object) MusicOn_Object.SetActive(false);
      if (MusicOff_Object) MusicOff_Object.SetActive(true);
      audioController.MuteUnmute(Sound.Music, true, true);
    }
  }

  private void UrlButtons(string url)
  {
    Application.OpenURL(url);
  }

  private void ToggleSound()
  {
    isSound = !isSound;
    if (isSound)
    {
      if (SoundOn_Object) SoundOn_Object.SetActive(true);
      if (SoundOff_Object) SoundOff_Object.SetActive(false);
      if (audioController) audioController.MuteUnmute(Sound.Sound, false, true);
    }
    else
    {
      if (SoundOn_Object) SoundOn_Object.SetActive(false);
      if (SoundOff_Object) SoundOff_Object.SetActive(true);
      if (audioController) audioController.MuteUnmute(Sound.Sound, true, true);
    }
  }
}
