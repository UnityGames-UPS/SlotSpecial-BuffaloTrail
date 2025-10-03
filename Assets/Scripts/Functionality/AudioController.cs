using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class AudioController : MonoBehaviour
{
    [SerializeField] private AudioListener m_MainAudioListener;
    [SerializeField] private AudioSource m_NormalButton_Audio;
    [SerializeField] private AudioSource m_SpinButton_Audio;
    [SerializeField] private AudioSource m_Spinning_Audio;
    [SerializeField] private AudioSource m_BG_Music;
    [SerializeField] private AudioSource m_FreeSpinEnc_Sound;
    [SerializeField] private AudioSource m_NormalWin_Sound;
    [SerializeField] private AudioSource m_BigWin_Sound;
    [SerializeField] private AudioSource m_MegaWin_Sound;
    [SerializeField] private AudioSource m_GoldCount_Audio;
    [SerializeField] private AudioSource m_Bull_Audio;

    [SerializeField] private bool m_MutedMusic = false;
    [SerializeField] private bool m_MutedSound = false;

    private void Start()
    {
        if (m_BG_Music) m_BG_Music.Play();
    }

    internal void CheckFocusFunction(bool focus)
    {
        //m_MainAudioListener.enabled = focus;
        if (!focus)
        {
            //m_MainAudioListener.enabled = focus;
            MuteUnmute(Sound.All, true, false);
        }
        else
        {
            //m_MainAudioListener.enabled = focus;
            MuteUnmute(Sound.All, false, false);
        }
    }

    internal void PlayNormalButton()
    {
        if(m_MainAudioListener.enabled) m_NormalButton_Audio.Play();
    }

    internal void PlaySpinButton()
    {
        if (m_MainAudioListener.enabled) m_SpinButton_Audio.Play();
    }

    internal void PlayFreeSpin_Enc()
    {
        if (m_MainAudioListener.enabled) m_FreeSpinEnc_Sound.Play();
    }

    internal void PlayGold_Enc()
    {
        if (m_MainAudioListener.enabled) m_GoldCount_Audio.Play();
    }

    internal void PlayBull_Audio()
    {
        if(m_MainAudioListener.enabled) m_Bull_Audio.Play();
    }

    internal void PlaySpinAudio(bool m_play_pause)
    {
        switch (m_play_pause)
        {
            case true:
                if (m_MainAudioListener.enabled) m_Spinning_Audio.Play();
                break;
            case false:
                if (m_MainAudioListener.enabled) m_Spinning_Audio.Stop();
                break;
        }
    }

    internal void PlayWin(Sound win)
    {
        switch (win)
        {
            case Sound.NormalWin:
                if (m_MainAudioListener.enabled) m_NormalWin_Sound.Play();
                break;
            case Sound.BigWin:
                if (m_MainAudioListener.enabled) m_BigWin_Sound.Play();
                break;
            case Sound.MegaWin:
                if (m_MainAudioListener.enabled) m_MegaWin_Sound.Play();
                break;
        }
    }

    internal void MuteUnmute(Sound sound, bool toggle, bool config)
    {
        switch (sound)
        {
            case Sound.Music:
                m_BG_Music.mute = toggle;
                m_MutedMusic = toggle;
                break;
            case Sound.Sound:
                m_NormalButton_Audio.mute = toggle;
                m_SpinButton_Audio.mute = toggle;
                m_Spinning_Audio.mute = toggle;
                m_GoldCount_Audio.mute = toggle;
                m_NormalWin_Sound.mute = toggle;
                m_BigWin_Sound.mute = toggle;
                m_MegaWin_Sound.mute = toggle;
                m_FreeSpinEnc_Sound.mute = toggle;
                m_Bull_Audio.mute = toggle;
                m_MutedSound = toggle;
                break;
            case Sound.All:
                //Debug.Log("Toggle Is: " + toggle + " " + " Config Is: " + config);
                if (config || (!config && toggle))
                {
                    m_NormalButton_Audio.mute = toggle;
                    m_SpinButton_Audio.mute = toggle;
                    m_Spinning_Audio.mute = toggle;
                    m_GoldCount_Audio.mute = toggle;
                    m_NormalWin_Sound.mute = toggle;
                    m_BigWin_Sound.mute = toggle;
                    m_MegaWin_Sound.mute = toggle;
                    m_FreeSpinEnc_Sound.mute = toggle;
                    m_BG_Music.mute = toggle;
                    m_Bull_Audio.mute = toggle;
                }
                else
                {
                    if (!m_MutedMusic)
                    {
                        m_BG_Music.mute = toggle;
                    }
                    if (!m_MutedSound)
                    {
                        m_NormalButton_Audio.mute = toggle;
                        m_SpinButton_Audio.mute = toggle;
                        m_Spinning_Audio.mute = toggle;
                        m_GoldCount_Audio.mute = toggle;
                        m_NormalWin_Sound.mute = toggle;
                        m_BigWin_Sound.mute = toggle;
                        m_MegaWin_Sound.mute = toggle;
                        m_FreeSpinEnc_Sound.mute = toggle;
                        m_Bull_Audio.mute = toggle;
                    }
                }
                break;
        }
    }
}

public enum Sound
{
    NormalWin,
    BigWin,
    MegaWin,
    //Mute or Unmute Ids
    All,
    Music,
    Sound,
}
