using UnityEngine;

public class EnemyFootstep : MonoBehaviour
{
    [Header("발소리 클립 (여러 개 넣으면 랜덤 재생)")]
    public AudioClip[] footstepClips;

    [Header("발소리 간격 (초)")]
    public float stepInterval = 0.5f;

    //[Header("볼륨 범위 (자연스럽게)")]
    //public float minVolume = 0.8f;
    //public float maxVolume = 1.0f;

    AudioSource audioSource;
    float stepTimer = 0f;
    bool isWalking = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (isWalking)
        {
            stepTimer += Time.deltaTime;

            if (stepTimer >= stepInterval)
            {
                PlayFootstep();
                stepTimer = 0f;
            }
        }
        else
        {
            // 걷지 않으면 타이머 리셋
            stepTimer = 0f;
        }
    }

    void PlayFootstep()
    {
        if (audioSource.isPlaying) return;
        if (footstepClips.Length == 0) return;

        // 랜덤 클립 선택
        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        //float volume = Random.Range(minVolume, maxVolume);

        audioSource.Play();
    }

    // 외부(적 AI 스크립트)에서 호출
    public void SetWalking(bool walking)
    {
        isWalking = walking;
    }
}