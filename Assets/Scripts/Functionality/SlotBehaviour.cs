using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using System;

public class SlotBehaviour : MonoBehaviour
{
  [Header("Sprites")]
  [SerializeField]
  private Sprite[] myImages;  //images taken initially

  [Header("Slot Images")]
  [SerializeField]
  private List<SlotImage> images;     //class to store total images
  [SerializeField]
  internal List<SlotImage> Tempimages;     //class to store the result matrix
  [SerializeField]
  private AnimationController m_AnimationController;
  [SerializeField]
  private Image m_MainUIMask;
  [SerializeField]
  private GameObject m_BuffaloRush;

  [Header("Slots Elements")]
  [SerializeField]
  private LayoutElement[] Slot_Elements;

  [Header("Slots Transforms")]
  [SerializeField]
  private Transform[] Slot_Transform;

  [Header("Line Button Objects")]
  [SerializeField]
  private List<GameObject> StaticLine_Objects;

  [Header("Line Button Texts")]
  [SerializeField]
  private List<TMP_Text> StaticLine_Texts;

  private Dictionary<int, string> y_string = new Dictionary<int, string>();

  [Header("Buttons")]
  [SerializeField]
  private Button SlotStart_Button;
  [SerializeField]
  private Button AutoSpin_Button;
  [SerializeField] private Button AutoSpinStop_Button;
  [SerializeField]
  private Button MaxBet_Button;
  [SerializeField]
  private Button BetPlus_Button;
  [SerializeField]
  private Button BetMinus_Button;
  [SerializeField]
  private Button m_BetButton;

  [SerializeField] private Button TurboOff_Button;   // active by default; click => turn turbo ON
  [SerializeField] private Button TurboOn_Button;    // inactive by default; click => turn turbo OFF
  [SerializeField] private Button StopSpin_Button;

  private bool StopSpinToggle;
  private float SpinDelay = 0.2f;
  [SerializeField] private float postWinBannerDelay = 0.35f;  //breath between the banner clearing and the next chained spin
  [SerializeField] private float noWinSpinDelay = 0.2f;
  internal bool IsTurboOn;
  private bool WasAutoSpinOn;

  [Header("Animated Sprites")]
  [SerializeField]
  private Sprite[] Cat_Sprite;
  [SerializeField]
  private Sprite[] Eagle_Sprite;
  [SerializeField]
  private Sprite[] Bear_Sprite;
  [SerializeField]
  private Sprite[] Wolf_Sprite;
  [SerializeField]
  private Sprite[] Buffalo_Sprite;
  [SerializeField]
  private Sprite[] Gold_Buffalo;
  [SerializeField]
  private Sprite[] Landscape_Sprite;
  [SerializeField]
  private Sprite[] Bonus_Sprite;

  [Header("Miscellaneous UI")]
  [SerializeField]
  private TMP_Text Balance_text;
  [SerializeField]
  private TMP_Text TotalBet_text;
  [SerializeField]
  private TMP_Text LineBet_text;
  [SerializeField]
  private TMP_Text TotalWin_text;


  [Header("Audio Management")]
  [SerializeField]
  private AudioController audioController;

  [SerializeField]
  private UIManager uiManager;

  [Header("Free Spins Board")]
  [SerializeField]
  private GameObject FSBoard_Object;
  [SerializeField]
  private TMP_Text FSnum_text;

  int tweenHeight = 0;  //calculate the height at which tweening is done

  //Reel position + speed model (see Assets/Scripts/MD/REEL_TWEENING_GUIDE.md)
  private const int RestRowIndex = 6;           //the row the reel settles on (was StopTweening's 'reqpos')
  private float SpinTopY = 0f;                  //top of the strip: loop start / spawn point
  private float SpinBottomY => -tweenHeight;    //bottom of the strip: loop exit point
  //final resting Y where results are shown. Derived from IconSizeFactor (a [SerializeField], 177 in-scene),
  //never hardcoded — this reproduces the original -(reqpos * IconSizeFactor - IconSizeFactor) + 100.
  [SerializeField] private float RestY;
  [SerializeField] private float reelSpeed = 2857f;  //reel travel speed in local units/second

  //Duration needed to travel between two Y positions at reelSpeed.
  private float DurationFor(float fromY, float toY)
      => Mathf.Abs(toY - fromY) / Mathf.Max(reelSpeed, 0.0001f);

