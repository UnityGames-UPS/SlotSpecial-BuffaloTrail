using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using System;
using UnityEngine.Networking;

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

    [Header("About Popup")]
    [SerializeField]
    private GameObject AboutPopup_Object;
    [SerializeField]
    private Button AboutExit_Button;
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

    [Header("Win Popup")]
    [SerializeField]
    private Sprite BigWin_Sprite;
    [SerializeField]
    private Sprite HugeWin_Sprite;
    [SerializeField]
    private Sprite MegaWin_Sprite;
    [SerializeField]
    private Sprite Jackpot_Sprite;
    [SerializeField]
    private Image Win_Image;
    [SerializeField]
    private GameObject WinPopup_Object;
    [SerializeField]
    private TMP_Text Win_Text;
    [SerializeField]
    private Sprite[] m_BigWin;
    [SerializeField]
    private Sprite[] m_MegaWin;
    [SerializeField]
    private ImageAnimation m_WinImage;
    [SerializeField]
    private ImageAnimation m_WinAnimation;

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

    [SerializeField] private Button SkipWinAnimation;

    [SerializeField]
    private List<float> m_DummyBetValues;

    private bool isMusic = true;
    private bool isSound = true;
    private bool isExit = false;
    private bool MenuOpen = false;

    internal int FreeSpins;
    private Tween WinPopupTextTween;
    private Tween ClosePopupTween;


    private void Start()
    {

        if (Menu_Button) Menu_Button.onClick.RemoveAllListeners();
        if (Menu_Button) Menu_Button.onClick.AddListener(OpenMenu);

        if (Exit_Button) Exit_Button.onClick.RemoveAllListeners();
        if (Exit_Button) Exit_Button.onClick.AddListener(CloseMenu);

        //if (About_Button) About_Button.onClick.RemoveAllListeners();
        //if (About_Button) About_Button.onClick.AddListener(delegate { OpenPopup(AboutPopup_Object); });

        //if (AboutExit_Button) AboutExit_Button.onClick.RemoveAllListeners();
        //if (AboutExit_Button) AboutExit_Button.onClick.AddListener(delegate { ClosePopup(AboutPopup_Object); });

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
        if (GameExit_Button) GameExit_Button.onClick.AddListener(delegate { 
            OpenPopup(QuitPopup_Object);
            CloseMenu();
            Debug.Log("Quit event: pressed Big_X button");
            
            });

        if (NoQuit_Button) NoQuit_Button.onClick.RemoveAllListeners();
        if (NoQuit_Button) NoQuit_Button.onClick.AddListener(delegate { 
            ClosePopup(QuitPopup_Object); 
            Debug.Log("quit event: pressed NO Button ");
             });

        if (CrossQuit_Button) CrossQuit_Button.onClick.RemoveAllListeners();
        if (CrossQuit_Button) CrossQuit_Button.onClick.AddListener(delegate { 
            ClosePopup(QuitPopup_Object); 
            Debug.Log("quit event: pressed Small_X Button ");
            
             });

        if (LBExit_Button) LBExit_Button.onClick.RemoveAllListeners();
        if (LBExit_Button) LBExit_Button.onClick.AddListener(delegate { ClosePopup(LBPopup_Object); });

        if (YesQuit_Button) YesQuit_Button.onClick.RemoveAllListeners();
        if (YesQuit_Button) YesQuit_Button.onClick.AddListener(delegate{
            CallOnExitFunction();
            Debug.Log("quit event: pressed YES Button ");
            
            });

        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.RemoveAllListeners();
        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.AddListener(delegate { CallOnExitFunction(); socketManager.ReactNativeCallOnFailedToConnect(); }); //BackendChanges

        if (CloseAD_Button) CloseAD_Button.onClick.RemoveAllListeners();
        if (CloseAD_Button) CloseAD_Button.onClick.AddListener(CallOnExitFunction);

        if (FreeSpin_Button) FreeSpin_Button.onClick.RemoveAllListeners();
        if (FreeSpin_Button) FreeSpin_Button.onClick.AddListener(delegate{ StartFreeSpins(FreeSpins); });

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

        if (SkipWinAnimation) SkipWinAnimation.onClick.RemoveAllListeners();
        if (SkipWinAnimation) SkipWinAnimation.onClick.AddListener(SkipWin);

        //AssignBetButtons(m_DummyBetValues);

    }

    #region [[===BET BUTTONS HANDLING===]]
    internal void AssignBetButtons(List<double> m_bet_items)
    {
        for (int i = 0; i < m_BetButtons.Count; i++)
        {
            Button m_Temp_Bet = m_BetButtons[i];
            if(i < m_bet_items.Count)
            {
                m_Temp_Bet.gameObject.SetActive(true);
                m_Temp_Bet.transform.GetChild(0).GetComponent<TMP_Text>().text = m_bet_items[i].ToString("f2");
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

    void SkipWin()
    {
        Debug.Log("Skip win called");
        if (ClosePopupTween != null)
        {
            ClosePopupTween.Kill();
            ClosePopupTween = null;
        }
        if (WinPopupTextTween != null)
        {
            WinPopupTextTween.Kill();
            WinPopupTextTween = null;
        }
        ClosePopup(WinPopup_Object);
        slotManager.CheckPopups = false;
    }

    internal void LowBalPopup()
    {
        OpenPopup(LBPopup_Object);
    }

    internal void DisconnectionPopup(bool isReconnection)
    {
        //if(isReconnection)
        //{
        //    OpenPopup(ReconnectPopup_Object);
        //}
        //else
        //{
        //    ClosePopup(ReconnectPopup_Object);
        //}

        if (!isExit)
        {
            OpenPopup(DisconnectPopup_Object);
        }
    }

    internal void PopulateWin(int value, double amount)
    {
        m_WinAnimation.StartAnimation();
        m_WinImage.textureArray.Clear();
        m_WinImage.textureArray.TrimExcess();
        m_WinImage.gameObject.SetActive(false);
        bool winToActive = false;

        switch(value)
        {
            case 1:
                winToActive = true;
                foreach(var i in m_BigWin)
                {
                    m_WinImage.textureArray.Add(i);
                }
                break;
            case 2:
                winToActive = true;
                foreach(var i in m_MegaWin)
                {
                    m_WinImage.textureArray.Add(i);
                }
                break;
            case 3:
                winToActive = false;
                break;
        }

        if (winToActive)
        {
            m_WinImage.gameObject.SetActive(true);
            m_WinImage.StartAnimation();
        }
        StartPopupAnim(amount);
    }

    private void StartFreeSpins(int spins)
    {
        if (MainPopup_Object) MainPopup_Object.SetActive(false);
        if (FreeSpinPopup_Object) FreeSpinPopup_Object.SetActive(false);
        slotManager.FreeSpin(spins);
    }

    internal void FreeSpinProcess(int spins, int newSpins)
    {
        int ExtraSpins = spins - FreeSpins;
        FreeSpins = spins;
        Debug.Log(ExtraSpins);
        if (FreeSpinPopup_Object) FreeSpinPopup_Object.SetActive(true);
        if (Free_Text) Free_Text.text = ExtraSpins.ToString() + " Free spins awarded.";
        if (MainPopup_Object) MainPopup_Object.SetActive(true);
        DOVirtual.DelayedCall(2f, () => {
            StartFreeSpins(spins);
        });
    }

    private void StartPopupAnim(double amount)
    {
        double initAmount = 0.00;
        if (WinPopup_Object) WinPopup_Object.SetActive(true);
        if (MainPopup_Object) MainPopup_Object.SetActive(true);
        float delay=1.4f;
        float closeDelay=1.7f;
        if(slotManager.IsTurboOn){
            delay=.7f;
            closeDelay=1f;
        }
        WinPopupTextTween = DOTween.To(() => initAmount, (val) => initAmount = val, (double)amount, delay).OnUpdate(() =>
        {
            if (Win_Text) Win_Text.text = initAmount.ToString("F3");
        });

        ClosePopupTween = DOVirtual.DelayedCall(closeDelay, () =>
        {
            ClosePopup(WinPopup_Object);
            slotManager.CheckPopups = false;
        });
    }

    internal void ADfunction()
    {
        OpenPopup(ADPopup_Object); 
    }

    internal void InitialiseUIData(string SupportUrl, string AbtImgUrl, string TermsUrl, string PrivacyUrl, Paylines symbolsText)
    {
        if (Support_Button) Support_Button.onClick.RemoveAllListeners();
        if (Support_Button) Support_Button.onClick.AddListener(delegate { UrlButtons(SupportUrl); });

        if (Terms_Button) Terms_Button.onClick.RemoveAllListeners();
        if (Terms_Button) Terms_Button.onClick.AddListener(delegate { UrlButtons(TermsUrl); });

        if (Privacy_Button) Privacy_Button.onClick.RemoveAllListeners();
        if (Privacy_Button) Privacy_Button.onClick.AddListener(delegate { UrlButtons(PrivacyUrl); });

        StartCoroutine(DownloadImage(AbtImgUrl));
        PopulateSymbolsPayout(symbolsText);
    }

    private void PopulateSymbolsPayout(Paylines paylines)
    {
        for (int i = 0; i < SymbolsText.Length; i++)
        {
            string text = null;
            if (paylines.symbols[i].Multiplier[0][0] != 0)
            {
                text += "<color=yellow>5x - </color>" + paylines.symbols[i].Multiplier[0][0] + "x";
            }
            if (paylines.symbols[i].Multiplier[1][0] != 0)
            {
                text += "<color=yellow>\n4x - </color>" + paylines.symbols[i].Multiplier[1][0] + "x";
            }
            if (paylines.symbols[i].Multiplier[2][0] != 0)
            {
                text += "<color=yellow>\n3x - </color>" + paylines.symbols[i].Multiplier[2][0] + "x";
            }
            if(paylines.symbols[i].Multiplier[3][0] != 0)
            {
                text += "<color=yellow>\n2x - </color>" + paylines.symbols[i].Multiplier[3][0] + "x";
            }
            if (SymbolsText[i]) SymbolsText[i].text = text;
        }

        for (int i = 0; i < paylines.symbols.Count; i++)
        {
            if (paylines.symbols[i].Name.ToUpper() == "FREESPIN")
            {
                //if (FreeSpin_Text) FreeSpin_Text.text = paylines.symbols[i].description.ToString();
                string freespintext = $"Free game \n2x {paylines.symbols[i].Multiplier[3][1]} FREE SPINS \n3x {paylines.symbols[i].Multiplier[2][1]} FREE SPINS \n4x {paylines.symbols[i].Multiplier[1][1]} FREE SPINS \n5x {paylines.symbols[i].Multiplier[0][1]} FREE SPINS";
                if (FreeSpin_Text) FreeSpin_Text.text = freespintext;
            }
            if (paylines.symbols[i].Name.ToUpper() == "SCATTER")
            {
                if (Scatter_Text) Scatter_Text.text = paylines.symbols[i].description.ToString();
            }
            if (paylines.symbols[i].Name.ToUpper() == "JACKPOT")
            {
                if (Jackpot_Text) Jackpot_Text.text = paylines.symbols[i].description.ToString();
            }
            if (paylines.symbols[i].Name.ToUpper() == "BONUS")
            {
                if (Bonus_Text) Bonus_Text.text = paylines.symbols[i].description.ToString();
            }
            if (paylines.symbols[i].Name.ToUpper() == "WILD")
            {
                if (Wild_Text) Wild_Text.text = paylines.symbols[i].description.ToString();
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
            if(audioController) audioController.MuteUnmute(Sound.Sound, true, true);
        }
    }

    private IEnumerator DownloadImage(string url)
    {
        // Create a UnityWebRequest object to download the image
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);

        // Wait for the download to complete
        yield return request.SendWebRequest();

        // Check for errors
        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(request);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);

            // Apply the sprite to the target image
            AboutLogo_Image.sprite = sprite;
        }
        else
        {
            Debug.LogError("Error downloading image: " + request.error);
        }
    }
}
