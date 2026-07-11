using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class AnimationController : MonoBehaviour
{
    [SerializeField]
    private Transform m_ParentSlotsHolder;
    [SerializeField]
    internal List<SlotImage> m_AnimatedSlots = new List<SlotImage>();
    [SerializeField]
    private SlotBehaviour m_SlotBehaviour;
    [SerializeField]
    private SocketIOManager SocketManager;
    [SerializeField]
    private AudioController m_AudioController;
    [SerializeField]
    //Top-level transform (sits above the outside-slot UI in the canvas hierarchy) that a cell is
    //reparented into while its win animation plays, since SetAsLastSibling only reorders within a
    //parent and can't draw over sibling hierarchies.
    private Transform m_AnimOverlayParent;

    [Header("Win-line timing")]
    [SerializeField]
    private float m_BetweenLineDelay = 0.25f;   //black-overlay-only gap between plays
    [SerializeField]
    private float m_NoAnimLineDuration = 2f;     //per-line duration when no symbol has a frame sequence
    [SerializeField]
    private float m_PulseScale = 1.15f;          //scale-pulse peak for symbols without frame sequences

    [Header("Per-symbol overlay Y offset")]
    [SerializeField]
    //Y nudge applied to a symbol's overlay while it animates (e.g. wolf id 9 => -23, bear id 8 => -5).
    private List<SymbolYOffset> m_SymbolYOffsets = new List<SymbolYOffset>
    {
        new SymbolYOffset { symbolId = 9, yOffset = -23f },
        new SymbolYOffset { symbolId = 8, yOffset = -5f },
    };

    private Coroutine m_WinRoutine;
    //guards against re-applying the Y-offset nudge if a cell is lit twice before it's reset
    private Dictionary<(int col, int row), float> m_OffsetCells = new Dictionary<(int, int), float>();
    //per-cell one-shot highlight tweens (scale pulses), so a single line can be reset in isolation
    private Dictionary<(int col, int row), Tween> m_SlotsAnim = new Dictionary<(int, int), Tween>();
    //cached per-cell payout labels (child 0) and frame animators
    private List<List<TMP_Text>> m_WinLabels = new List<List<TMP_Text>>();
    private List<List<ImageAnimation>> m_CellAnim = new List<List<ImageAnimation>>();
    //cached per-cell home transform (captured once in Awake, before anything moves), so a cell
    //pulled into m_AnimOverlayParent can be put back exactly where it started
    private List<List<Transform>> m_HomeParent = new List<List<Transform>>();
    private List<List<int>> m_HomeSiblingIndex = new List<List<int>>();
    private List<List<Vector3>> m_HomeLocalPosition = new List<List<Vector3>>();

    private void Awake()
    {
        //Cache the per-cell labels, frame animators, and home transform once. m_AnimatedSlots is a
        //[SerializeField] list, so it is already populated by the time Awake runs.
        m_WinLabels.Clear();
        m_CellAnim.Clear();
        m_HomeParent.Clear();
        m_HomeSiblingIndex.Clear();
        m_HomeLocalPosition.Clear();
        for (int c = 0; c < m_AnimatedSlots.Count; c++)
        {
            var labelCol = new List<TMP_Text>();
            var animCol = new List<ImageAnimation>();
            var parentCol = new List<Transform>();
            var siblingCol = new List<int>();
            var posCol = new List<Vector3>();
            for (int r = 0; r < m_AnimatedSlots[c].slotImages.Count; r++)
            {
                var cell = m_AnimatedSlots[c].slotImages[r];

                TMP_Text label = null;
                if (cell != null && cell.transform.childCount > 0)
                    label = cell.transform.GetChild(0).GetComponent<TMP_Text>();
                labelCol.Add(label);

                ImageAnimation anim = cell != null ? cell.GetComponent<ImageAnimation>() : null;
                if (anim != null)
                    anim.doLoopAnimation = false;   //win highlights play their sequence once, not looped
                animCol.Add(anim);

                parentCol.Add(cell != null ? cell.transform.parent : null);
                siblingCol.Add(cell != null ? cell.transform.GetSiblingIndex() : -1);
                posCol.Add(cell != null ? cell.transform.localPosition : Vector3.zero);
            }
            m_WinLabels.Add(labelCol);
            m_CellAnim.Add(animCol);
            m_HomeParent.Add(parentCol);
            m_HomeSiblingIndex.Add(siblingCol);
            m_HomeLocalPosition.Add(posCol);
        }
    }

    internal void StartAnimation(List<WinningCombination> combos, bool autoContinued)
    {
        StopAnimation();
        if (combos == null || combos.Count == 0) return;

        m_WinRoutine = StartCoroutine(AnimateLineWins(combos, autoContinued));
    }

    //True while the opening synced pass is still playing. Chained spins (auto/free) poll this so a
    //win that triggers no banner still gets one full animation cycle before the next spin starts.
    internal bool IsWinPassPlaying { get; private set; }

    internal void StopAnimation()
    {
        //Order matters: stop the loop first, then reset, so the loop can't spawn one more
        //iteration on the frame between the two calls.
        if (m_WinRoutine != null)
        {
            StopCoroutine(m_WinRoutine);
            m_WinRoutine = null;
        }
        IsWinPassPlaying = false;   //the pass can't outlive the routine that drives it
        ResetAnimatedView();
    }

    private IEnumerator AnimateLineWins(List<WinningCombination> combos, bool autoContinued)
    {
        m_ParentSlotsHolder.gameObject.SetActive(true);   //dim on (dark image + overlay layer)

        bool singleLine = combos.Count == 1;

        //Synced pass: every winning symbol (deduped) plays its sequence once, together.
        IsWinPassPlaying = true;
        float syncedDuration = PlaySyncedPass(combos, showPayouts: singleLine && !autoContinued);
        yield return new WaitForSecondsRealtime(syncedDuration);
        IsWinPassPlaying = false;

        //Auto / free / feature-triggered chains: one pass, then clear so the chain can advance.
        if (autoContinued)
        {
            ResetAnimatedView();
            yield break;
        }

        //Turn everything off (only the black overlay shows), then cycle the lines one at a time.
        //A single line simply cycles on itself: play once -> off for a beat -> play again.
        ResetAllLines(combos);
        while (true)
        {
            foreach (var combo in combos)
            {
                yield return new WaitForSecondsRealtime(m_BetweenLineDelay);
                float dur = PlayLine(combo, showPayouts: true);
                yield return new WaitForSecondsRealtime(dur);
                ResetLine(combo);
            }
        }
    }

    private float PlaySyncedPass(List<WinningCombination> combos, bool showPayouts)
    {
        //Map each line's last symbol to its payout (only when we show labels).
        var lastPayout = new Dictionary<(int, int), double>();
        if (showPayouts)
        {
            foreach (var combo in combos)
            {
                if (combo.positions == null || combo.positions.Count == 0) continue;
                if (!TryCell(combo.positions[combo.positions.Count - 1], out int lc, out int lr)) continue;
                lastPayout[(lc, lr)] = combo.payout;
            }
        }

        //Collect the deduped set of cells first, so the group duration covers all of them.
        var cells = new List<(int col, int row)>();
        var seen = new HashSet<(int, int)>();
        foreach (var combo in combos)
        {
            if (combo.positions == null) continue;
            foreach (var pos in combo.positions)
            {
                if (!TryCell(pos, out int col, out int row)) continue;
                if (!seen.Add((col, row))) continue;   //a symbol on N lines plays once
                cells.Add((col, row));
            }
        }

        float duration = ComputeGroupDuration(cells);
        var playedSymbols = new HashSet<int>();
        foreach (var (col, row) in cells)
        {
            TryPlaySymbolAudio(col, row, playedSymbols);
            bool show = lastPayout.TryGetValue((col, row), out double payout);
            LightCell(col, row, show, show ? payout : 0, duration);
        }
        return duration;
    }

    private float PlayLine(WinningCombination combo, bool showPayouts)
    {
        if (combo.positions == null) return m_NoAnimLineDuration;

        var cells = new List<(int col, int row)>();
        foreach (var pos in combo.positions)
            if (TryCell(pos, out int col, out int row))
                cells.Add((col, row));

        float duration = ComputeGroupDuration(cells);
        int last = combo.positions.Count - 1;
        var playedSymbols = new HashSet<int>();
        for (int i = 0; i < combo.positions.Count; i++)
        {
            if (!TryCell(combo.positions[i], out int col, out int row)) continue;
            TryPlaySymbolAudio(col, row, playedSymbols);
            bool show = showPayouts && (i == last);
            LightCell(col, row, show, show ? combo.payout : 0, duration);
        }
        return duration;
    }

    //Plays a symbol's win SFX once per group (synced pass or single line), keyed by symbol id so
    //repeats of the same symbol (e.g. 3 wolves) only sound once.
    private void TryPlaySymbolAudio(int col, int row, HashSet<int> played)
    {
        int symbolId = GetSymbolId(col, row);
        if (symbolId < 6 || symbolId > 10) return;   //only the 5 animal symbols have win SFX
        if (!played.Add(symbolId)) return;
        if (m_AudioController != null) m_AudioController.PlaySymbolWin(symbolId);
    }

    private void ResetLine(WinningCombination combo)
    {
        if (m_AudioController != null) m_AudioController.StopAllSymbolWinSounds();
        if (combo.positions == null) return;
        for (int i = 0; i < combo.positions.Count; i++)   //iterate full positions, matching the play loop
        {
            if (!TryCell(combo.positions[i], out int col, out int row)) continue;
            ResetCell(col, row);
        }
    }

    private void ResetAllLines(List<WinningCombination> combos)
    {
        foreach (var combo in combos)
            ResetLine(combo);
    }

    //Longest one-shot animation in the group. Frame symbols use their sequence length; symbols
    //without frames are pulsed over this same duration so the whole group finishes together.
    //If no symbol in the group has a frame sequence, fall back to the inspector duration.
    private float ComputeGroupDuration(List<(int col, int row)> cells)
    {
        float maxFrames = 0f;
        bool anyFrames = false;
        foreach (var (col, row) in cells)
        {
            var anim = m_CellAnim[col][row];
            if (anim != null && anim.textureArray != null && anim.textureArray.Count > 0)
            {
                anyFrames = true;
                maxFrames = Mathf.Max(maxFrames, anim.GetSequenceDuration());
            }
        }
        return anyFrames ? maxFrames : m_NoAnimLineDuration;
    }

    private void LightCell(int col, int row, bool showPayout, double payout, float duration)
    {
        if (!m_ParentSlotsHolder.gameObject.activeSelf)
            m_ParentSlotsHolder.gameObject.SetActive(true);

        var cell = m_AnimatedSlots[col].slotImages[row];
        cell.gameObject.SetActive(true);
        m_SlotBehaviour.Tempimages[col].slotImages[row].gameObject.SetActive(false);

        //Pull the cell into the overlay parent (worldPositionStays: true keeps its visual spot) so it
        //draws above the outside-slot UI, which SetAsLastSibling alone can never do across parents.
        cell.transform.SetParent(m_AnimOverlayParent, true);

        //Nudge certain symbols' overlay so their frame sits correctly; the exact amount is tracked so
        //it's undone once on reset and can never drift across iterations.
        if (!m_OffsetCells.ContainsKey((col, row)))
        {
            float yOff = GetYOffset(GetSymbolId(col, row));
            if (yOff != 0f)
            {
                cell.transform.localPosition += new Vector3(0f, yOff, 0f);
                m_OffsetCells[(col, row)] = yOff;
            }
        }

        var anim = m_CellAnim[col][row];
        if (anim != null && anim.textureArray != null && anim.textureArray.Count > 0)
        {
            anim.StopAnimation();    //force back to NONE so StartAnimation replays from frame 0
            anim.StartAnimation();
        }
        else
        {
            //One up/down scale pulse spanning the group's duration, so frameless symbols stay in
            //time with the frame-animated ones.
            if (m_SlotsAnim.TryGetValue((col, row), out var existing))
                existing.Kill();

            var rt = cell.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(rt.DOScale(m_PulseScale, duration * 0.5f).SetEase(Ease.OutSine));
            seq.Append(rt.DOScale(1f, duration * 0.5f).SetEase(Ease.InSine));
            m_SlotsAnim[(col, row)] = seq;
        }

        var label = m_WinLabels[col][row];
        if (showPayout && label != null)
        {
            label.gameObject.SetActive(true);
            label.text = FormatPayout(payout);
        }
    }

    private void ResetCell(int col, int row)
    {
        var cell = m_AnimatedSlots[col].slotImages[row];
        cell.gameObject.SetActive(false);
        m_SlotBehaviour.Tempimages[col].slotImages[row].gameObject.SetActive(true);

        //Return the cell to its cached home parent/sibling-index/local-position exactly, which also
        //undoes the Y-offset nudge and the overlay reparent in one shot (no drift across iterations).
        cell.transform.SetParent(m_HomeParent[col][row], false);
        cell.transform.SetSiblingIndex(m_HomeSiblingIndex[col][row]);
        cell.transform.localPosition = m_HomeLocalPosition[col][row];
        m_OffsetCells.Remove((col, row));

        var anim = m_CellAnim[col][row];
        if (anim != null && anim.textureArray != null && anim.textureArray.Count > 0)
            anim.StopAnimation();
        else
            cell.transform.localScale = Vector3.one;

        if (m_SlotsAnim.TryGetValue((col, row), out var tween))
        {
            tween.Kill();
            m_SlotsAnim.Remove((col, row));
        }

        var label = m_WinLabels[col][row];
        if (label != null)
            label.gameObject.SetActive(false);
    }

    private void ResetAnimatedView()
    {
        if (m_AudioController != null) m_AudioController.StopAllSymbolWinSounds();
        for (int c = 0; c < m_AnimatedSlots.Count; c++)
            for (int r = 0; r < m_AnimatedSlots[c].slotImages.Count; r++)
                ResetCell(c, r);

        foreach (var kvp in m_SlotsAnim)
            kvp.Value.Kill();
        m_SlotsAnim.Clear();
        m_OffsetCells.Clear();

        m_ParentSlotsHolder.gameObject.SetActive(false);   //instant dim clear (no fade)
    }

    //Normalize a server position into (col, row). The wire format is [row, col] but every access
    //in this codebase indexes layer[pos[1]].slotImages[pos[0]] — preserve that exactly.
    private bool TryCell(List<int> pos, out int col, out int row)
    {
        col = 0; row = 0;
        if (pos == null || pos.Count < 2) return false;
        col = pos[1];
        row = pos[0];
        if (col < 0 || col >= m_AnimatedSlots.Count) return false;
        if (row < 0 || row >= m_AnimatedSlots[col].slotImages.Count) return false;
        return true;
    }

    //Configured Y nudge for a symbol id (0 if none).
    private float GetYOffset(int symbolId)
    {
        for (int i = 0; i < m_SymbolYOffsets.Count; i++)
            if (m_SymbolYOffsets[i].symbolId == symbolId)
                return m_SymbolYOffsets[i].yOffset;
        return 0f;
    }

    //Symbol id landed on a cell, read from the server result matrix (indexed [row][col], as in
    //SlotBehaviour). Returns -1 if unavailable.
    private int GetSymbolId(int col, int row)
    {
        var matrix = SocketManager != null && SocketManager.resultData != null
            ? SocketManager.resultData.matrix : null;
        if (matrix == null || row < 0 || row >= matrix.Count) return -1;
        var r = matrix[row];
        if (r == null || col < 0 || col >= r.Count) return -1;
        return int.TryParse(r[col], out int id) ? id : -1;
    }

    private static string FormatPayout(double value)
    {
        return value.ToString("0.###");
    }

    [System.Serializable]
    public struct SymbolYOffset
    {
        public int symbolId;
        public float yOffset;
    }

    internal void FreeSpinCoinAnimate()
    {
        for (int i = 0; i < SocketManager.resultData.features.freeSpin.wildMultiplier.Count; i++)
        {
            var wm = SocketManager.resultData.features.freeSpin.wildMultiplier[i];
            if (wm.position == null || wm.position.Count < 2)
                continue;

            int x = wm.position[0];
            int y = wm.position[1];

            if (y < m_AnimatedSlots.Count && x < m_AnimatedSlots[y].slotImages.Count)
            {
                m_AnimatedSlots[y].slotImages[x].GetComponent<ImageAnimation>().StartAnimation();
            }
        }
    }
}
