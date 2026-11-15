using System;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LunarQuest.Module4
{
    // Presentation-owned settings. Gameplay reads Values and owns the landing verdict.
    public sealed class Module4LandingTuning : MonoBehaviour
    {
        [Serializable] public class Settings
        {
            public float verticalLimit = 2.5f;
            public float horizontalLimit = 1f;
            public float thrustResponse = .65f;
        }
        public Font font;
        public Settings values = new Settings();
        public UnityEvent onChanged = new UnityEvent();
        public Settings Values => values;
        Slider[] sliders;
        Text status;
        public string SavePath => Path.Combine(Application.persistentDataPath,"module4-landing.json");

        void Awake()
        {
            if(File.Exists(SavePath)) { try { values=JsonUtility.FromJson<Settings>(File.ReadAllText(SavePath)); } catch { values=new Settings(); } }
            var bg=gameObject.AddComponent<Image>();bg.color=new Color(.015f,.035f,.06f,.94f);bg.raycastTarget=false;
            TextAt("着陆参数调节",26,25,40,400);
            sliders=new Slider[3];
            AddSlider(0,"下降速度上限  m/s",.5f,6,values.verticalLimit,86);
            AddSlider(1,"水平速度上限  m/s",.1f,4,values.horizontalLimit,160);
            AddSlider(2,"反推响应",.1f,1,values.thrustResponse,234);
            AddButton("保存配置",25,320,Save);
            AddButton("恢复默认",232,320,ResetDefaults);
            status=TextAt("演示配置 · 判定由程序组接入",16,25,366,410);
        }
        Text TextAt(string text,int size,float x,float y,float width)
        {
            var g=new GameObject(text,typeof(RectTransform),typeof(Text));g.transform.SetParent(transform,false);
            var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,38);
            var t=g.GetComponent<Text>();t.font=font;t.fontSize=size;t.color=new Color(.85f,.94f,.98f);t.text=text;t.raycastTarget=false;return t;
        }
        void AddSlider(int index,string name,float min,float max,float value,float y)
        {
            var label=TextAt(name+"   "+value.ToString("0.00"),19,25,y,410);
            var g=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Slider));g.transform.SetParent(transform,false);
            var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(25,-y-42);r.sizeDelta=new Vector2(400,10);
            g.GetComponent<Image>().color=new Color(.12f,.22f,.3f);
            var knob=new GameObject("Handle",typeof(RectTransform),typeof(Image));knob.transform.SetParent(g.transform,false);var kr=(RectTransform)knob.transform;kr.sizeDelta=new Vector2(22,28);knob.GetComponent<Image>().color=new Color(.25f,.82f,.93f);
            var s=g.GetComponent<Slider>();s.minValue=min;s.maxValue=max;s.handleRect=kr;s.targetGraphic=knob.GetComponent<Image>();s.value=value;sliders[index]=s;
            s.onValueChanged.AddListener(v=> {if(index==0)values.verticalLimit=v;else if(index==1)values.horizontalLimit=v;else values.thrustResponse=v;label.text=name+"   "+v.ToString("0.00");onChanged.Invoke();});
        }
        void AddButton(string text,float x,float y,UnityAction action)
        {
            var g=new GameObject(text+" Button",typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(transform,false);
            var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(190,38);
            var bg=g.GetComponent<Image>();bg.color=new Color(.06f,.27f,.36f);var b=g.GetComponent<Button>();b.targetGraphic=bg;b.onClick.AddListener(action);
            var label=TextAt(text,19,0,0,190);label.transform.SetParent(g.transform,false);label.alignment=TextAnchor.MiddleCenter;
        }
        public void SaveTo(string path) {File.WriteAllText(path,JsonUtility.ToJson(values,true));}
        public void Save() {SaveTo(SavePath);status.text="已保存到本机 · 可由程序模块读取";}
        public void ResetDefaults() {values=new Settings();sliders[0].value=values.verticalLimit;sliders[1].value=values.horizontalLimit;sliders[2].value=values.thrustResponse;status.text="已恢复默认值 · 点击保存后生效";onChanged.Invoke();}
    }
}
