using System;
using Game.Gimmick;

public static class EventBus
{
    //기믹 해제 성공 이벤트
    public static event Action<GimmickNode> OnGimmickCompleted;

    //숨기 이벤트
    public static event Action OnHideEntered;
    public static event Action OnHideExited;

    public static void RaiseGimmickCompleted(GimmickNode g) => OnGimmickCompleted?.Invoke(g);
    public static void RaiseHideEntered() => OnHideEntered?.Invoke();
    public static void RaiseHideExited() => OnHideExited?.Invoke();
}
