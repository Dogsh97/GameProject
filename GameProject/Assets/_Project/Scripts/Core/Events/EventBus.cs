using System;
using UnityEngine;
using Game.Gimmick;

public static class EventBus
{
    //기믹 해제 성공 이벤트
    public static event Action<GimmickNode> OnGimmickCompleted;
    // 스킬 체크 실패
    public static event Action<GimmickNode> OnSkillCheckFailed;
    //소음
    public static event Action<Vector3, float> OnNoise;

    //숨기 이벤트
    public static event Action OnHideEntered;
    public static event Action OnHideExited;

    // 몬스터 무력화, 리스폰
    public static event Action<float> OnMonsterDisableRequested; // duration
    public static event Action OnMonsterRespawned;


    public static void RaiseGimmickCompleted(GimmickNode g) 
        => OnGimmickCompleted?.Invoke(g);
    public static void RaiseSkillCheckFailed(GimmickNode g) 
        => OnSkillCheckFailed?.Invoke(g);

    public static void RaiseNoise(Vector3 pos, float intensity) 
        => OnNoise?.Invoke(pos, intensity);

    public static void RaiseHideEntered() 
        => OnHideEntered?.Invoke();
    public static void RaiseHideExited() 
        => OnHideExited?.Invoke();

    public static void RaiseMonsterDisableRequested(float duration)
        => OnMonsterDisableRequested?.Invoke(duration);
    public static void RaiseMonsterRespawned()
        => OnMonsterRespawned?.Invoke();
}
