using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LunarQuest
{
    // dialogue.json 的可序列化映射。字段名与 JSON 键一致；
    // "event"/"operator" 是 C# 关键字，用 @ 前缀保留原名（JsonUtility 按原名匹配）。
    [Serializable] public class DlgCond { public string field; public string @operator; public string value; }
    [Serializable] public class DlgEffect { public string type; public string field; public string value; public int required_count; }
    [Serializable] public class DlgChoice { public string id; public string text; public DlgEffect[] effects; public string next_node; }
    [Serializable] public class DlgNode
    {
        public string id; public string task_id; public string @event; public int priority;
        public DlgCond[] conditions; public string speaker; public string channel;
        public string action; public string text; public DlgChoice[] choices; public DlgEffect[] effects;
    }
    [Serializable] public class DlgTask { public string id; public string title; public string completion; public string next; }
    [Serializable] public class DlgChar { public string id; public string display_name; public string role; }
    [Serializable] class DlgWrapper { public DlgNode[] nodes; public DlgTask[] tasks; public DlgChar[] characters; }

    /// <summary>
    /// 对话/任务数据库：解析 Content/Characters/dialogue.json（33 节点、9 任务）。
    /// JSON 中 value 字段混用布尔/数字/字符串，加载前统一转为字符串。
    /// </summary>
    public class DialogueDatabase
    {
        public readonly List<DlgNode> Nodes = new List<DlgNode>();
        public readonly List<DlgTask> Tasks = new List<DlgTask>();
        public readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>();

        public static DialogueDatabase Load(TextAsset asset)
        {
            string raw = asset.text;
            raw = Regex.Replace(raw, "\"value\"\\s*:\\s*(true|false)", "\"value\":\"$1\"");
            raw = Regex.Replace(raw, "\"value\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", "\"value\":\"$1\"");
            var w = JsonUtility.FromJson<DlgWrapper>(raw);
            var db = new DialogueDatabase();
            if (w == null) return db;
            if (w.nodes != null) db.Nodes.AddRange(w.nodes);
            if (w.tasks != null) db.Tasks.AddRange(w.tasks);
            if (w.characters != null)
                foreach (var c in w.characters)
                    db.DisplayNames[c.id] = c.display_name;
            return db;
        }
    }
}
