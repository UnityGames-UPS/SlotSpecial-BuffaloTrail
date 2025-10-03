using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro; // For TextMeshPro support

public class DOTweenUIManager : MonoBehaviour
{
    // Singleton for easy access (optional)
    public static DOTweenUIManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 1. Slide in from left
    internal void SlideInFromLeft(RectTransform uiElement, float duration)
    {
        uiElement.anchoredPosition = new Vector2(-Screen.width, uiElement.anchoredPosition.y);
        uiElement.DOAnchorPosX(0, duration).SetEase(Ease.OutExpo);
    }

    // 2. Slide in from right
    internal void SlideInFromRight(RectTransform uiElement, float duration)
    {
        uiElement.anchoredPosition = new Vector2(Screen.width, uiElement.anchoredPosition.y);
        uiElement.DOAnchorPosX(0, duration).SetEase(Ease.OutExpo);
    }

    // 3. Slide in from top
    internal void SlideInFromTop(RectTransform uiElement, float duration)
    {
        uiElement.anchoredPosition = new Vector2(uiElement.anchoredPosition.x, Screen.height);
        uiElement.DOAnchorPosY(0, duration).SetEase(Ease.OutExpo);
    }

    // 4. Slide in from bottom
    internal void SlideInFromBottom(RectTransform uiElement, float duration)
    {
        uiElement.anchoredPosition = new Vector2(uiElement.anchoredPosition.x, -Screen.height);
        uiElement.DOAnchorPosY(0, duration).SetEase(Ease.OutExpo);
    }

    // 5. Pop in (scale)
    internal void PopIn(RectTransform uiElement, float duration)
    {
        uiElement.localScale = Vector3.zero;
        uiElement.DOScale(Vector3.one, duration).SetEase(Ease.OutBack);
    }

    // 6. Pop out (scale)
    internal void PopOut(RectTransform uiElement, float duration)
    {
        uiElement.DOScale(Vector3.zero, duration).SetEase(Ease.InBack);
    }

    // 7. Fade in UI element
    internal void FadeIn(CanvasGroup uiElement, float duration)
    {
        uiElement.alpha = 0;
        uiElement.DOFade(1, duration);
    }

    // 8. Fade out UI element
    internal void FadeOut(CanvasGroup uiElement, float duration)
    {
        uiElement.alpha = 1;
        uiElement.DOFade(0, duration);
    }

    // 9. Bounce in (Y-axis bounce effect)
    internal void BounceIn(RectTransform uiElement, float duration)
    {
        uiElement.localScale = Vector3.zero;
        uiElement.DOScale(Vector3.one, duration).SetEase(Ease.OutBounce);
    }

    // 10. Shake effect (like a UI button shake)
    internal void ShakeUI(RectTransform uiElement, float duration, float strength = 20, int vibrato = 10)
    {
        uiElement.DOShakePosition(duration, strength, vibrato);
    }

    // 11. Boomerang effect (move to a point and back)
    internal void Boomerang(RectTransform uiElement, Vector2 targetPos, float duration)
    {
        Vector2 originalPos = uiElement.anchoredPosition;
        uiElement.DOAnchorPos(targetPos, duration / 2).SetEase(Ease.OutQuad)
            .OnComplete(() => uiElement.DOAnchorPos(originalPos, duration / 2).SetEase(Ease.InQuad));
    }

    // 12. Rotate UI Element (like a spinning icon)
    internal void RotateUI(RectTransform uiElement, string axis, float duration, float rotationAngle = 360f)
    {
        switch (axis.ToUpper())
        {
            case "X":
                uiElement.DORotate(new Vector3(rotationAngle, 0, 0), duration, RotateMode.FastBeyond360);
                break;
            case "Y":
                uiElement.DORotate(new Vector3(0, rotationAngle, 0), duration, RotateMode.FastBeyond360);
                break;
            case "Z":
                uiElement.DORotate(new Vector3(0, 0, rotationAngle), duration, RotateMode.FastBeyond360);
                break;
        }
    }

    // 13. Pulse (scaling up and down)
    internal void Pulse(RectTransform uiElement, float duration, float scaleMultiplier = 1.2f)
    {
        Sequence pulseSequence = DOTween.Sequence();
        pulseSequence.Append(uiElement.DOScale(Vector3.one * scaleMultiplier, duration / 2))
            .Append(uiElement.DOScale(Vector3.one, duration / 2))
            .SetLoops(-1, LoopType.Yoyo);
    }

    // 14. Text typing animation (TextMeshPro)
    //internal void TypeText(TextMeshProUGUI textComponent, string fullText, float typeDuration)
    //{
    //    textComponent.text = "";
    //    textComponent.DOText(fullText, typeDuration).SetEase(Ease.Linear);
    //}

    // 15. Jump animation (makes the UI element jump)
    internal void Jump(RectTransform uiElement, float jumpPower, int numJumps, float duration)
    {
        uiElement.DOJumpAnchorPos(uiElement.anchoredPosition, jumpPower, numJumps, duration);
    }

    // 16. Flip horizontally (like a card flip)
    internal void FlipHorizontally(RectTransform uiElement, float duration)
    {
        uiElement.DORotate(new Vector3(0, 180, 0), duration, RotateMode.LocalAxisAdd);
    }

    // 17. Flip vertically
    internal void FlipVertically(RectTransform uiElement, float duration)
    {
        uiElement.DORotate(new Vector3(180, 0, 0), duration, RotateMode.LocalAxisAdd);
    }

    // 18. Expand width (like opening a drawer)
    internal void ExpandWidth(RectTransform uiElement, float targetWidth, float duration)
    {
        uiElement.DOSizeDelta(new Vector2(targetWidth, uiElement.sizeDelta.y), duration);
    }

    // 19. Expand height
    internal void ExpandHeight(RectTransform uiElement, float targetHeight, float duration)
    {
        uiElement.DOSizeDelta(new Vector2(uiElement.sizeDelta.x, targetHeight), duration);
    }

    // 20. Color change animation (for UI Images)
    internal void ChangeColor(Image uiElement, Color targetColor, float duration)
    {
        uiElement.DOColor(targetColor, duration);
    }

    internal void MoveDir(Transform MainTransform, float End_Val, string Dir, float Duration)
    {
        switch (Dir.ToUpper())
        {
            case "X":
                MainTransform.DOMoveX(End_Val, Duration);
                break;
            case "Y":
                MainTransform.DOMoveY(End_Val, Duration);
                break;
            case "Z":
                MainTransform.DOMoveZ(End_Val, Duration);
                break;
        }
    }
}