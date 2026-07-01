using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동을 위해 필요
using UnityEngine.Audio;
public class InGameMenuController : MonoBehaviour
{
    public GameObject pausePanel; // 메뉴 패널 오브젝트
    public static bool isPaused = false; // 일시정지 상태 여부
    public AudioMixer MainAudioMixer;

    void Update()
    {
        // ESC 키를 누르면 메뉴를 켜거나 끕니다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume();
            else Pause();
        }
    }

    // 1. 다시 게임 재개 (메뉴를 닫고 게임으로 복귀)
    public void Resume()
    {
        pausePanel.SetActive(false); // 메뉴판 끄기
        Time.timeScale = 1f;         // 시간 정상화
        isPaused = false;

        // 다시 소리 키우기 (0dB가 기본)
        MainAudioMixer.SetFloat("BGMVol", 0f);
        MainAudioMixer.SetFloat("SFXVol", 0f);

        // 3D 마우스 커서 다시 숨기기
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Pause()
    {
        pausePanel.SetActive(true);  // 메뉴판 켜기
        Time.timeScale = 0f;         // 시간 정지
        isPaused = true;

        //소리 제거
        MainAudioMixer.SetFloat("BGMVol", -80f); 
        MainAudioMixer.SetFloat("SFXVol", -80f);

        // 마우스 커서 보이기 (버튼 클릭용)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 2. 게임 로드 (저장된 지점 불러오기)
    public void LoadGame()
    {
        // 시간을 정상으로 돌린 후 로드 로직 실행
        Time.timeScale = 1f;
        isPaused = false;
        // 다시 소리 키우기 (0dB가 기본)
        MainAudioMixer.SetFloat("BGMVol", 0f);
        MainAudioMixer.SetFloat("SFXVol", 0f);
        Debug.Log("저장된 게임 데이터를 불러옵니다.");
        // 여기에 세이브 시스템 로드 코드를 추가하게 됩니다.
        // SceneManager.LoadScene("실제게임씬이름"); 
    }

    // 3. 게임 끝내기 (타이틀 화면으로 이동)
    public void QuitToTitle()
    {
        Time.timeScale = 1f;
        isPaused = false;
        // 다시 소리 키우기 (0dB가 기본)
        MainAudioMixer.SetFloat("BGMVol", 0f);
        MainAudioMixer.SetFloat("SFXVol", 0f);
        // 빌드 설정(Build Settings)에 등록된 타이틀 씬 이름을 입력하세요.
        SceneManager.LoadScene("Title"); 
    }
}