  [SerializeField]
  private GameObject Image_Prefab;    //icons prefab

  private List<Tweener> alltweens = new List<Tweener>();

  private Tweener WinTween = null;

  [SerializeField]
  private List<ImageAnimation> TempList;  //stores the sprites whose animation is running at present 

  [SerializeField]
  private SocketIOManager SocketManager;

  [SerializeField]
  private List<string> m_Instructions;

  [SerializeField]
  private List<OrderingUI> m_UI_Order = new List<OrderingUI>();

  private Coroutine AutoSpinRoutine = null;
  private Coroutine FreeSpinRoutine = null;
  private Coroutine tweenroutine;

  private bool IsAutoSpin = false;
  private bool IsFreeSpin = false;
  private bool IsSpinning = false;
  private bool CheckSpinAudio = false;

  private int BetCounter = 0;
  private double currentBalance = 0;
  private double currentTotalBet = 0;
  private double freeSpinAccumulatedWin = 0;
  protected int Lines = 20;
  [SerializeField]
  private int IconSizeFactor = 100;       //set this parameter according to the size of the icon and spacing
  private int numberOfSlots = 6;          //number of columns

  private Tweener BalanceTween;

  //protected internal int[,] m_DemoResponse =
  //        {
  //            { 1, 7, 3, 8, 5, 6 },
  //            { 3, 10, 1, 11, 9, 4},
  //            { 2, 8, 4, 12, 1, 0 },
  //            { 4, 3, 11, 9, 7, 5}
  //        };

  private void Awake()
  {
    currentBalance = 160.2346;
    Balance_text.text = currentBalance.ToString();
  }

  private void Start()
  {
    IsAutoSpin = false;

    if (SlotStart_Button) SlotStart_Button.onClick.RemoveAllListeners();
    if (SlotStart_Button) SlotStart_Button.onClick.AddListener(delegate { StartSlots(); uiManager.CloseMenu(); });

    //if (BetPlus_Button) BetPlus_Button.onClick.RemoveAllListeners();
    //if (BetPlus_Button) BetPlus_Button.onClick.AddListener(delegate { ChangeBet(true); });
    //if (BetMinus_Button) BetMinus_Button.onClick.RemoveAllListeners();
    //if (BetMinus_Button) BetMinus_Button.onClick.AddListener(delegate { ChangeBet(false); });

    //if (MaxBet_Button) MaxBet_Button.onClick.RemoveAllListeners();
    //if (MaxBet_Button) MaxBet_Button.onClick.AddListener(MaxBet);

    if (m_BetButton) m_BetButton.onClick.RemoveAllListeners();
    if (m_BetButton) m_BetButton.onClick.AddListener(() =>
    {
      uiManager.OpenBetPanel();
      uiManager.CloseMenu();
    });

    if (AutoSpin_Button) AutoSpin_Button.onClick.RemoveAllListeners();
    if (AutoSpin_Button) AutoSpin_Button.onClick.AddListener(delegate { AutoSpin(); uiManager.CloseMenu(); });


    if (AutoSpinStop_Button) AutoSpinStop_Button.onClick.RemoveAllListeners();
    if (AutoSpinStop_Button) AutoSpinStop_Button.onClick.AddListener(() =>
    {
      audioController.PlayNormalButton();
      StopAutoSpin();
    });

    if (StopSpin_Button) StopSpin_Button.onClick.RemoveAllListeners();
    if (StopSpin_Button) StopSpin_Button.onClick.AddListener(() =>
    {
      audioController.PlayNormalButton();
      StopSpinToggle = true;
      StopSpin_Button.gameObject.SetActive(false);
    });

    if (TurboOff_Button) TurboOff_Button.onClick.RemoveAllListeners();
    if (TurboOff_Button) TurboOff_Button.onClick.AddListener(() =>
    {
      audioController.PlayNormalButton();
      SetTurbo(true);
    });

    if (TurboOn_Button) TurboOn_Button.onClick.RemoveAllListeners();
    if (TurboOn_Button) TurboOn_Button.onClick.AddListener(() =>
    {
      audioController.PlayNormalButton();
      SetTurbo(false);
    });

    if (FSBoard_Object) FSBoard_Object.SetActive(false);

    tweenHeight = (myImages.Length * IconSizeFactor) - 280;
  }

  void SetTurbo(bool on)
  {
    IsTurboOn = on;
    if (TurboOff_Button) TurboOff_Button.gameObject.SetActive(!on);
    if (TurboOn_Button) TurboOn_Button.gameObject.SetActive(on);
  }

