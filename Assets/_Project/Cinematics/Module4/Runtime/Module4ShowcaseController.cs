using System;
using UnityEngine;
using Cinemachine;

namespace LunarQuest.Module4
{
    [DefaultExecutionOrder(-100)]
    public sealed class Module4ShowcaseController : MonoBehaviour
    {
        public Module4Sequence[] sequences;
        public GameObject moonWorld;
        public CinemachineVirtualCamera moonCamera;
        public Camera mainCamera;
        public Module4HudView hud;
        public AudioSource moonMusic;
        public AudioSource moonVoice;
        public AudioClip[] hudVoices;
        public float totalDuration = 194;
        public bool externalClock;
        public double forcedTime;
        public bool paused;
        public double clock;
        int active = -99;
        int voiced = -1;
        public int completedSignals;
        public bool exportMode;
        string smokeResult;

        void Start()
        {
            foreach (var sequence in sequences)
            {
                sequence.Bind(mainCamera, hud);
                sequence.onCompleted.AddListener(() => completedSignals++);
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-m4smoke")smokeResult=args[i+1];
            if(!string.IsNullOrEmpty(smokeResult)) { Time.timeScale=20; AudioListener.pause=true; }
        }

        public void Jump(double time)
        {
            clock = time;
            active = -99;
        }

        void Update()
        {
            if (externalClock) clock = forcedTime;
            else if (!paused) clock += Time.deltaTime;
            if (!externalClock)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    paused = !paused;
                    foreach (var sequence in sequences)
                        if (sequence.gameObject.activeSelf)
                        {
                            if (paused) sequence.director.Pause(); else sequence.director.Resume();
                        }
                }
                if (Input.GetKeyDown(KeyCode.Alpha1)) Jump(8);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Jump(69);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Jump(100);
                if (Input.GetKeyDown(KeyCode.Alpha4)) Jump(141);
                if (Input.GetKeyDown(KeyCode.R)) { paused = false; Jump(0); }
                if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
                if (clock >= totalDuration)
                {
                    clock = totalDuration - .01; paused = true;
                    if(!string.IsNullOrEmpty(smokeResult))
                    {
                        var tuning=hud.landingPanel.GetComponent<Module4LandingTuning>();
                        var slider=tuning.GetComponentsInChildren<UnityEngine.UI.Slider>(true)[0];float previous=slider.value;slider.value=3.2f;
                        string tuningPath=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(smokeResult),"landing-tuning-smoke.json");
                        tuning.SaveTo(tuningPath);
                        var saved=JsonUtility.FromJson<Module4LandingTuning.Settings>(System.IO.File.ReadAllText(tuningPath));
                        bool uiPassed=Mathf.Abs(saved.verticalLimit-3.2f)<.001f;slider.value=previous;
                        bool passed=completedSignals==3&&uiPassed;
                        System.IO.File.WriteAllText(smokeResult,"{\"completedSignals\":"+completedSignals+",\"expectedSignals\":3,\"landingSliderAndJson\":"+(uiPassed?"true":"false")+",\"passed\":"+(passed?"true":"false")+"}");
                        Application.Quit(passed?0:2);
                        return;
                    }
                }
            }

            float t = (float)clock;
            int section = t < 69 ? 0 : t < 100 ? 1 : t < 141 ? 3 : 2;
            if (section != active) Activate(section);
            float local = section == 0 ? Mathf.Max(0, t - 8) : section == 1 ? t - 69 : section == 2 ? t - 141 : t - 100;
            if (section < 3)
            {
                var sequence = sequences[section];
                if (externalClock || t < 8) sequence.Seek(local);
                if (t < 8) sequence.director.Pause();
                else if (!externalClock && !paused && sequence.director.state != UnityEngine.Playables.PlayState.Playing && local < sequence.duration-.1)
                    sequence.director.Resume();
            }

