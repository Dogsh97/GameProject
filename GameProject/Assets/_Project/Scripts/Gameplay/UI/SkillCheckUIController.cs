using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class SkillCheckUIController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform bar;          // BarBG의 RectTransform
        [SerializeField] private RectTransform successZone;  // SuccessZone RectTransform
        [SerializeField] private RectTransform needle;       // Needle RectTransform

        [Header("Tuning")]
        [SerializeField] private float speed = 700f;         // px/sec
        [SerializeField] private float duration = 2.0f;      // 제한 시간
        [SerializeField] private KeyCode hitKey = KeyCode.Space;

        private Action<bool> onFinished;
        private float timeLeft;
        private float dir = 1f;
        private bool running;

        public void Begin(Action<bool> onFinished)
        {
            this.onFinished = onFinished;
            timeLeft = duration;
            dir = 1f;
            running = true;
            gameObject.SetActive(true);

            // 시작 위치 왼쪽
            SetNeedleX(-bar.rect.width * 0.5f);
        }

        private void Update()
        {
            if (!running) return;

            // 시간 감소
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                Finish(false);
                return;
            }

            // Needle 이동(바 좌우 끝에서 반사)
            float x = needle.anchoredPosition.x + dir * speed * Time.deltaTime;
            float half = bar.rect.width * 0.5f;

            if (x > half) { x = half; dir = -1f; }
            if (x < -half) { x = -half; dir = 1f; }

            SetNeedleX(x);

            // 입력
            if (Input.GetKeyDown(hitKey))
            {
                bool success = IsNeedleInSuccessZone();
                Finish(success);
            }
        }

        private void Finish(bool success)
        {
            running = false;
            gameObject.SetActive(false);
            onFinished?.Invoke(success);
            onFinished = null;
        }

        private bool IsNeedleInSuccessZone()
        {
            float needleX = needle.anchoredPosition.x;

            // successZone의 로컬 범위 계산
            float zoneCenter = successZone.anchoredPosition.x;
            float zoneHalf = successZone.rect.width * 0.5f;

            return needleX >= (zoneCenter - zoneHalf) && needleX <= (zoneCenter + zoneHalf);
        }

        private void SetNeedleX(float x)
        {
            var p = needle.anchoredPosition;
            p.x = x;
            needle.anchoredPosition = p;
        }
    }
}
