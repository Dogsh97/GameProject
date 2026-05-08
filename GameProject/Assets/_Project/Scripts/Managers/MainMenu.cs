using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
public class MainMenu : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string sceneName = "10_Gameplay_Stage1"; // 인게임 씬 이름
    [SerializeField] private GameObject titleUI;            // 타이틀 UI 부모 객체

    [Header("Audio Settings")]
    [SerializeField] private AudioMixer masterMixer;        // 사용할 오디오 믹서
    private const string BGM_PARAM = "BGMVol";        // 익스포즈된 파라미터 이름
    private const string SFX_PARAM = "SFXVol";

    void Awake()
    {
        // 1. 시작하자마자 엔진 전체 소리를 0으로 고정 (무조건 무음)
        AudioListener.volume = 0f;
    }

    void Start()
    {
        // 2. 타이틀 시작 시 게임 씬을 배경으로 로드 (중복 로드 방지)
        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            SceneManager.sceneLoaded += OnSceneLoaded; // 리스너 해결을 위한 콜백 등록
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }
    }

    // 씬 로딩 완료 후 실행 (리스너 중복 해결)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
            if (scene.name == sceneName)
        {
            // 3. 게임 씬의 리스너만 찾아 꺼서 경고 메시지 방지
            AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
            foreach (var listener in listeners)
            {
                // 로드된 게임 씬에 속한 리스너만 비활성화
                if (listener.gameObject.scene.name == sceneName)
                {
                    listener.enabled = false;
                }
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    public void ClickStart() // 게임시작 버튼
    {
        Debug.Log("게임시작");

       // 4. 타이틀 UI 끄기
        if (titleUI != null) titleUI.SetActive(false);

        // 5. 전체 볼륨 복구 (이제부터 소리가 들림)
        AudioListener.volume = 1f;

        // 6. 게임 씬의 리스너를 다시 켜줌
        AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
        foreach (var listener in listeners)
        {
            if (listener.gameObject.scene.name == sceneName)
            {
                listener.enabled = true;
            }
        }

        // 7. 게임 씬을 활성 씬으로 변경 (라이팅/물리 적용)
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));        
    }

    public void ClickLoad() // 게임 로드 버튼
    {
        Debug.Log("게임로드");
        // 세이브 데이터 로드 로직 추가 공간
    }

    public void ClickSet() // 환경설정 버튼
    {
        Debug.Log("환경설정");
        // 설정창 팝업 로직 추가 공간
    }

    public void ClickExit() // 게임 나가기 버튼
    {
        Debug.Log("게임종료");

        #if UNITY_EDITOR
            // 에디터에서 플레이 모드 종료
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // 실제 빌드된 게임 종료
            Application.Quit();
        #endif
    }

    private void SetMixerVolume(float volume)
    {
        if (masterMixer != null)
        {
            masterMixer.SetFloat(BGM_PARAM, volume);
            masterMixer.SetFloat(SFX_PARAM, volume);
        }
    }
}
