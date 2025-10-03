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

    [SerializeField] private Button Turbo_Button;
    [SerializeField] private Button StopSpin_Button;

    [SerializeField] Sprite[] TurboToggleSprites;

    private bool StopSpinToggle;
    private float SpinDelay = 0.2f;
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

    [SerializeField]
    private GameObject Image_Prefab;    //icons prefab

    [SerializeField]
    private PayoutCalculation PayCalculator;

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
    internal bool CheckPopups = false;

    private int BetCounter = 0;
    private double currentBalance = 0;
    private double currentTotalBet = 0;
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

        if (Turbo_Button) Turbo_Button.onClick.RemoveAllListeners();
        if (Turbo_Button) Turbo_Button.onClick.AddListener(() =>
        {
            audioController.PlayNormalButton();
            TurboToggle();
        });

        if (FSBoard_Object) FSBoard_Object.SetActive(false);

        tweenHeight = (myImages.Length * IconSizeFactor) - 280;
    }

    void TurboToggle()
    {
        if (IsTurboOn)
        {
            IsTurboOn = false;
            Turbo_Button.GetComponent<ImageAnimation>().StopAnimation();
            Turbo_Button.image.sprite = TurboToggleSprites[0];
            Turbo_Button.image.color = new Color(0.86f, 0.86f, 0.86f, 1);
        }
        else
        {
            IsTurboOn = true;
            Turbo_Button.GetComponent<ImageAnimation>().StartAnimation();
            Turbo_Button.image.color = new Color(1, 1, 1, 1);
        }
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
            yield return new WaitForSeconds(SpinDelay);
        }
        WasAutoSpinOn = false;
    }

    private IEnumerator StopAutoSpinCoroutine()
    {
        yield return new WaitUntil(() => !IsSpinning);
        ToggleButtonGrp(true);
        if (AutoSpinRoutine != null || tweenroutine != null)
        {
            StopCoroutine(AutoSpinRoutine);
            StopCoroutine(tweenroutine);
            tweenroutine = null;
            AutoSpinRoutine = null;
            StopCoroutine(StopAutoSpinCoroutine());
        }
        //IsAutoSpin = false;
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

    #region LinesCalculation
    //Fetch Lines from backend
    internal void FetchLines(string LineVal, int count)
    {
        y_string.Add(count + 1, LineVal);
        StaticLine_Texts[count].text = (count + 1).ToString();
        StaticLine_Objects[count].SetActive(true);
    }

    //Generate Static Lines from button hovers
    internal void GenerateStaticLine(TMP_Text LineID_Text)
    {
        DestroyStaticLine();
        int LineID = 1;
        try
        {
            LineID = int.Parse(LineID_Text.text);
        }
        catch (Exception e)
        {
            Debug.Log("Exception while parsing " + e.Message);
        }
        List<int> y_points = null;
        y_points = y_string[LineID]?.Split(',')?.Select(Int32.Parse)?.ToList();
        PayCalculator.GeneratePayoutLinesBackend(y_points, y_points.Count, true);
    }

    //Destroy Static Lines from button hovers
    internal void DestroyStaticLine()
    {
        PayCalculator.ResetStaticLine();
    }
    #endregion

    internal void OnBetClicked(int Bet, double Value)
    {
        if (audioController) audioController.PlayNormalButton();
        BetCounter = Bet;
        if (LineBet_text) LineBet_text.text = Value.ToString();
        if (TotalBet_text) TotalBet_text.text = (Value).ToString();
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
        if (LineBet_text) LineBet_text.text = SocketManager.initialData.Bets[BetCounter].ToString();
        if (TotalBet_text) TotalBet_text.text = (SocketManager.initialData.Bets[BetCounter]).ToString();
        if (TotalWin_text) TotalWin_text.text = "0.000";
        if (Balance_text) Balance_text.text = SocketManager.playerdata.Balance.ToString("f3");
        currentBalance = SocketManager.playerdata.Balance;
        currentTotalBet = SocketManager.initialData.Bets[BetCounter];
        //_bonusManager.PopulateWheel(SocketManager.bonusdata);
        CompareBalance();
        uiManager.AssignBetButtons(SocketManager.initialData.Bets);
        uiManager.InitialiseUIData(SocketManager.initUIData.AbtLogo.link, SocketManager.initUIData.AbtLogo.logoSprite, SocketManager.initUIData.ToULink, SocketManager.initUIData.PopLink, SocketManager.initUIData.paylines);
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
                animScript.AnimationSpeed = 60f;
                break;
            case 7:
                for (int i = 0; i < Eagle_Sprite.Length; i++)
                {
                    animScript.textureArray.Add(Eagle_Sprite[i]);
                }
                animScript.AnimationSpeed = 60f;
                break;
            case 8:
                for (int i = 0; i < Bear_Sprite.Length; i++)
                {
                    animScript.textureArray.Add(Bear_Sprite[i]);
                }
                animScript.AnimationSpeed = 30f;
                break;
            case 9:
                for (int i = 0; i < Wolf_Sprite.Length; i++)
                {
                    animScript.textureArray.Add(Wolf_Sprite[i]);
                }
                animScript.AnimationSpeed = 60f;
                break;
            case 10:
                for (int i = 0; i < Buffalo_Sprite.Length; i++)
                {
                    animScript.textureArray.Add(Buffalo_Sprite[i]);
                }
                animScript.AnimationSpeed = 40f;
                break;
            case 11:
                for (int i = 0; i < Landscape_Sprite.Length; i++)
                {
                    animScript.textureArray.Add(Landscape_Sprite[i]);
                }
                animScript.AnimationSpeed = 60f;
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
        WinningsAnim(false);
        if (SlotStart_Button) SlotStart_Button.interactable = false;
        if (TempList.Count > 0)
        {
            StopGameAnimation();
        }
        PayCalculator.ResetLines();
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

        TotalWin_text.text = m_Instructions[1];

        m_MainUIMask.enabled = true;

        m_AnimationController.StopAnimation();


        if (!IsTurboOn && !IsFreeSpin && !IsAutoSpin)
        {
            StopSpin_Button.gameObject.SetActive(true);
        }

        for (int i = 0; i < numberOfSlots; i++)
        {
            InitializeTweening(Slot_Transform[i]);
            yield return new WaitForSeconds(0.1f);
        }

        ResetRectSizes();

        if (!IsFreeSpin)
        {
            BalanceDeduction();
        }

        //HACK: This will be used when to send the spin instruction to the socket and wait for the socket to receive the request.
        SocketManager.AccumulateResult(BetCounter);
        yield return new WaitUntil(() => SocketManager.isResultdone);

        //Populate The Tempimages To Show The Result Images.
        //for (int j = 0; j < SocketManager.resultData.ResultReel.Count; j++)
        //{
        //    List<int> resultnum = SocketManager.resultData.FinalResultReel[j]?.Split(',')?.Select(Int32.Parse)?.ToList();
        //    for (int i = 0; i < 5; i++)
        //    {
        //        if (images[i].slotImages[images[i].slotImages.Count - 5 + j]) images[i].slotImages[images[i].slotImages.Count - 5 + j].sprite = myImages[resultnum[i]];
        //        PopulateAnimationSprites(images[i].slotImages[images[i].slotImages.Count - 5 + j].gameObject.GetComponent<ImageAnimation>(), resultnum[i]);
        //    }
        //}
        OrderingUI m_order;
        OrderingUI m_anim_order;
        for(int i = 0; i < Tempimages.Count; i++)
        {
            for(int j = 0; j < Tempimages[i].slotImages.Count; j++)
            {
                //Tempimages[i].slotImages[j].sprite = myImages[m_DemoResponse[j, i]];
                Tempimages[i].slotImages[j].sprite = myImages[SocketManager.resultData.resultMatrix[j][i]];
                m_order = new OrderingUI { };
                m_anim_order = new OrderingUI { };
                SlotControl(i, j, SocketManager.resultData.resultMatrix[j][i], m_order, m_anim_order);
            }
        }

        //yield return new WaitForSeconds(0.5f);
        if (IsTurboOn || IsFreeSpin)
        {
            yield return new WaitForSeconds(0.1f);
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.1f);
                if (StopSpinToggle)
                {
                    break;
                }
            }
            StopSpin_Button.gameObject.SetActive(false);
        }

        PrioritizeList();

        for (int i = 0; i < numberOfSlots; i++)
        {
            yield return StopTweening(6, Slot_Transform[i], i, StopSpinToggle);
        }

        if (audioController) audioController.PlaySpinAudio(false);

        m_MainUIMask.enabled = false;

        yield return new WaitForSeconds(0.1f);

        StopSpinToggle = false;
        yield return alltweens[^1].WaitForCompletion();

        //HACK: Kills The Tweens So That They Will Get Ready For Next Spin
        KillAllTweens();

        if (SocketManager.playerdata.currentWining > 0)
        {
            SpinDelay = 1.5f;
        }
        else
        {
            SpinDelay = 0.2f;
        }

        CheckPopups = true;

        BalanceTween?.Kill();

        //HACK: Instruction Updated After Spin Ends If Wins then it shouldn't be updated other wise it will prompt 0th index
        TotalWin_text.text = m_Instructions[0];

        //HACK: Check For The Result And Activate Animations Accordingly
        m_AnimationController.StartAnimation(SocketManager.resultData.symbolsToEmit);

        if (SocketManager.resultData.WildMultipliers.Count > 0)
        {
            //m_AnimationController.FreeSpinCoinAnimate();
            List<List<int>> m_multiplier = SocketManager.resultData.WildMultipliers;
            foreach(var i in m_multiplier)
            {
                Tempimages[i[1]].slotImages[i[0]].transform.GetChild(0).gameObject.SetActive(true);
                m_AnimationController.m_AnimatedSlots[i[1]].slotImages[i[0]].transform.GetChild(0).gameObject.SetActive(true);
                Tempimages[i[1]].slotImages[i[0]].transform.GetChild(0).GetComponent<TMP_Text>().text = (i[2]).ToString();
                m_AnimationController.m_AnimatedSlots[i[1]].slotImages[i[0]].transform.GetChild(0).GetComponent<TMP_Text>().text = (i[2]).ToString();

                yield return new WaitForSeconds(0.5f);

                DOTweenUIManager.Instance.Jump(Tempimages[i[1]].slotImages[i[0]].transform.GetChild(0).GetComponent<RectTransform>(), 50f, 2, 0.6f);
                DOTweenUIManager.Instance.Jump(m_AnimationController.m_AnimatedSlots[i[1]].slotImages[i[0]].transform.GetChild(0).GetComponent<RectTransform>(), 50f, 2, 0.6f);
            }

            yield return new WaitForSeconds(1.4f);
        }


        if (TotalWin_text) TotalWin_text.text = SocketManager.playerdata.currentWining.ToString("f3");

        if (Balance_text) Balance_text.text = SocketManager.playerdata.Balance.ToString("f3");

        currentBalance = SocketManager.playerdata.Balance;

        //if (SocketManager.resultData.jackpot > 0)
        //{
        //    uiManager.PopulateWin(4, SocketManager.resultData.jackpot);
        //    yield return new WaitUntil(() => !CheckPopups);
        //    CheckPopups = true;
        //}

        if (SocketManager.resultData.isBonus)
        {
            CheckBonusGame();
        }
        else
        {
            CheckWinPopups();
        }

        yield return new WaitUntil(() => !CheckPopups);
        if (!IsAutoSpin && !IsFreeSpin && !SocketManager.resultData.isFreeSpin)
        {
            ToggleButtonGrp(true);
            IsSpinning = false;
        }
        else
        {
            //yield return new WaitForSeconds(2f);
            IsSpinning = false;
        }

        //if(SocketManager.resultData.fsWinningSymbols.Count > 0)
        //    FreeSpinCoinAnimate();

        //yield return new WaitForSeconds(1f);

        if (SocketManager.resultData.isFreeSpin)
        {
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
            }
            uiManager.FreeSpinProcess((int)SocketManager.resultData.freeSpinCount, (int)SocketManager.resultData.isNewAdded);
            if (IsAutoSpin)
            {
                WasAutoSpinOn = true;
                StopAutoSpin();
                yield return new WaitForSeconds(0.1f);
            }
        }
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

        BalanceTween = DOTween.To(() => initAmount, (val) => initAmount = val, balance, 0.8f).OnUpdate(() =>
        {
            if (Balance_text) Balance_text.text = initAmount.ToString("f3");
        });
        currentBalance = balance;
    }

    internal void CheckWinPopups()
    {
        if (SocketManager.resultData.WinAmout >= currentTotalBet * 5 && SocketManager.resultData.WinAmout < currentTotalBet * 10)
        {
            audioController.PlayWin(Sound.BigWin);
            uiManager.PopulateWin(1, SocketManager.resultData.WinAmout);
        }
        else if (SocketManager.resultData.WinAmout >= currentTotalBet * 10)
        {
            audioController.PlayWin(Sound.MegaWin);
            uiManager.PopulateWin(2, SocketManager.resultData.WinAmout);
        }
        else if (SocketManager.resultData.WinAmout > 0 && currentTotalBet<SocketManager.resultData.WinAmout)
        {
            audioController.PlayWin(Sound.NormalWin);
            uiManager.PopulateWin(3, SocketManager.resultData.WinAmout);
        }
        else
        {
            CheckPopups = false;
        }
    }

    internal void CheckBonusGame()
    {
        //_bonusManager.StartBonus((int)SocketManager.resultData.BonusStopIndex);
    }

    private void WinningsAnim(bool IsStart)
    {
        if (IsStart)
        {
            WinTween = TotalWin_text.gameObject.GetComponent<RectTransform>().DOScale(new Vector2(1.5f, 1.5f), 1f).SetLoops(-1, LoopType.Yoyo).SetDelay(0);
        }
        else
        {
            WinTween.Kill();
            TotalWin_text.gameObject.GetComponent<RectTransform>().localScale = Vector3.one;
        }
    }

    #endregion

    internal void CallCloseSocket()
    {
        SocketManager.CloseSocket();
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
        if(temp.textureArray.Count > 0)
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
        for(int i = 0; i < Tempimages.Count; i++)
        {
            for(int j = 0; j < Tempimages[i].slotImages.Count; j++)
            {
                Tempimages[i].slotImages[j].rectTransform.sizeDelta = new Vector2(242, 185);
                m_AnimationController.m_AnimatedSlots[i].slotImages[j].rectTransform.sizeDelta = new Vector2(242, 185);
                Tempimages[i].slotImages[j].transform.GetChild(0).gameObject.SetActive(false);
                m_AnimationController.m_AnimatedSlots[i].slotImages[j].transform.GetChild(0).gameObject.SetActive(false);
            }
        }

        foreach(var i in m_UI_Order)
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

        foreach(var i in m_UI_Order)
        {
            i.current_object.SetAsLastSibling();
        }

    }

    #region TweeningCode
    private void InitializeTweening(Transform slotTransform)
    {
        slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, 0);
        Tweener tweener = slotTransform.DOLocalMoveY(-tweenHeight, 0.2f).SetLoops(-1, LoopType.Restart).SetDelay(0);
        tweener.Play();
        alltweens.Add(tweener);
    }

    private IEnumerator StopTweening(int reqpos, Transform slotTransform, int index, bool isStop)
    {
        alltweens[index].Pause();
        int tweenpos = (reqpos * IconSizeFactor) - IconSizeFactor;
        slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, 0);
        alltweens[index] = slotTransform.DOLocalMoveY(-tweenpos + 100, 0.5f).SetEase(Ease.OutElastic);
        if (!isStop)
        {
            yield return new WaitForSeconds(0.2f);
        }
        else
        {
            yield return null;
        }
    }

    private void KillAllTweens()
    {
        for (int i = 0; i < numberOfSlots; i++)
        {
            alltweens[i].Kill();
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