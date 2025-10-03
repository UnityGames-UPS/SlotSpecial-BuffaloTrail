using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class AnimationController : MonoBehaviour
{
    [SerializeField]
    private Transform m_ParentSlotsHolder;
    [SerializeField]
    internal List<SlotImage> m_AnimatedSlots = new List<SlotImage>();
    [SerializeField]
    //private List<AnimCords> m_Cords;
    private List<List<List<int>>> m_Cords = new List<List<List<int>>>();
    [SerializeField]
    private SlotBehaviour m_SlotBehaviour;
    [SerializeField]
    private SocketIOManager SocketManager;

    private Coroutine m_AnimationRoutine;
    private List<Tweener> m_SlotsAnim = new List<Tweener>();
    private bool m_PlayingAnimation = false;

    internal void StartAnimation(List<List<List<int>>> m_SymbolsToEmit)
    {
        m_PlayingAnimation = true;
        if(m_AnimationRoutine == null)
            m_AnimationRoutine = StartCoroutine(ActivateAllAnimation());

        m_Cords = m_SymbolsToEmit;
    }

    internal void StopAnimation()
    {
        m_PlayingAnimation = false;
        ResetAnimatedView();
        if(m_AnimationRoutine != null)
            StopCoroutine(m_AnimationRoutine);
        m_AnimationRoutine = null;

        m_Cords.Clear();
        m_Cords.TrimExcess();
    }

    private IEnumerator ActivateAllAnimation()
    {
        while (m_PlayingAnimation)
        {
            foreach (var i in m_Cords)
            {
                foreach (var k in i)
                {
                    ActivateAnimatedView(k[0], k[1]);
                }
            }
            yield return new WaitForSeconds(1f);
            ResetAnimatedView();
            foreach (var i in m_Cords)
            {
                foreach (var k in i)
                {
                    ActivateAnimatedView(k[0], k[1]);
                }
                yield return new WaitForSeconds(1f);
                ResetAnimatedView();
            }
            yield return new WaitForSeconds(1f);
            ResetAnimatedView();
        }
    }

    internal void FreeSpinCoinAnimate()
    {
        for (int i = 0; i < SocketManager.resultData.fsWinningSymbols.Count; i++)
        {

            m_AnimatedSlots[SocketManager.resultData.fsWinningSymbols[i][1]].slotImages[SocketManager.resultData.fsWinningSymbols[i][0]].GetComponent<ImageAnimation>().StartAnimation();
        }
    }

    private void ActivateAnimatedView(int j, int i)
    {
        if (!m_ParentSlotsHolder.gameObject.activeSelf)
        {
            ResetAnimatedView();
            m_ParentSlotsHolder.gameObject.SetActive(true);
        }
        m_AnimatedSlots[i].slotImages[j].gameObject.SetActive(true);
        m_SlotBehaviour.Tempimages[i].slotImages[j].gameObject.SetActive(false);
        //m_AnimatedSlots[i].slotImages[j].transform.DOScale(1.2f, 1);
        if(m_AnimatedSlots[i].slotImages[j].gameObject.GetComponent<ImageAnimation>().textureArray.Count > 0)
            m_AnimatedSlots[i].slotImages[j].gameObject.GetComponent<ImageAnimation>().StartAnimation();
        else
        {
            var tween = m_AnimatedSlots[i].slotImages[j].gameObject.GetComponent<RectTransform>().DOScale(new Vector2(1.15f, 1.15f), .8f).SetLoops(-1, LoopType.Yoyo).SetDelay(0);
            tween.Play();
            m_SlotsAnim.Add(tween);
        }
    }

    private void ResetAnimatedView()
    {
        for(int i = 0; i < m_AnimatedSlots.Count; i++)
        {
            for(int j = 0; j < m_AnimatedSlots[i].slotImages.Count; j++)
            {
                m_AnimatedSlots[i].slotImages[j].gameObject.SetActive(false);
                m_SlotBehaviour.Tempimages[i].slotImages[j].gameObject.SetActive(true);
                if (m_AnimatedSlots[i].slotImages[j].gameObject.GetComponent<ImageAnimation>().textureArray.Count > 0)
                    m_AnimatedSlots[i].slotImages[j].gameObject.GetComponent<ImageAnimation>().StopAnimation();
                else
                    m_AnimatedSlots[i].slotImages[j].transform.localScale = new Vector3(1, 1, 1);
            }
        }
        foreach(var i in m_SlotsAnim)
        {
            i.Kill();
        }
        m_SlotsAnim.Clear();
        m_SlotsAnim.TrimExcess();
        m_ParentSlotsHolder.gameObject.SetActive(false);
    }
}

[System.Serializable]
public struct Cord
{
    public int i;
    public int j;
}

[System.Serializable]
public struct AnimCords
{
    public List<Cord> m_Cords;
}