  #region Autospin
  private void AutoSpin()
  {
    if (!IsAutoSpin)
    {

      IsAutoSpin = true;
      if (AutoSpinStop_Button) AutoSpinStop_Button.gameObject.SetActive(true);
      if (AutoSpin_Button) AutoSpin_Button.gameObject.SetActive(false);

      if (AutoSpinRoutine != null)
      {
        StopCoroutine(AutoSpinRoutine);
        AutoSpinRoutine = null;
      }
      AutoSpinRoutine = StartCoroutine(AutoSpinCoroutine());

    }
  }

  private void StopAutoSpin()
  {
    if (IsAutoSpin)
    {
      IsAutoSpin = false;
      if (AutoSpinStop_Button) AutoSpinStop_Button.gameObject.SetActive(false);
      if (AutoSpin_Button) AutoSpin_Button.gameObject.SetActive(true);
      StartCoroutine(StopAutoSpinCoroutine());
    }
  }

  private IEnumerator AutoSpinCoroutine()
  {
    while (IsAutoSpin)
    {
      StartSlots(IsAutoSpin);
      yield return tweenroutine;
      //Chained modes let the celebration play out; the next StartSlots then resets it. With no banner
      //the symbol pass is the only celebration, so wait for it too or it gets cut off instantly.
      yield return new WaitWhile(() => uiManager.isWinAnimating || m_AnimationController.IsWinPassPlaying);
      if (!IsAutoSpin) break;
      yield return new WaitForSeconds(SpinDelay);
    }
    WasAutoSpinOn = false;
  }

  private IEnumerator StopAutoSpinCoroutine()
  {
    yield return new WaitUntil(() => !IsSpinning);
    ToggleButtonGrp(true);
    //Independent null checks: the handles are not necessarily both set, and StopCoroutine(null) errors.
    if (AutoSpinRoutine != null)
    {
      StopCoroutine(AutoSpinRoutine);
      AutoSpinRoutine = null;
    }
    if (tweenroutine != null)
    {
      StopCoroutine(tweenroutine);
      tweenroutine = null;
    }
  }
  #endregion

  #region FreeSpin
  internal void FreeSpin(int spins)
  {
    if (!IsFreeSpin)
    {
      if (FSnum_text) FSnum_text.text = spins.ToString();
      if (FSBoard_Object) FSBoard_Object.SetActive(true);
      IsFreeSpin = true;
      ToggleButtonGrp(false);

      if (FreeSpinRoutine != null)
      {
        StopCoroutine(FreeSpinRoutine);
        FreeSpinRoutine = null;
      }
      FreeSpinRoutine = StartCoroutine(FreeSpinCoroutine(spins));
    }
  }

  private IEnumerator FreeSpinCoroutine(int spinchances)
  {
    int i = 0;
    while (i < spinchances)
    {
      uiManager.FreeSpins--;
      StartSlots(IsAutoSpin);
      yield return tweenroutine;
      yield return new WaitWhile(() => uiManager.isWinAnimating || m_AnimationController.IsWinPassPlaying);
      yield return new WaitForSeconds(SpinDelay);
      i++;
      if (FSnum_text) FSnum_text.text = (spinchances - i).ToString();
    }
    if (FSBoard_Object) FSBoard_Object.SetActive(false);
    if (WasAutoSpinOn)
    {
      AutoSpin();
    }
    else
    {
      ToggleButtonGrp(true);
    }
    IsFreeSpin = false;
  }
  #endregion

  private void CompareBalance()
  {
    if (currentBalance < currentTotalBet)
    {
      uiManager.LowBalPopup();
    }
  }

  internal void OnBetClicked(int Bet, double Value)
  {
    if (audioController) audioController.PlayNormalButton();
    BetCounter = Bet;
    if (LineBet_text) LineBet_text.text = Value.ToString();
    if (TotalBet_text) TotalBet_text.text = Value.ToString();
    Debug.Log("Bet = " + Value);
    currentTotalBet = Value;
    CompareBalance();
  }

  #region InitialFunctions
  internal void shuffleInitialMatrix()
  {
    OrderingUI m_order = new OrderingUI { };
    OrderingUI m_anim_order = new OrderingUI { };

    for (int i = 0; i < Tempimages.Count; i++)
    {
      for (int j = 0; j < 4; j++)
      {
        int randomIndex = UnityEngine.Random.Range(0, myImages.Length - 7);
        Tempimages[i].slotImages[j].sprite = myImages[randomIndex];
        SlotControl(i, j, randomIndex, m_order, m_anim_order);
      }
    }
  }

