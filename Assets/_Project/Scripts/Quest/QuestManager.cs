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
            PrimeStartTaskHud();
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(startEvent))
                Invoke(nameof(FireStartEvent), 0.6f);
        }

        void FireStartEvent() => FireEvent(startEvent);

        void PrimeStartTaskHud()
        {
            if (string.IsNullOrEmpty(startEvent)) return;
            DlgNode best = null;
            foreach (DlgNode node in db.Nodes)
            {
                if (node.@event != startEvent || string.IsNullOrEmpty(node.task_id) || !ConditionsOk(node))
                    continue;
                if (best == null || node.priority > best.priority) best = node;
            }
            if (best != null) UpdateTaskHud(best.task_id);
        }

        void SetDefaults()
        {
            state["sample_task_accepted"] = "false";
            state["sample_count"] = "0";
            state["sample_delivered"] = "false";
            state["exhibit_complete"] = "false";
        }

        // ---------------- 对外入口 ----------------

        public bool FireEvent(string ev)
        {
            DlgNode best = null;
            foreach (var n in db.Nodes)
            {
                if (n.@event != ev || !ConditionsOk(n)) continue;
                if (best == null || n.priority > best.priority) best = n;
            }
            if (best == null) return false;
            RunNode(best);
            return true;
        }

        public void OnNpc(string characterId)
        {
            if (!FireEvent("interact_" + characterId))
                ShowSub("系统", "这名队员暂时没有新的任务信息。");
        }

        public string GetState(string field)
            => state.TryGetValue(field, out string value) ? value : null;

        /// <summary>查询某个固定勘测点是否已经采集，供导航标记在样本实体销毁后继续正确更新。</summary>
        public bool IsSampleCollected(string pointId)
            => !string.IsNullOrEmpty(pointId) && collectedPoints.Contains(pointId);

        /// <summary>按节点与选项 ID 执行选择；供 UI、存档恢复和自动验收共用。</summary>
        public bool TrySelectChoice(string nodeId, string choiceId)
        {
            foreach (var node in db.Nodes)
            {
                if (node.id != nodeId || !ConditionsOk(node) || node.choices == null) continue;
                foreach (var choice in node.choices)
                    if (choice.id == choiceId) return ExecuteChoice(choice);
            }
            return false;
        }

        public bool OnSamplePoint(string pointId)
        {
            if (state.GetValueOrDefault("sample_task_accepted") != "true")
            {
                ShowSub("系统", "尚未接取采样任务。先到广寒站找周航。");
                return false;
            }
            if (state.GetValueOrDefault("sample_delivered") == "true")
            {
                ShowSub("系统", "样本已交付，任务完成。");
                return false;
            }
            if (!collectedPoints.Add(pointId))
            {
                ShowSub("系统", "该勘测点已采集过。");
                return false;
            }
            int count = collectedPoints.Count;
            state["sample_count"] = count.ToString();
            FireEvent("sample_committed");
            if (count >= 3) ShowSub("系统", "三份样本已齐，回广寒站交给许青。");
            return true;
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
            if (!ApplyEffects(n.effects)) return;
            if (!string.IsNullOrEmpty(n.text)) ShowSub(n);
            if (!string.IsNullOrEmpty(n.task_id)) UpdateTaskHud(n.task_id);
            if (n.choices != null && n.choices.Length > 0) PresentChoices(n);
            ApplyEffects(n.on_complete_effects);
        }

        bool ApplyEffects(DlgEffect[] effects)
        {
            if (effects == null) return true;
            foreach (var e in effects)
            {
                if (e == null || string.IsNullOrEmpty(e.type)) continue;

                switch (e.type)
                {
                    case "set_state":
                        if (!string.IsNullOrEmpty(e.field)) state[e.field] = e.value;
                        break;

                    case "deliver_samples_once":
                    {
                        int required = Mathf.Max(1, e.required_count);
                        int.TryParse(state.GetValueOrDefault("sample_count"), out int count);
                        bool accepted = state.GetValueOrDefault("sample_task_accepted") == "true";
                        bool delivered = state.GetValueOrDefault("sample_delivered") == "true";
                        if (!accepted || delivered || count < required)
                        {
                            ShowSub("系统", delivered ? "样本已经交付。" : $"样本不足，需要 {required} 份。");
                            return false;
                        }
                        state["sample_count"] = "0";
                        state["sample_delivered"] = "true";
                        break;
                    }

                    case "start_outbound_sequence":
                        SceneFlow.StartOutboundSequence(2.5f);
                        break;

                    case "start_return_sequence":
                        SceneFlow.StartReturnSequence(2.5f);
                        break;

                    default:
                        Debug.LogWarning("[QuestManager] 未识别的任务效果: " + e.type);
                        break;
                }
            }
            return true;
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
                if (t.id == taskId)
                {
                    string title = ExpandTaskText(t.title);
                    string objective = ExpandTaskText(t.objective);
                    hud.SetQuest(string.IsNullOrEmpty(objective) ? title : title + "\n" + objective);
                    return;
                }
        }

        string ExpandTaskText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string count = state.GetValueOrDefault("sample_count") ?? "0";
            return text.Replace("{sample_count}", count);
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
            if (node.choices == null || index < 0 || index >= node.choices.Length) return;
            ExecuteChoice(node.choices[index]);
        }

        bool ExecuteChoice(DlgChoice ch)
        {
            if (!ApplyEffects(ch.effects)) return false;
            if (!string.IsNullOrEmpty(ch.next_node))
                foreach (var n in db.Nodes)
                    if (n.id == ch.next_node && ConditionsOk(n))
                    {
                        RunNode(n);
                        return true;
                    }
            return string.IsNullOrEmpty(ch.next_node);
        }
    }
}
