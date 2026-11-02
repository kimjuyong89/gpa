using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GPA.World
{
    // RegionData를 화면에 깔고 주인공을 칸 단위로 걷게 한다.
    // 방향키/WASD로 이동, E·Space·Enter로 앞의 NPC에게 말을 건다. 출구 칸을 밟으면 OnExit.
    // 경공을 익혔으면(Agile) Shift로 달리고 F로 도약한다.
    public class RegionView : MonoBehaviour
    {
        const float StepSpeed = 6f; // 초당 칸
        const float RunMultiplier = 1.8f;
        const float TiredMultiplier = 0.55f;
        const int LeapReach = 3;    // 도약으로 닿는 칸 수
        const float LeapHeight = 0.9f;
        static readonly Vector2Int[] Dirs = { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right }; // 시트 열 순서

        public event Action<NpcDef> OnTalk;
        public event Action OnExit;
        public event Action OnLeap;
        public event Action<TileDef> OnStep; // 한 칸 도착할 때마다
        public bool Paused;
        public bool Agile;   // 경공을 익혔는가
        public bool Running; // 달리는 중
        public bool Tired;   // 피로가 다 차서 느리다

        RegionData data;
        bool[,] solid;
        int width, height;
        readonly Dictionary<Vector2Int, (NpcDef def, SpriteRenderer sr, Sprite[,] frames)> npcs = new();
        readonly Dictionary<(Texture2D, RectInt), Sprite> spriteCache = new();

        Transform player;
        SpriteRenderer playerSr;
        Sprite[,] playerFrames;
        Vector2Int cell, target, previous;
        int facing;
        float moveT = 1;
        int leapCells;       // 도약 중이면 칸 수, 아니면 0

        // start를 주면 그 칸에서 시작한다. 없으면 지도의 시작 글자 위치.
        public static RegionView Create(RegionData data, Texture2D playerSheet, Vector2Int? start = null)
        {
            var view = new GameObject($"Region {data.name}").AddComponent<RegionView>();
            view.Build(data, playerSheet, start);
            return view;
        }

        Sprite Slice(Texture2D sheet, Vector2Int cell, Vector2Int size)
        {
            var rect = new RectInt(cell.x * 16, sheet.height - (cell.y + size.y) * 16, size.x * 16, size.y * 16);
            if (!spriteCache.TryGetValue((sheet, rect), out var s))
                spriteCache[(sheet, rect)] = s = Sprite.Create(sheet, new Rect(rect.x, rect.y, rect.width, rect.height), Vector2.zero, 16);
            return s;
        }

        // 걷기 프레임: [방향, 프레임]
        Sprite[,] Frames(Texture2D sheet)
        {
            var f = new Sprite[4, 4];
            for (int d = 0; d < 4; d++)
                for (int i = 0; i < 4; i++)
                    f[d, i] = Slice(sheet, new Vector2Int(d, i), Vector2Int.one);
            return f;
        }

        // 물체는 어느 쪽에서 봐도 같은 그림이다.
        Sprite[,] Still(NpcDef npc)
        {
            var f = new Sprite[4, 4];
            for (int d = 0; d < 4; d++)
                for (int i = 0; i < 4; i++)
                    f[d, i] = Slice(npc.prop, npc.propCell, Vector2Int.one);
            return f;
        }

        SpriteRenderer Place(string name, Sprite sprite, Vector2 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // 아래쪽에 있을수록 앞에 그린다.
        static int OrderAt(float y) => 1000 - Mathf.RoundToInt(y * 10);

        Vector2 World(int x, int row) => new(x, height - 1 - row);

        void Build(RegionData data, Texture2D playerSheet, Vector2Int? start)
        {
            this.data = data;
            var rows = data.Rows;
            height = rows.Length;
            width = 0;
            foreach (var r in rows) width = Mathf.Max(width, r.Length);
            solid = new bool[width, height];

            for (int row = 0; row < height; row++)
            for (int x = 0; x < width; x++)
            {
                char c = x < rows[row].Length ? rows[row][x] : ' ';
                if (c == ' ') continue;
                bool special = data.start.IndexOf(c) == 0 || data.exit.IndexOf(c) == 0 || data.Npc(c) != null;
                var def = special ? data.Tile(data.specialGround[0]) : data.Tile(c);
                if (def == null) continue;

                bool isProp = def.size != Vector2Int.one || def.overlay;
                var ground = isProp ? data.Tile(def.ground[0]) : def;
                if (ground != null) Place($"{x},{row}", Slice(ground.sheet, ground.cell, Vector2Int.one), World(x, row), -1000);
                if (ground != null && ground.solid) solid[x, row] = true;

                if (isProp)
                {
                    var bottomLeft = new Vector2(x, height - row - def.size.y);
                    Place($"{def.key}@{x},{row}", Slice(def.sheet, def.cell, def.size), bottomLeft, OrderAt(bottomLeft.y));
                    if (def.solid)
                        for (int dy = 0; dy < def.size.y; dy++)
                        for (int dx = 0; dx < def.size.x; dx++)
                            if (x + dx < width && row + dy < height) solid[x + dx, row + dy] = true;
                }

                if (data.start.IndexOf(c) == 0) cell = new Vector2Int(x, row);
                if (data.Npc(c) is { } npc)
                {
                    var frames = npc.prop != null ? Still(npc) : Frames(npc.sheet);
                    var pos = World(x, row);
                    npcs[new Vector2Int(x, row)] = (npc, Place(npc.name, frames[0, 0], pos, OrderAt(pos.y) + 5), frames);
                }
            }

            if (start.HasValue) cell = start.Value;
            playerFrames = Frames(playerSheet);
            playerSr = Place("Player", playerFrames[0, 0], World(cell.x, cell.y), 0);
            player = playerSr.transform;
            target = previous = cell;
            UpdatePlayer();
        }

        bool Walkable(Vector2Int c) =>
            c.x >= 0 && c.y >= 0 && c.x < width && c.y < height && !solid[c.x, c.y] && !npcs.ContainsKey(c);

        // 칸 좌표는 위에서부터 세므로 위로 가면 row가 줄어든다.
        static Vector2Int Step(int dir) => new(Dirs[dir].x, -Dirs[dir].y);

        void Update()
        {
            if (moveT < 1)
            {
                float speed = leapCells > 0 ? StepSpeed / leapCells : Running && Agile ? StepSpeed * RunMultiplier : StepSpeed;
                if (Tired) speed *= TiredMultiplier;
                moveT = Mathf.Min(1, moveT + Time.deltaTime * speed);
                if (moveT >= 1)
                {
                    previous = cell;
                    cell = target;
                    leapCells = 0;
                    if (Exits(cell)) OnExit?.Invoke();
                    else OnStep?.Invoke(TileAt(cell));
                }
            }
            else if (!Paused) ReadInput();
            UpdatePlayer();
        }

        TileDef TileAt(Vector2Int c)
        {
            var rows = data.Rows;
            return c.x < rows[c.y].Length ? data.Tile(rows[c.y][c.x]) : null;
        }

        // 막 떠나온 칸. 입구에서 돌아올 자리로 쓴다.
        public Vector2Int Previous => previous;

        // 잠긴 곳을 밟았을 때 되돌린다.
        public void StepBack()
        {
            cell = target = previous;
            moveT = 1;
        }

        bool Exits(Vector2Int c)
        {
            var rows = data.Rows;
            return c.x < rows[c.y].Length && data.exit.IndexOf(rows[c.y][c.x]) == 0;
        }

        void ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            int dir = kb.downArrowKey.isPressed || kb.sKey.isPressed ? 0
                : kb.upArrowKey.isPressed || kb.wKey.isPressed ? 1
                : kb.leftArrowKey.isPressed || kb.aKey.isPressed ? 2
                : kb.rightArrowKey.isPressed || kb.dKey.isPressed ? 3 : -1;
            Running = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.fKey.wasPressedThisFrame) Leap();
            if (dir >= 0) Move(dir);
            if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Interact();
        }

        public Vector2Int Cell => cell;
        public bool Moving => moveT < 1;

        // 그 방향을 보고, 막히지 않았으면 한 칸 걷기 시작한다. dir: 0 아래, 1 위, 2 왼쪽, 3 오른쪽
        public void Move(int dir)
        {
            if (Moving) return;
            facing = dir;
            var next = cell + Step(dir);
            if (!Walkable(next)) return;
            target = next;
            moveT = 0;
        }

        // 경공 도약: 바라보는 쪽으로 가장 멀리 내릴 수 있는 칸까지 뛰어넘는다. 건물처럼 두꺼운 곳은 못 넘는다.
        public bool Leap()
        {
            if (!Agile || Moving) return false;
            for (int d = LeapReach; d >= 2; d--)
            {
                var landing = cell + Step(facing) * d;
                if (!Walkable(landing)) continue;
                target = landing;
                moveT = 0;
                leapCells = d;
                OnLeap?.Invoke();
                return true;
            }
            return false;
        }

        // 바라보는 칸의 NPC에게 말을 건다.
        public void Interact()
        {
            if (!npcs.TryGetValue(cell + Step(facing), out var npc)) return;
            int back = facing switch { 0 => 1, 1 => 0, 2 => 3, _ => 2 }; // NPC가 주인공을 본다
            npc.sr.sprite = npc.frames[back, 0];
            OnTalk?.Invoke(npc.def);
        }

        void UpdatePlayer()
        {
            var from = World(cell.x, cell.y);
            var to = World(target.x, target.y);
            var pos = Vector2.Lerp(from, to, moveT);
            float lift = leapCells > 0 ? Mathf.Sin(moveT * Mathf.PI) * LeapHeight : 0;
            player.localPosition = pos + Vector2.up * lift;
            bool walking = moveT < 1;
            playerSr.sprite = playerFrames[facing, walking ? (int)(Time.time * 8) % 4 : 0];
            playerSr.sortingOrder = OrderAt(pos.y) + (leapCells > 0 ? 500 : 5); // 뛰는 동안은 무엇보다 앞에

            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.orthographicSize = 7.5f;
            float halfW = cam.orthographicSize * cam.aspect, halfH = cam.orthographicSize;
            var center = pos + new Vector2(0.5f, 0.5f);
            // 지도가 화면보다 크면 지도 밖이 보이지 않게 붙잡는다.
            center.x = width > halfW * 2 ? Mathf.Clamp(center.x, halfW, width - halfW) : width / 2f;
            center.y = height > halfH * 2 ? Mathf.Clamp(center.y, halfH, height - halfH) : height / 2f;
            cam.transform.position = new Vector3(center.x, center.y, -10);
        }
    }
}