  internal void SetInitialUI()
  {
    BetCounter = 0;
    if (LineBet_text) LineBet_text.text = SocketManager.initialData.bets[BetCounter].ToString();
    if (TotalBet_text) TotalBet_text.text = SocketManager.initialData.bets[BetCounter].ToString();
    if (TotalWin_text) TotalWin_text.text = m_Instructions[1];
    if (Balance_text) Balance_text.text = SocketManager.playerdata.balance.ToString("F" + UIManager.GetSignificantDecimals(SocketManager.playerdata.balance));
    currentBalance = SocketManager.playerdata.balance;
    currentTotalBet = SocketManager.initialData.bets[BetCounter];
    CompareBalance();
    uiManager.AssignBetButtons(SocketManager.initialData.bets);
  }
  #endregion

  //Checking The Focus Is On Application Or On Other Tabs
  private void OnApplicationFocus(bool focus)
  {
    audioController.CheckFocusFunction(focus);
  }

  //function to populate animation sprites accordingly
  private void PopulateAnimationSprites(ImageAnimation animScript, int val)
  {
    animScript.textureArray.Clear();
    animScript.textureArray.TrimExcess();
    switch (val)
    {
      case 6:
        for (int i = 0; i < Cat_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Cat_Sprite[i]);
        }
        animScript.AnimationSpeed = 63f;
        break;
      case 7:
        for (int i = 0; i < Eagle_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Eagle_Sprite[i]);
        }
        animScript.AnimationSpeed = 62f;
        break;
      case 8:
        for (int i = 0; i < Bear_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Bear_Sprite[i]);
        }
        animScript.AnimationSpeed = 32f;
        break;
      case 9:
        for (int i = 0; i < Wolf_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Wolf_Sprite[i]);
        }
        animScript.AnimationSpeed = 63f;
        break;
      case 10:
        for (int i = 0; i < Buffalo_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Buffalo_Sprite[i]);
        }
        animScript.AnimationSpeed = 42f;
        break;
      case 11:
        for (int i = 0; i < Landscape_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Landscape_Sprite[i]);
        }
        animScript.AnimationSpeed = 63f;
        break;
      case 12:
        for (int i = 0; i < Gold_Buffalo.Length; i++)
        {
          animScript.textureArray.Add(Gold_Buffalo[i]);
        }
        animScript.AnimationSpeed = 20f;
        break;
    }
  }

  #region SlotSpin
  //starts the spin process
  private void StartSlots(bool autoSpin = false)
  {
    if (audioController) audioController.PlaySpinButton();

    if (!autoSpin)
    {
      if (AutoSpinRoutine != null)
      {
        StopCoroutine(AutoSpinRoutine);
        StopCoroutine(tweenroutine);
        tweenroutine = null;
        AutoSpinRoutine = null;
      }
    }
    //Clears any banner still on screen — this is both the spin-start reset and, because the auto/free
    //loops route through here, the top-of-chained-iteration reset. It is also how the player skips.
    uiManager.SnapResetWinBanner();
    if (SlotStart_Button) SlotStart_Button.interactable = false;
    if (TempList.Count > 0)
    {
      StopGameAnimation();
    }
    tweenroutine = StartCoroutine(TweenRoutine());
  }

  //manage the Routine for spinning of the slots
  private IEnumerator TweenRoutine()
  {
    if (currentBalance < currentTotalBet && !IsFreeSpin)
    {
      CompareBalance();
      StopAutoSpin();
      yield return new WaitForSeconds(1);
      ToggleButtonGrp(true);
      yield break;
    }
    if (audioController) audioController.PlaySpinAudio(true);
    CheckSpinAudio = true;

    IsSpinning = true;

    ToggleButtonGrp(false);

    if (!IsFreeSpin) TotalWin_text.text = m_Instructions[1];

    m_MainUIMask.enabled = true;

    m_AnimationController.StopAnimation();


    if (!IsTurboOn && !IsFreeSpin && !IsAutoSpin)
    {
      StopSpin_Button.gameObject.SetActive(true);
    }
    if (!IsFreeSpin)
    {
      BalanceDeduction();
    }

    //Pre-size the list so each reel's loop tween lands at its own index (filled by the intro callback).
    alltweens.Clear();
    for (int i = 0; i < numberOfSlots; i++)
      alltweens.Add(null);

    List<Tween> introTweens = new();
    for (int i = 0; i < numberOfSlots; i++)
    {
      introTweens.Add(InitializeTweening(Slot_Transform[i], i));
      // yield return new WaitForSeconds(0.f);   //keep the staggered left-to-right wind-up
    }

    //Wait until every reel's intro slide has finished — all loops are now spinning.
    for (int i = 0; i < introTweens.Count; i++)
      yield return introTweens[i].WaitForCompletion();

    //Let the reels spin freely for a beat before doing anything else (socket request, etc.).
    yield return new WaitForSeconds(0.5f);

    ResetRectSizes();

    //HACK: This will be used when to send the spin instruction to the socket and wait for the socket to receive the request.
    SocketManager.AccumulateResult(BetCounter);
    yield return new WaitUntil(() => SocketManager.isResultdone);

    OrderingUI m_order;
    OrderingUI m_anim_order;
    for (int i = 0; i < Tempimages.Count; i++)
    {
      for (int j = 0; j < Tempimages[i].slotImages.Count; j++)
      {
        //Tempimages[i].slotImages[j].sprite = myImages[m_DemoResponse[j, i]];
        int symbolIndex = int.Parse(SocketManager.resultData.matrix[j][i]);
        Tempimages[i].slotImages[j].sprite = myImages[symbolIndex];
        m_order = new OrderingUI { };
        m_anim_order = new OrderingUI { };
        SlotControl(i, j, symbolIndex, m_order, m_anim_order);
      }
    }

    //yield return new WaitForSeconds(0.5f);
    if (IsTurboOn || IsFreeSpin)
    {
      yield return new WaitForSeconds(0.1f);
    }
    else
    {
      for (int i = 0; i < 10; i++)
      {
        if (StopSpinToggle)
        {
          break;
        }
        yield return new WaitForSeconds(0.1f);
      }
    }

    PrioritizeList();

    for (int i = 0; i < numberOfSlots; i++)
    {
      yield return StopTweening(Slot_Transform[i], i, StopSpinToggle || IsTurboOn);
    }

    if (audioController) audioController.PlaySpinAudio(false);
    //Wait for every landing tween to finish, not just the last one.
    for (int i = 0; i < numberOfSlots; i++)
      yield return alltweens[i].WaitForCompletion();

    StopSpin_Button.gameObject.SetActive(false);
    m_MainUIMask.enabled = false;
    yield return new WaitForSeconds(0.1f);
    StopSpinToggle = false;

    //Snap to exactly RestY: the OutBack overshoot can leave a column mid-bounce, and the next
    //spin's intro reads this Y as its start position.
    for (int i = 0; i < numberOfSlots; i++)
      Slot_Transform[i].localPosition =
          new Vector2(Slot_Transform[i].localPosition.x, RestY);

    //HACK: Kills The Tweens So That They Will Get Ready For Next Spin
    KillAllTweens();

    //The chained-mode loops now wait on the banner itself, so this is just the breath after it clears.
    SpinDelay = SocketManager.resultData.payload.winAmount > 0 ? postWinBannerDelay : noWinSpinDelay;

    BalanceTween?.Kill();

    //HACK: Check For The Result And Activate Animations Accordingly
    //Auto/free/feature spins get one synced pass then clear (no infinite cycle); manual spins loop.
    bool autoContinued = IsAutoSpin || IsFreeSpin || SocketManager.resultData.features.freeSpin.isTriggered;
    m_AnimationController.StartAnimation(SocketManager.resultData.payload.winningCombinations, autoContinued);

    if (SocketManager.resultData.features.freeSpin.wildMultiplier != null & SocketManager.resultData.features.freeSpin.wildMultiplier.Count > 0)
    {
      foreach (var wm in SocketManager.resultData.features.freeSpin.wildMultiplier)
      {
        int x = wm.position[0];
        int y = wm.position[1];
        int multiplier = wm.multiplier;

        Tempimages[y].slotImages[x].transform.GetChild(0).gameObject.SetActive(true);
        m_AnimationController.m_AnimatedSlots[y].slotImages[x].transform.GetChild(0).gameObject.SetActive(true);

        Tempimages[y].slotImages[x].transform.GetChild(0).GetComponent<TMP_Text>().text = multiplier.ToString() +"x";
        m_AnimationController.m_AnimatedSlots[y].slotImages[x].transform.GetChild(0).GetComponent<TMP_Text>().text = multiplier.ToString() + "x";
      }
    }

    if (IsFreeSpin)
    {
      freeSpinAccumulatedWin += SocketManager.resultData.payload.winAmount;
      TotalWin_text.text = freeSpinAccumulatedWin.ToString("F" + UIManager.GetSignificantDecimals(freeSpinAccumulatedWin));
    }
    else if (SocketManager.resultData.payload.winAmount > 0)
      TotalWin_text.text = SocketManager.resultData.payload.winAmount.ToString("F" + UIManager.GetSignificantDecimals(SocketManager.resultData.payload.winAmount));
    else if (SocketManager.resultData.payload.winAmount == 0)
      TotalWin_text.text = "0.00";

    if (Balance_text) Balance_text.text = SocketManager.playerdata.balance.ToString("F" + UIManager.GetSignificantDecimals(SocketManager.playerdata.balance));

    currentBalance = SocketManager.playerdata.balance;

    //Fire-and-forget. Awaiting it here would stall manual spins for the whole banner; the chained-mode
    //loops poll uiManager.isWinAnimating instead.
    uiManager.TriggerWinBanner(SocketManager.resultData.payload.winAmount, currentTotalBet);

    if (SocketManager.resultData.features.freeSpin.isTriggered)
    {
      //IsSpinning stays TRUE across the whole handoff. StopAutoSpinCoroutine waits on !IsSpinning and
      //then kills this coroutine — dropping it early would let a stop-autospin press during the banner
      //tear TweenRoutine down before FreeSpinProcess runs, silently losing the awarded free spins.
      yield return new WaitWhile(() => uiManager.isWinAnimating);
      yield return new WaitWhile(() => m_AnimationController.IsWinPassPlaying);

      if (IsFreeSpin)
      {
        IsFreeSpin = false;
        if (FreeSpinRoutine != null)
        {
          StopCoroutine(FreeSpinRoutine);
          FreeSpinRoutine = null;
        }
      }
      else
      {
        yield return StartCoroutine(BuffaloRushRoutine());
        freeSpinAccumulatedWin = 0;
      }
      uiManager.FreeSpinProcess((int)SocketManager.resultData.features.freeSpin.freeSpinCount);
      if (IsAutoSpin)
      {
        WasAutoSpinOn = true;
        IsSpinning = false;
        StopAutoSpin();
        yield return new WaitForSeconds(0.1f);
      }
      IsSpinning = false;
      yield break;
    }

    //Manual spins re-arm immediately: clicking Spin again is how the player skips the banner.
    if (!IsAutoSpin && !IsFreeSpin) ToggleButtonGrp(true);
    IsSpinning = false;
  }

  private IEnumerator BuffaloRushRoutine()
  {
    m_BuffaloRush.SetActive(true);
    audioController.PlayBull_Audio();
    m_BuffaloRush.GetComponent<ImageAnimation>().StartAnimation();
    yield return new WaitForSeconds(2.2f);
    m_BuffaloRush.GetComponent<ImageAnimation>().StopAnimation();
    m_BuffaloRush.SetActive(false);
    yield return new WaitForSeconds(0.2f);
    StopCoroutine(BuffaloRushRoutine());
  }

  private void SlotControl(int i, int j, int index, OrderingUI m_order, OrderingUI m_anim_order)
  {
    m_AnimationController.m_AnimatedSlots[i].slotImages[j].sprite = myImages[index];
    PopulateAnimationSprites(m_AnimationController.m_AnimatedSlots[i].slotImages[j].gameObject.GetComponent<ImageAnimation>(), index);

    Vector3 temp_Position;
    Vector3 temp_Anim_Position;

    if (index >= 6 && index <= 12)
    {
      m_order = new OrderingUI
      {
        m_Priority = Priority.Gold_Buffalo,
        child_index = Tempimages[i].slotImages[j].transform.GetSiblingIndex(),
        this_parent = Tempimages[i].slotImages[j].transform.parent,
        current_object = Tempimages[i].slotImages[j].transform,
        current_position = Tempimages[i].slotImages[j].transform.localPosition
      };

      m_anim_order = new OrderingUI
      {
        m_Priority = Priority.Wolf,
        child_index = m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.GetSiblingIndex(),
        this_parent = m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.parent,
        current_object = m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform,
        current_position = m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition
      };

      switch (index)
      {
        case (6):
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(245, 200);
          m_order.m_Priority = Priority.Lion;
          m_anim_order.m_Priority = Priority.Lion;
          break;
        case (7):
          m_order.m_Priority = Priority.Eagle;
          m_anim_order.m_Priority = Priority.Eagle;
          break;
        case (8):
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(245, 195);
          m_order.m_Priority = Priority.Bear;
          m_anim_order.m_Priority = Priority.Bear;
          break;
        case (9):
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(245, 245);
          //m_UI_Order.Add();

          //m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition -= Vector3.up * 20;

          m_order.m_Priority = Priority.Wolf;
          m_anim_order.m_Priority = Priority.Wolf;
          break;
        case (10):
          Tempimages[i].slotImages[j].rectTransform.sizeDelta = new Vector2(290, 230);//297,240
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(300, 230);

          //Tempimages[i].slotImages[j].transform.localPosition -= Vector3.up * 28;
          //m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition -= Vector3.up * 16;

          temp_Position = Tempimages[i].slotImages[j].transform.localPosition;
          temp_Anim_Position = m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition;
          temp_Position.y -= 15;
          temp_Anim_Position.y -= 15;
          Tempimages[i].slotImages[j].transform.localPosition = temp_Position;
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition = temp_Anim_Position;

          m_order.m_Priority = Priority.Buffalo;
          m_anim_order.m_Priority = Priority.Buffalo;
          break;
        case (11):
          Tempimages[i].slotImages[j].rectTransform.sizeDelta = new Vector2(270, 230);//297,240 268, 210
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(270, 230);

          //Tempimages[i].slotImages[j].transform.localPosition += Vector3.up * 28;
          //m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition += Vector3.up * 28;
          temp_Position = Tempimages[i].slotImages[j].transform.localPosition;
          temp_Anim_Position = m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition;
          temp_Position.y += 18;
          temp_Anim_Position.y += 18;
          Tempimages[i].slotImages[j].transform.localPosition = temp_Position;
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.localPosition = temp_Anim_Position;

          m_order.m_Priority = Priority.Landscape;
          m_anim_order.m_Priority = Priority.Landscape;
          break;
        case (12):
          Tempimages[i].slotImages[j].rectTransform.sizeDelta = new Vector2(320, 320);//297,240 280, 220
          m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(320, 320);
          m_order.m_Priority = Priority.Gold_Buffalo;
          m_anim_order.m_Priority = Priority.Gold_Buffalo;
          //Tempimages[i].slotImages[j].transform.SetAsLastSibling();
          break;
        default:
          break;
          //m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(297, 240);

      }
      m_UI_Order.Add(m_order);
      m_UI_Order.Add(m_anim_order);
    }
  }

  private void BalanceDeduction()
  {
    double bet = 0;
    double balance = 0;
    try
    {
      bet = double.Parse(TotalBet_text.text);
    }
    catch (Exception e)
    {
      Debug.Log("Error while conversion " + e.Message);
    }

    try
    {
      balance = double.Parse(Balance_text.text);
    }
    catch (Exception e)
    {
      Debug.Log("Error while conversion " + e.Message);
    }
    double initAmount = balance;

    balance = balance - bet;
    int decimalPlaces = UIManager.GetSignificantDecimals(balance);
    BalanceTween = DOTween.To(() => initAmount, (val) => initAmount = val, balance, 0.8f).OnUpdate(() =>
    {
      if (Balance_text) Balance_text.text = initAmount.ToString("F" + decimalPlaces);
    });
    currentBalance = balance;
  }

  // internal void CheckBonusGame()
  // {
  //     //_bonusManager.StartBonus((int)SocketManager.resultData.BonusStopIndex);
  // }

  #endregion

  internal void CallCloseSocket()
  {
    StartCoroutine(SocketManager.CloseSocket());
  }


  void ToggleButtonGrp(bool toggle)
  {

    if (SlotStart_Button) SlotStart_Button.interactable = toggle;
    if (MaxBet_Button) MaxBet_Button.interactable = toggle;
    if (AutoSpin_Button) AutoSpin_Button.interactable = toggle;
    if (BetMinus_Button) BetMinus_Button.interactable = toggle;
    if (BetPlus_Button) BetPlus_Button.interactable = toggle;
    if (m_BetButton) m_BetButton.interactable = toggle;
    if (uiManager.Menu_Button) uiManager.Menu_Button.interactable = toggle;
  }

  //start the icons animation
  private void StartGameAnimation(GameObject animObjects)
  {
    ImageAnimation temp = animObjects.GetComponent<ImageAnimation>();
    if (temp.textureArray.Count > 0)
    {
      temp.StartAnimation();
      TempList.Add(temp);
    }
  }

  //stop the icons animation
  private void StopGameAnimation()
  {
    for (int i = 0; i < TempList.Count; i++)
    {
      TempList[i].StopAnimation();
    }
    TempList.Clear();
    TempList.TrimExcess();
  }

  private void ResetRectSizes()
  {
    for (int i = 0; i < Tempimages.Count; i++)
    {
      for (int j = 0; j < Tempimages[i].slotImages.Count; j++)
      {
        Tempimages[i].slotImages[j].rectTransform.sizeDelta = new Vector2(242, 185);
        m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(242, 185);
        Tempimages[i].slotImages[j].transform.GetChild(0).gameObject.SetActive(false);
        m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.GetChild(0).gameObject.SetActive(false);
      }
    }

    foreach (var i in m_UI_Order)
    {
      //i.current_object.SetParent(i.this_parent);
      i.current_object.SetSiblingIndex(i.child_index);
      i.current_object.localPosition = i.current_position;
    }

    m_UI_Order.Clear();
    m_UI_Order.TrimExcess();
  }

  private void PrioritizeList()
  {
    //m_UI_Order.Sort((x, y) => y.m_Priority.CompareTo(x.m_Priority)); //Descending
    m_UI_Order.Sort((x, y) => x.m_Priority.CompareTo(y.m_Priority)); //Asscending

    foreach (var i in m_UI_Order)
    {
      i.current_object.SetAsLastSibling();
    }
  }

  #region TweeningCode
  private Tween InitializeTweening(Transform slotTransform, int index)
  {
    Sequence seq = DOTween.Sequence();
    float startY = slotTransform.localPosition.y;

    //1) One-time intro slide: drop from wherever it is down to the bottom.
    seq.Append(slotTransform.DOLocalMoveY(SpinBottomY, DurationFor(startY, SpinBottomY))
        .SetEase(Ease.InBack));

    //2) The instant that finishes, teleport to the top and start the infinite loop.
    seq.AppendCallback(() =>
    {
      slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);

      Tweener tweener = slotTransform
          .DOLocalMoveY(SpinBottomY, DurationFor(SpinTopY, SpinBottomY))
          .SetLoops(-1, LoopType.Restart)   //infinite loop: top -> bottom, snap back to top, repeat
          .SetEase(Ease.Linear);            //constant speed = seamless scroll

      //Bind by REEL INDEX, never by completion order, so StopTweening/KillAllTweens
      //always act on the reel they were handed.
      alltweens[index] = tweener;
    });

    return seq; //caller waits on this to know the intro is done
  }

  private IEnumerator StopTweening(Transform slotTransform, int index, bool isStop)
  {
    alltweens[index]?.Kill();  //stop the infinite loop

    //Teleport to the top so the landing slide always covers the full window (consistent feel).
    slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, SpinTopY);

    //Replace the loop entry with the landing tween so callers can await alltweens[index].
    alltweens[index] = slotTransform.DOLocalMoveY(RestY, DurationFor(SpinTopY, RestY))
        .SetEase(Ease.OutBack, 1.2f);  //overshoot then settle onto RestY

    if (!isStop)
    {
      yield return new WaitForSeconds(0.4f);
    }
    else
    {
      yield return null;
    }
  }

  private void KillAllTweens()
  {
    for (int i = 0; i < alltweens.Count; i++)
    {
      alltweens[i]?.Kill();
    }
    alltweens.Clear();

  }
  #endregion

}

[Serializable]
public class SlotImage
{
  public List<Image> slotImages = new List<Image>(10);
}

[Serializable]
public struct OrderingUI
{
  public Priority m_Priority;
  public int child_index;
  public Transform this_parent;
  public Transform current_object;
  public Vector3 current_position;
}

[Serializable]
public enum Priority
{
  Gold_Buffalo = 7,
  Landscape = 6,
  Buffalo = 5,
  Bear = 4,
  Wolf = 3,
  Lion = 2,
  Eagle = 1
}
