using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
public class MainMenu : MonoBehaviour
{   
    private string sceneName = "10_Gameplay_Stage1"; // 인게임 씬 이름
 
    public void ClickStart() // 게임시작 버튼을 눌렀을 때 실행될 함수

    {

        Debug.Log("게임시작");

        SceneManager.LoadScene(sceneName);

    }



    public void ClickLoad() // 게임 로드 버튼을 눌렀을 때 실행될 함수

    {

        Debug.Log("게임로드");



    }



    public void ClickSet() // 환경설정 버튼을 눌렀을 때 실행될 함수

    {

        Debug.Log("환경설정");



    }



    public void ClickExit() // 게임 나가기 버튼을 눌렀을 때 실행될 함수

    {

        Debug.Log("게임종료");

        Application.Quit();

    }
}
