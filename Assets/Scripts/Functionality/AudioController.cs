using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using DG.Tweening;

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
    [SerializeField] private AudioSource m_HugeWin_Sound;
    [SerializeField] private AudioSource m_MegaWin_Sound;
    [SerializeField] private AudioSource m_GoldCount_Audio;
    [SerializeField] private AudioSource m_Bull_Audio;

    //Per-symbol win SFX, indexed by (symbolId - 6): 0=Cat, 1=Eagle, 2=Bear, 3=Wolf, 4=Buffalo.
    [SerializeField] private AudioSource[] m_SymbolWinSounds;

    [SerializeField] private bool m_MutedMusic = false;
    [SerializeField] private bool m_MutedSound = false;

    //True while audio is silenced because the game lost focus, as opposed to because the user muted it.
    //Guards CheckFocusFunction against duplicate blur/focus signals; cleared by any user-driven
    //MuteUnmute so a stray unpaired blur can't leave the sound/music buttons unable to re-mute later.
    private bool isForceMuted = false;

    //Original volume of each win-tier source, cached once so FadeOutWinAudio/PlayIfEnabled can
    //restore it after a DOFade drives it to 0.
    private readonly Dictionary<AudioSource, float> m_WinAudioOriginalVolume = new Dictionary<AudioSource, float>();

    private void Start()
    {
        if (m_BG_Music) m_BG_Music.Play();

        CacheWinAudioVolume(m_NormalWin_Sound);
        CacheWinAudioVolume(m_BigWin_Sound);
        CacheWinAudioVolume(m_HugeWin_Sound);
        CacheWinAudioVolume(m_MegaWin_Sound);
    }

    private void CacheWinAudioVolume(AudioSource source)
    {
        if (source) m_WinAudioOriginalVolume[source] = source.volume;
    }

    //Shared focus-mute entry point: both the JS bridge (SocketIOManager.OnFocusChanged) and Unity's
    //native OnApplicationFocus route here, and in a WebGL build either or both may fire for the same
    //blur/focus event. isForceMuted makes a duplicate call for the same direction a no-op so the
    //second one can't re-run the restore against an already-forced state.
    //Never touches m_MutedMusic/m_MutedSound — those hold the user's own choice and are written only
    //by the sound/music buttons, so regaining focus restores exactly what the user last picked.
    internal void CheckFocusFunction(bool focus)
    {
        bool forceMute = !focus;
        if (forceMute == isForceMuted) return;
        isForceMuted = forceMute;

        MuteUnmute(Sound.All, forceMute, false);
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
                PlayIfEnabled(m_NormalWin_Sound);
                break;
            case Sound.BigWin:
                PlayIfEnabled(m_BigWin_Sound);
                break;
            case Sound.HugeWin:
                PlayIfEnabled(m_HugeWin_Sound);
                break;
            case Sound.MegaWin:
                PlayIfEnabled(m_MegaWin_Sound);
                break;
        }
    }

    //Win sources are assigned per-tier in the Editor; an unassigned tier stays silent instead of throwing.
    //Kills any in-flight fade-out first so a re-triggered source doesn't start at a faded volume.
    private void PlayIfEnabled(AudioSource source)
    {
        if (!source || !m_MainAudioListener.enabled) return;
        source.DOKill();
        if (m_WinAudioOriginalVolume.TryGetValue(source, out float original)) source.volume = original;
        source.Play();
    }

    //Fades out and stops whichever win-tier source is currently playing (Normal/Big/Huge/Mega), so
    //skipping to the next spin doesn't leave the previous win's sound running underneath it.
    internal void FadeOutWinAudio(float duration = 0.15f)
    {
        FadeOutIfPlaying(m_NormalWin_Sound, duration);
        FadeOutIfPlaying(m_BigWin_Sound, duration);
        FadeOutIfPlaying(m_HugeWin_Sound, duration);
        FadeOutIfPlaying(m_MegaWin_Sound, duration);
    }

    private void FadeOutIfPlaying(AudioSource source, float duration)
    {
        if (!source || !source.isPlaying) return;
        source.DOKill();
        float original = m_WinAudioOriginalVolume.TryGetValue(source, out float v) ? v : source.volume;
        source.DOFade(0f, duration).OnComplete(() =>
        {
            source.Stop();
            source.volume = original;
        });
    }

    internal void PlaySymbolWin(int symbolId)
    {
        int idx = symbolId - 6;
        if (m_SymbolWinSounds == null || idx < 0 || idx >= m_SymbolWinSounds.Length) return;
        PlayIfEnabled(m_SymbolWinSounds[idx]);
    }

    //Hard-cuts any playing symbol win SFX; called whenever the win animation moves to a new combo/line.
    internal void StopAllSymbolWinSounds()
    {
        if (m_SymbolWinSounds == null) return;
        foreach (var source in m_SymbolWinSounds)
            if (source) source.Stop();
    }

    internal void MuteUnmute(Sound sound, bool toggle, bool config)
    {
        //config==true means this came from the user's own sound/music button. An explicit tap proves the
        //game really has focus, so drop any lingering forced-mute rather than letting a stale blur
        //signal keep the focus state (and the guard in CheckFocusFunction) out of sync with reality.
        if (config) isForceMuted = false;

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
                if (m_HugeWin_Sound) m_HugeWin_Sound.mute = toggle;
                m_MegaWin_Sound.mute = toggle;
                m_FreeSpinEnc_Sound.mute = toggle;
                m_Bull_Audio.mute = toggle;
                MuteSymbolWinSounds(toggle);
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
                    if (m_HugeWin_Sound) m_HugeWin_Sound.mute = toggle;
                    m_MegaWin_Sound.mute = toggle;
                    m_FreeSpinEnc_Sound.mute = toggle;
                    m_BG_Music.mute = toggle;
                    m_Bull_Audio.mute = toggle;
                    MuteSymbolWinSounds(toggle);
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
                        if (m_HugeWin_Sound) m_HugeWin_Sound.mute = toggle;
                        m_MegaWin_Sound.mute = toggle;
                        m_FreeSpinEnc_Sound.mute = toggle;
                        m_Bull_Audio.mute = toggle;
                        MuteSymbolWinSounds(toggle);
                    }
                }
                break;
        }
    }

    private void MuteSymbolWinSounds(bool toggle)
    {
        if (m_SymbolWinSounds == null) return;
        foreach (var source in m_SymbolWinSounds)
            if (source) source.mute = toggle;
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
    //Appended, not inserted: existing ordinals must stay put for already-serialized values.
    HugeWin,
    None,
}
