using System;
using System.Collections.Generic;
using System.Linq;
using GPA.Combat;
using GPA.Growth;
using UnityEngine;

namespace GPA.Narrative
{
    // 게임 상태 ↔ JSON. 무공은 에셋 이름(파일명)으로 저장한다.
    // 전투 데이터·동료 설정처럼 에셋에서 다시 만들 수 있는 것은 저장하지 않는다.
    public static class SaveGame
    {
        const int Version = 4; // 2: 7스탯, 3: 아이템, 4: 전진파 기본 무공

        [Serializable] class Entry { public string key; public int value; }

        [Serializable]
        class QuestData
        {
            public string id;
            public int stage;
            public bool done;
            public List<string> notes;
        }

        [Serializable]
        class Data
        {
            public int version;
            public string name;
            public int appearance;
            public Stats stats;
            public int realm, innerPower, fatigue, money;
            public bool innerInjured, enlightened;
            public int[] statProgress;
            public List<string> arts;
            public List<Entry> mastery, caps, values, bag;
            public List<Entry> forged = new();
            public string activeMind = "";
            public string legacyMind = "";
            public int legacyExp;
            public List<Entry> skills = new(), skillProgress = new();
            public List<string> equipped; // 칸마다 하나. 빈 칸은 ""
            public List<QuestData> quests;
            public string sect;
            public string location;
            public bool disciple;
            public List<string> expelled;
            public string story; // Ink 진행 상태
        }

        static List<Entry> Entries<T>(Dictionary<T, int> d, Func<T, string> key) =>
            d.Select(kv => new Entry { key = key(kv.Key), value = kv.Value }).ToList();

        public static bool IsCompatible(string json) => JsonUtility.FromJson<Data>(json).version == Version;

        public static string Write(GameState s, string storyState)
        {
            var h = s.Hero;
            var data = new Data
            {
                version = Version,
                name = h.Name,
                appearance = h.Appearance,
                stats = h.Stats,
                realm = (int)h.Realm,
                innerPower = h.InnerPower,
                fatigue = h.Fatigue,
                money = h.Money,
                innerInjured = h.InnerInjured,
                enlightened = h.Enlightened,
                statProgress = h.statProgress,
                arts = h.Arts.Select(a => a.name).ToList(),
                mastery = Entries(h.masteryExp, a => a.name),
                caps = Entries(h.MasteryCaps, a => a.name),
                forged = Entries(h.Forged, a => a.name),
                activeMind = h.ActiveMind != null ? h.ActiveMind.name : "",
                legacyMind = h.legacyMind != null ? h.legacyMind.name : "",
                legacyExp = h.legacyExp,
                skills = Entries(h.SkillLevels, k => k.ToString()),
                skillProgress = Entries(h.SkillProgress, k => k.ToString()),
                values = Entries(s.Values, k => k),
                bag = Entries(h.Bag.counts, i => i.name),
                equipped = h.Equipped.Select(i => i != null ? i.name : "").ToList(),
                quests = s.Quests.Select(q => new QuestData { id = q.Id, stage = q.Stage, done = q.Done, notes = q.Notes.ToList() }).ToList(),
                sect = s.Sect,
                location = s.Location,
                disciple = s.Disciple,
                expelled = s.Expelled.ToList(),
                story = storyState,
            };
            return JsonUtility.ToJson(data, true);
        }

        // 새로 만든 GameState(문파 데이터가 들어 있는)에 덮어쓴다. Ink 진행 상태를 돌려준다.
        // artByName: 에셋 이름으로 무공을 찾는다. 못 찾은 무공은 건너뛴다.
        public static string Read(string json, GameState s, Func<string, MartialArt> artByName)
        {
            var data = JsonUtility.FromJson<Data>(json);
            if (data.version != Version) throw new InvalidOperationException($"세이브 버전이 맞지 않는다: {data.version}");

            var h = s.Hero;
            h.Name = data.name;
            h.Appearance = data.appearance;
            h.Stats = data.stats;
            h.Realm = (Realm)data.realm;
            h.InnerPower = data.innerPower;
            h.Fatigue = data.fatigue;
            h.Money = data.money;
            h.InnerInjured = data.innerInjured;
            h.Enlightened = data.enlightened;
            Array.Copy(data.statProgress, h.statProgress, h.statProgress.Length);

            h.Arts.Clear();
            h.Arts.AddRange(data.arts.Select(artByName).Where(a => a != null));
            Fill(h.masteryExp, data.mastery, artByName);
            Fill(h.MasteryCaps, data.caps, artByName);
            Fill(h.Forged, data.forged, artByName);
            h.ActiveMind = data.activeMind == "" ? null : artByName(data.activeMind);
            h.legacyMind = data.legacyMind == "" ? null : artByName(data.legacyMind);
            h.legacyExp = data.legacyExp;
            foreach (var e in data.skills) h.SkillLevels[Enum.Parse<SkillKind>(e.key)] = e.value;
            foreach (var e in data.skillProgress) h.SkillProgress[Enum.Parse<SkillKind>(e.key)] = e.value;
            h.Bag.counts.Clear();
            foreach (var e in data.bag)
                if (ItemData.Find(e.key) is { } item) h.Bag.Add(item, e.value);
            for (int i = 0; i < h.Equipped.Length; i++) h.Equipped[i] = data.equipped[i] == "" ? null : ItemData.Find(data.equipped[i]);

            s.Values.Clear();
            foreach (var e in data.values) s.Values[e.key] = e.value;
            s.Quests.Clear();
            foreach (var q in data.quests)
            {
                var quest = new Quest { Id = q.id, Stage = q.stage, Done = q.done };
                quest.Notes.AddRange(q.notes);
                s.Quests.Add(quest);
            }
            s.Sect = string.IsNullOrEmpty(data.sect) ? null : data.sect;
            s.Disciple = data.disciple;
            s.Location = data.location;
            s.Expelled.Clear();
            s.Expelled.UnionWith(data.expelled);
            return data.story;
        }

        static void Fill(Dictionary<MartialArt, int> target, List<Entry> entries, Func<string, MartialArt> artByName)
        {
            target.Clear();
            foreach (var e in entries)
                if (artByName(e.key) is { } art) target[art] = e.value;
        }
    }
}
