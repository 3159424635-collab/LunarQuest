using System.Collections.Generic;
using UnityEngine;

namespace LunarQuest
{
    /// <summary>
    /// 任务与对话运行时（模块一核心系统）。
    /// 事件驱动：各交互点 FireEvent → 按 conditions 筛选节点 → 最高优先级节点播放
    /// （字幕 + NPC 动作 + 效果）；带 choices 的节点在屏幕下方给出选项按钮。
    /// 状态字段与 dialogue.json runtime_contract 对齐。
    /// 挂载要求：场景内任意物体；database 拖 dialogue.json（TextAsset）。
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager I { get; private set; }
        public TextAsset database;
        public string startEvent;   // 场景开场即触发的事件（S4=出舱教学，S1=发射教学）

        DialogueDatabase db;
        readonly Dictionary<string, string> state = new Dictionary<string, string>();
        readonly HashSet<string> collectedPoints = new HashSet<string>();
        HUDController hud;

        void Awake()
        {
            I = this;
            db = database != null ? DialogueDatabase.Load(database) : new DialogueDatabase();
            hud = FindObjectOfType<HUDController>();
            SetDefaults();
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(startEvent))
                Invoke(nameof(FireStartEvent), 0.6f);
        }

        void FireStartEvent() => FireEvent(startEvent);

        void SetDefaults()
        {
            state["sample_task_accepted"] = "false";
            state["sample_count"] = "0";
            state["sample_delivered"] = "false";
            state["exhibit_complete"] = "false";
        }

        // ---------------- 对外入口 ----------------

        public void FireEvent(string ev)
        {
            DlgNode best = null;
            foreach (var n in db.Nodes)
            {
                if (n.@event != ev || !ConditionsOk(n)) continue;
                if (best == null || n.priority > best.priority) best = n;
            }
            if (best != null) RunNode(best);
        }

        public void OnNpc(string characterId) => FireEvent("interact_" + characterId);

        public void OnSamplePoint(string pointId)
        {
            if (state.GetValueOrDefault("sample_task_accepted") != "true")
            {
                ShowSub("系统", "尚未接取采样任务。先到广寒站找周航。");
                return;
            }
            if (state.GetValueOrDefault("sample_delivered") == "true")
            {
                ShowSub("系统", "样本已交付，任务完成。");
                return;
            }
            if (!collectedPoints.Add(pointId))
            {
                ShowSub("系统", "该勘测点已采集过。");
                return;
            }
            int count = collectedPoints.Count;
            state["sample_count"] = count.ToString();
            FireEvent("sample_committed");
            if (count >= 3) ShowSub("系统", "三份样本已齐，回广寒站交给许青。");
        }

        public void OnExhibitOpened() => FireEvent("exhibit_interaction_start");
        public void OnExhibitClosed()
        {
            if (state.GetValueOrDefault("sample_delivered") == "true")
                FireEvent("exhibit_interaction_completed");
        }

        // ---------------- 内部逻辑 ----------------

        void RunNode(DlgNode n)
        {
            ApplyEffects(n.effects);
            if (!string.IsNullOrEmpty(n.text)) ShowSub(n);
            if (!string.IsNullOrEmpty(n.task_id)) UpdateTaskHud(n.task_id);
            if (n.choices != null && n.choices.Length > 0) PresentChoices(n);
        }

        void ApplyEffects(DlgEffect[] effects)
        {
            if (effects == null) return;
            foreach (var e in effects)
                if (e != null && e.type == "set_state" && !string.IsNullOrEmpty(e.field))
                    state[e.field] = e.value;
        }

        bool ConditionsOk(DlgNode n)
        {
            if (n.conditions == null) return true;
            foreach (var c in n.conditions)
            {
                if (c == null) continue;
                state.TryGetValue(c.field, out string v);
                if (v == null) v = IsNumber(c.value) ? "0" : "false";
                if (!Compare(v, c.@operator, c.value)) return false;
            }
            return true;
        }

        static bool IsNumber(string s) => double.TryParse(s, out _);

        static bool Compare(string left, string op, string right)
        {
            if (op == "lt" || op == "gte")
            {
                double.TryParse(left, out double l);
                double.TryParse(right, out double r);
                return op == "lt" ? l < r : l >= r;
            }
            return string.Equals(left?.ToLowerInvariant(), right?.ToLowerInvariant());
        }

        void UpdateTaskHud(string taskId)
        {
            if (hud == null) return;
            foreach (var t in db.Tasks)
                if (t.id == taskId) { hud.SetQuest(t.title); return; }
        }

        void ShowSub(DlgNode n)
        {
            string who = db.DisplayNames.TryGetValue(n.speaker ?? "", out var dn) ? dn : n.speaker;
            ShowSub(who, n.text);
        }

        void ShowSub(string who, string text)
        {
            if (SciFiHUD.I != null)
                SciFiHUD.I.ShowSubtitle(who, text, Mathf.Max(4f, text.Length * 0.12f));
            else if (hud != null) hud.SetPrompt(text);
        }

        // ---------------- 选项（SciFiHUD 赛博风按钮） ----------------

        void PresentChoices(DlgNode n)
        {
            var opts = new List<string>();
            foreach (var c in n.choices) opts.Add(c.text);
            if (SciFiHUD.I != null)
                SciFiHUD.I.ShowChoices(opts, i => ApplyChoice(n, i));
        }

        void ApplyChoice(DlgNode node, int index)
        {
            var ch = node.choices[index];
            ApplyEffects(ch.effects);
            if (!string.IsNullOrEmpty(ch.next_node))
                foreach (var n in db.Nodes)
                    if (n.id == ch.next_node && ConditionsOk(n)) { RunNode(n); break; }
        }
    }
}