            hud.progress.fillAmount = Mathf.Clamp01(t / totalDuration);
            hud.missionClock.text = "MET  " + TimeSpan.FromSeconds(t).ToString(@"mm\:ss");
            hud.footerText.text = exportMode ? "LUNARQUEST   /   演出与视听展示" : "1 发射    2 地出    3 界面    4 返回    SPACE 暂停    R 重播    ESC 退出";
            hud.landingText.gameObject.SetActive(section == 3 && local > 28);
            hud.landingPanel.SetActive(section == 3 && local > 28);
            hud.minimapArrow.parent.gameObject.SetActive(section == 3);
            hud.dialoguePanel.SetActive(section == 3 && local >= 10 && local < 20);
            hud.dialogueText.text = "基地指挥官\n\n欢迎来到广寒站。三号勘测点已标记在小地图上。\n按 E 接受科考任务。";
            if (section == 0)
            {
                hud.SetChapter("01", "蓄势 · 启程", "LAUNCH / EARTH DEPARTURE");
                float rise = Mathf.Max(0, local-20);
                hud.SetTelemetry(rise*rise*.8f, rise*rise*.45f, 100, "发射准备 / 地月转移", local < 18 ? "倒计时  T−"+Mathf.CeilToInt(Mathf.Max(0,18-local)).ToString("00") : "点火正常 · 姿态锁定");
            }
            else if (section == 1)
            {
                hud.SetChapter("02", "绕月 · 地出", "ORBIT / EARTHRISE");
                hud.SetTelemetry(5840, 100000, 98, "环月观测 / 地出摄影", "保持姿态 · 观测窗口已开启");
            }
            else if (section == 2)
            {
                hud.SetChapter("04", "携样 · 归航", "RETURN / HOMEWARD");
                hud.SetTelemetry(local < 32 ? 360+local*120 : 26-(local-32), Mathf.Max(0,100000-local*2400), 94, "携带样本返回地球", local < 22 ? "上升器发射 / 轨道交会" : local < 32 ? "再入大气层 / 热防护" : "主伞展开 / 稳定下降");
            }
            else
            {
                hud.SetChapter("03", "月面 · 广寒站", "LUNAR SURFACE / HUD SHOWCASE");
                float speed = local < 10 ? 8+Mathf.Sin(local)*4 : local < 24 ? 22+Mathf.Sin(local)*7 : 0;
                hud.SetTelemetry(speed, 0, 97-local*.08f, local < 13 ? "任务 02 · 前往三号勘测点" : local < 25 ? "任务 03 · 采集月壤样本  2 / 3" : "科考任务完成 · 返回基地", local < 10 ? "按 F 进入月球车" : local < 24 ? "按住 E 进行采样" : "样本已封装 · 等待移交");
                hud.minimapArrow.localEulerAngles = new Vector3(0,0,-local*8);
                hud.landingText.text = "着陆参数面板\n下降速度上限  2.5 m/s\n水平速度上限  1.0 m/s\n反推响应       0.65\n演示配置 · 由程序模块接入判定";
                if (local < 6) hud.SetSubtitle("基地飞控", "欢迎来到广寒站。");
                else if (local > 12 && local < 19) hud.SetSubtitle("基地指挥官", "交互界面已就绪，请前往三号勘测点。");
                else if (local > 25 && local < 31) hud.SetSubtitle("随行科学家", "月壤样本已采集，任务完成。");
                else hud.SetSubtitle("", "");
                int line = local >= 25 ? 2 : local >= 12 ? 1 : local >= 1 ? 0 : -1;
                if (!externalClock && line != voiced && line >= 0)
                { moonVoice.PlayOneShot(hudVoices[line], .8f); voiced = line; }
            }
            bool intro = t < 8;
            bool outro = t >= 186;
            hud.SetTitle(intro ? "月征  绕 · 落 · 回" : "让探索成为亲历", intro ? "LUNARQUEST\n模块四  演出与视听" : "三段原生演出  /  HUD  /  音频设计\n月球纹理  NASA SVS   ·   字体  Noto Sans CJK\n小组工程及实机 AR 片段由组内联调", intro || outro);
            float fade = 0;
            foreach (float at in new float[]{68,99,140})
                if (t >= at && t < at+1) fade = Mathf.Sin((t-at)*Mathf.PI);
            if (t > 192) fade = Mathf.Max(fade,(t-192)/2);
            hud.SetFade(fade);
        }

        void Activate(int section)
        {
            foreach (var sequence in sequences) { sequence.director.Stop(); sequence.gameObject.SetActive(false); }
            moonWorld.SetActive(false);
            moonMusic.Stop(); moonVoice.Stop(); voiced = -1;
            hud.SetSubtitle("", "");
            active = section;
            if (section < 3)
            {
                var sequence = sequences[section];
                sequence.gameObject.SetActive(true);
                sequence.Bind(mainCamera,hud);
                sequence.Play();
                if (externalClock) sequence.director.Pause();
            }
            else
            {
                moonWorld.SetActive(true);
                moonCamera.Priority = 100;
                if (!externalClock) moonMusic.Play();
            }
        }
    }
}
