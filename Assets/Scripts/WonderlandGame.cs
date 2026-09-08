using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AliceMirrorfall
{
    /// <summary>
    /// An asset-free, fully playable vertical bullet-hell game. The art, UI, procedural sounds,
    /// local high scores, achievements, touch controls and all gameplay run from this component.
    /// </summary>
    public sealed partial class WonderlandGame : MonoBehaviour
    {
        private const string SaveKey = "alice-mirrorfall-save-v1";
        private const float FieldWidth = 480f;
        private const float FieldHeight = 640f;

        private SaveData save;
        private GameState state = GameState.Title;
        private PlayerUnit player;
        private Queen queen;
        private readonly List<EnemyUnit> foes = new List<EnemyUnit>();
        private readonly List<FriendlyShot> playerShots = new List<FriendlyShot>();
        private readonly List<EnemyShot> enemyShots = new List<EnemyShot>();
        private readonly List<Pickup> pickups = new List<Pickup>();
        private readonly List<Spark> sparks = new List<Spark>();
        private readonly List<Vector4> stars = new List<Vector4>();
        private float scroll, attractTime, stageTime, waveTimer, chain, comboTimer, introTimer, lastShotSound, runDuration, replaySample, replayClock;
        private int wave, score, graze, maxChain, bombsUsed, shotsFired, misses, stage = 1, selectedStage = 1, continues, nextExtend;
        private bool practice, ending, saveRecovered, confirmReset;
        private string panel = "", captureAction = "";
        private ReplayData recording, viewingReplay;
        private string initials = "ALI";
        private string toast = "";
        private float toastUntil;
        private Texture2D pixel, circle;
        private GUIStyle titleStyle, displayStyle, bodyStyle, smallStyle, buttonStyle, primaryButtonStyle, centeredStyle;
        private readonly Color primary = new Color(.965f, .835f, .43f);
        private readonly Color surface = new Color(.09f, .05f, .21f, .95f);
        private readonly Color surfaceRaised = new Color(.17f, .1f, .32f, .98f);
        private readonly Color ink = new Color(1f, .97f, 1f);
        private readonly Color muted = new Color(.8f, .75f, .87f);
        private readonly Color outline = new Color(.57f, .45f, .7f);

        private static readonly string[] BadgeIds = { "rabbit-hole", "grazer", "chain", "full-power", "queen-clear", "no-bomb" };
        private static readonly string[] BadgeIcons = { "⌄", "✧", "⛓", "✦", "♛", "♢" };
        private static readonly string[] BadgeNames = { "落下するアリス", "針の穴を抜けて", "ティーパーティー", "もっと不思議に", "首をはねないで", "素手の奇跡" };
        private static readonly string[] BadgeDescriptions = { "はじめてプレイする", "GRAZE を50回達成", "CHAIN x25達成", "POWERを最大まで上げる", "女王を倒してクリア", "ボムなしでクリア" };

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            LoadSave();
            CreateTextures();
            CreateStars();
            CreateAudio();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && state == GameState.Playing) PauseGame("フォーカスが外れたため一時停止しました");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && state == GameState.Playing) PauseGame("アプリが中断されたため一時停止しました");
        }

        private void PauseGame(string message = null)
        {
            state = GameState.Paused;
            if (message != null) ShowToast(message);
            StopMusic();
        }

        private void ResumeGame()
        {
            state = GameState.Playing;
            StartMusic();
        }

        private void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, .034f);
            attractTime += dt;
            scroll = (scroll + dt * (save.reducedMotion ? 13 : 34)) % 128;
            UpdateStars(dt);
            if (viewingReplay != null && viewingReplay.points.Count > 0)
            {
                replayClock += dt;
                if (replayClock > viewingReplay.points[viewingReplay.points.Count - 1].time) replayClock = 0;
            }
            if (state == GameState.Playing)
            {
                if (Pressed(save.input.pause) || Input.GetKeyDown(KeyCode.Escape)) { PauseGame(); return; }
                if (Pressed(save.input.bomb)) UseBomb();
                UpdateGame(dt);
            }
            else if (state == GameState.Paused && (Pressed(save.input.pause) || Input.GetKeyDown(KeyCode.Escape))) ResumeGame();
            else if (state == GameState.Title && string.IsNullOrEmpty(panel) && Input.GetKeyDown(KeyCode.Return)) BeginGame(false);
            else if (state == GameState.Result && Input.GetKeyDown(KeyCode.Return)) SaveResultAndTitle();
        }

        private void BeginGame(bool isPractice)
        {
            CancelInvoke();
            state = GameState.Playing;
            panel = "";
            practice = isPractice;
            ending = false;
            score = graze = maxChain = bombsUsed = wave = 0;
            shotsFired = misses = 0;
            runDuration = 0;
            replaySample = 0;
            continues = 1;
            stage = ProductRules.ClampStage(selectedStage, save.unlockedStage);
            nextExtend = 100000;
            stageTime = isPractice ? 44 : 0;
            waveTimer = .8f;
            chain = comboTimer = 0;
            introTimer = 2.6f;
            player = new PlayerUnit { pos = new Vector2(240, 566), invincible = 1.3f };
            recording = new ReplayData { label = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"), character = CharacterName(), stage = stage, difficulty = save.difficulty };
            queen = null;
            ClearRunEntities();
            UnlockBadge("rabbit-hole");
            ShowToast(saveRecovered ? "セーブデータを安全に復旧しました" : (isPractice ? "スペル練習：女王がまもなく現れます" : "STAGE " + stage + "　時計ウサギを追いかけよう"));
            saveRecovered = false;
            StartMusic();
        }

        private void ReturnToTitle()
        {
            CancelInvoke();
            state = GameState.Title;
            panel = "";
            ending = false;
            player = null; queen = null;
            viewingReplay = null; replayClock = 0;
            ClearRunEntities();
            StopMusic();
        }

        private void ClearRunEntities()
        {
            foes.Clear(); playerShots.Clear(); enemyShots.Clear(); pickups.Clear(); sparks.Clear();
        }

        private void UpdateGame(float dt)
        {
            stageTime += dt;
            runDuration += dt;
            introTimer = Mathf.Max(0, introTimer - dt);
            comboTimer -= dt;
            if (comboTimer <= 0 && chain > 0) chain = Mathf.Max(0, chain - dt * 4);
            UpdatePlayer(dt);
            UpdateSpawns(dt);
            UpdateFoes(dt);
            UpdateQueen(dt);
            UpdatePlayerShots(dt);
            UpdateEnemyShots(dt);
            UpdatePickups(dt);
            UpdateSparks(dt);
            RecordReplay(dt);
        }

        private void RecordReplay(float dt)
        {
            if (recording == null || player == null) return;
            replaySample -= dt;
            if (replaySample > 0) return;
            replaySample = .08f;
            recording.points.Add(new ReplayPoint { time = runDuration, x = player.pos.x, y = player.pos.y });
        }

        private static bool Held(KeyCode key) { return Input.GetKey(key); }
        private static bool Pressed(KeyCode key) { return Input.GetKeyDown(key); }
        private bool FocusHeld() { return Held(save.input.focus) || Held(KeyCode.Joystick1Button1); }
        private string CharacterName() { return GameCatalog.CharacterName(save.character); }
        private string DifficultyName() { return GameCatalog.DifficultyName(save.difficulty); }
        private float DifficultySpeed() { return ProductRules.DifficultySpeed(save.difficulty); }
        private string StageName() { return GameCatalog.StageName(stage); }
        private string BossName() { return GameCatalog.BossName(stage); }
        private string L(string japanese, string english) { return save != null && save.language == "en" ? english : japanese; }

        private void UpdatePlayer(float dt)
        {
            var move = new Vector2(
                (Held(save.input.right) || Held(KeyCode.Joystick1Button15) ? 1 : 0) - (Held(save.input.left) || Held(KeyCode.Joystick1Button13) ? 1 : 0),
                (Held(save.input.down) || Held(KeyCode.Joystick1Button14) ? 1 : 0) - (Held(save.input.up) || Held(KeyCode.Joystick1Button12) ? 1 : 0));
            if (move.sqrMagnitude > 1) move.Normalize();
            var focused = FocusHeld();
            player.pos += move * (focused ? 115 : 245) * dt;
            player.pos = ClampPlayerPosition(player.pos);
            player.invincible = Mathf.Max(0, player.invincible - dt);
            player.shotTimer -= dt;
            if ((save.autoFire || Held(save.input.shoot) || Held(KeyCode.Joystick1Button0)) && player.shotTimer <= 0)
            {
                ShootPlayer();
                player.shotTimer = focused ? .105f : .082f;
            }
            if (move.sqrMagnitude > 0 && UnityEngine.Random.value < dt * 16) SparkAt(player.pos + new Vector2(UnityEngine.Random.Range(-4f, 4f), 12), new Vector2(UnityEngine.Random.Range(-10f, 10f), 35), UnityEngine.Random.Range(1, 2.5f), new Color(.7f, .93f, 1));
        }

        private static Vector2 ClampPlayerPosition(Vector2 position)
        {
            return new Vector2(Mathf.Clamp(position.x, 18, 462), Mathf.Clamp(position.y, 74, 621));
        }

        private void ShootPlayer()
        {
            int level = Mathf.FloorToInt(player.power);
            PlayerShotProfile profile = GameCatalog.GetShotProfile(save.character, level);
            float damage = (2.25f + level * .35f) * profile.damageMultiplier;
            foreach (float lane in profile.lanes)
            {
                playerShots.Add(new FriendlyShot
                {
                    pos = player.pos + new Vector2(lane, -19),
                    velocity = new Vector2(lane * profile.horizontalVelocityMultiplier, -430),
                    radius = level >= 3 ? 4 : 3.4f,
                    damage = damage,
                    life = 1.8f
                });
            }
            shotsFired += profile.lanes.Length;
            if (Time.unscaledTime - lastShotSound > .07f) { PlayTone(740, .035f, .16f); lastShotSound = Time.unscaledTime; }
        }

        private void UpdateSpawns(float dt)
        {
            if (queen != null || ending) return;
            if (!practice && stageTime < 41)
            {
                waveTimer -= dt;
                if (waveTimer <= 0) { SpawnWave(); waveTimer = Mathf.Max(1.05f, 2.25f - stageTime * .018f); }
            }
            if ((practice && stageTime > 45.1f) || (!practice && stageTime > 45.5f && (foes.Count == 0 || stageTime > 49))) SpawnQueen();
        }

        private void SpawnWave()
        {
            wave++;
            switch (wave % 4)
            {
                case 0: for (var i = 0; i < 4; i++) SpawnFoe("rabbit", 90 + i * 100, -35 - i * 42, 55 + i * 8); break;
                case 1: SpawnFoe("card", 105, -36, 52); SpawnFoe("card", 375, -98, -52); break;
                case 2: for (var i = 0; i < 5; i++) SpawnFoe("rabbit", 80 + i * 80, -25 - i * 29, (i % 2 == 0 ? 1 : -1) * (42 + i * 7)); break;
                default: SpawnFoe("clock", 150, -32, 45); SpawnFoe("clock", 330, -88, -45); break;
            }
        }

        private void SpawnFoe(string kind, float x, float y, float drift)
        {
            var foe = new EnemyUnit { kind = kind, pos = new Vector2(x, y), drift = drift };
            if (kind == "rabbit") { foe.hp = 16; foe.radius = 15; foe.speed = 44; foe.score = 900; foe.timer = UnityEngine.Random.Range(.75f, 1.35f); }
            else if (kind == "card") { foe.hp = 28; foe.radius = 17; foe.speed = 34; foe.score = 1500; foe.timer = UnityEngine.Random.Range(.9f, 1.5f); }
            else { foe.hp = 42; foe.radius = 20; foe.speed = 29; foe.score = 2300; foe.timer = UnityEngine.Random.Range(1.1f, 1.6f); }
            foes.Add(foe);
        }

        private void UpdateFoes(float dt)
        {
            for (int i = foes.Count - 1; i >= 0; i--)
            {
                var foe = foes[i];
                foe.age += dt; foe.flash = Mathf.Max(0, foe.flash - dt);
                foe.pos += new Vector2(Mathf.Sin(foe.age * 1.5f + foe.drift) * foe.drift * .24f * dt, foe.speed * dt);
                foe.timer -= dt;
                if (foe.timer <= 0 && foe.pos.y > 42 && foe.pos.y < 570)
                {
                    FireFoe(foe);
                    foe.timer = foe.kind == "rabbit" ? 1.62f : foe.kind == "card" ? 1.32f : 1.75f;
                }
                if (foe.pos.y > 676) foes.RemoveAt(i);
            }
        }

        private void FireFoe(EnemyUnit foe)
        {
            float speedFactor = DifficultySpeed();
            if (foe.kind == "rabbit") FireAimed(foe.pos, 94 * speedFactor, new Color(1, .55f, .64f), 5, "round");
            else if (foe.kind == "card")
            {
                var a = Mathf.Atan2(player.pos.y - foe.pos.y, player.pos.x - foe.pos.x);
                SpawnEnemyShot(foe.pos, Direction(a - .23f) * 96 * speedFactor, 5.2f, new Color(.87f, .61f, 1), "diamond");
                SpawnEnemyShot(foe.pos, Direction(a + .23f) * 96 * speedFactor, 5.2f, new Color(.87f, .61f, 1), "diamond");
            }
            else for (int i = 0; i < 7 + save.difficulty; i++) SpawnEnemyShot(foe.pos, Direction(foe.age * 1.8f + Mathf.PI * 2 * i / (7 + save.difficulty)) * 73 * speedFactor, 5, primary, "round");
            PlayTone(280, .05f, .11f);
        }

        private void SpawnQueen()
        {
            if (queen != null) return;
            foes.Clear();
            int hp = Mathf.RoundToInt((practice ? 330 : 420) * (1 + stage * .15f + save.difficulty * .18f));
            queen = new Queen { hp = hp, maxHp = hp, timer = .4f, subTimer = .8f };
            ShowToast("警告：ハートの女王が現れた");
            PlayTone(220, .25f, .4f);
        }

        private void UpdateQueen(float dt)
        {
            if (queen == null) return;
            queen.flash = Mathf.Max(0, queen.flash - dt);
            if (queen.entering)
            {
                queen.pos += new Vector2(0, 130 * dt);
                if (queen.pos.y >= 118) { queen.pos.y = 118; queen.entering = false; BeginQueenPhase(); }
                return;
            }
            queen.phaseTime += dt;
            queen.pos.x = 240 + Mathf.Sin(queen.phaseTime * (.56f + queen.phase * .1f)) * (105 + queen.phase * 18);
            queen.timer -= dt; queen.subTimer -= dt;
            if (queen.phaseTime < 1.05f) return;
            if (queen.phase == 0)
            {
                if (queen.timer <= 0) { FireRing(queen.pos + new Vector2(0, 25), 18 + save.difficulty * 2, 84 * DifficultySpeed(), queen.phaseTime * .8f, new Color(1, .42f, .57f), "heart"); queen.timer = .78f; }
                if (queen.subTimer <= 0) { FireAimed(queen.pos + new Vector2(0, 24), 130 * DifficultySpeed(), new Color(1, .82f, .86f), 6, "round"); queen.subTimer = 1.2f; }
            }
            else if (queen.phase == 1)
            {
                if (queen.timer <= 0)
                {
                    float a = queen.phaseTime * 3.1f;
                    SpawnEnemyShot(queen.pos + new Vector2(0, 24), Direction(a) * 104 * DifficultySpeed() + new Vector2(0, 18), 5.6f, new Color(.78f, .61f, 1), "rose");
                    SpawnEnemyShot(queen.pos + new Vector2(0, 24), Direction(a + Mathf.PI) * 104 * DifficultySpeed() + new Vector2(0, 18), 5.6f, new Color(.78f, .61f, 1), "rose");
                    queen.timer = .105f;
                }
                if (queen.subTimer <= 0)
                {
                    float a = Mathf.Atan2(player.pos.y - queen.pos.y, player.pos.x - queen.pos.x);
                    for (int i = -2 - save.difficulty / 2; i <= 2 + save.difficulty / 2; i++) SpawnEnemyShot(queen.pos + new Vector2(0, 24), Direction(a + i * .13f) * 110 * DifficultySpeed(), 5.3f, new Color(1, .88f, .56f), "petal");
                    queen.subTimer = 1.14f;
                }
            }
            else
            {
                if (queen.timer <= 0) { FireRing(queen.pos + new Vector2(0, 25), 25 + save.difficulty * 2, 98 * DifficultySpeed(), queen.phaseTime * 1.25f, new Color(1, .34f, .49f), "heart"); FireRing(queen.pos + new Vector2(0, 25), 13 + save.difficulty, 55 * DifficultySpeed(), -queen.phaseTime * 1.25f, new Color(1, .74f, .42f), "heart"); queen.timer = .82f; }
                if (queen.subTimer <= 0)
                {
                    float a = Mathf.Atan2(player.pos.y - queen.pos.y, player.pos.x - queen.pos.x);
                    for (int i = -3 - save.difficulty / 2; i <= 3 + save.difficulty / 2; i++) SpawnEnemyShot(queen.pos + new Vector2(0, 28), Direction(a + i * .095f) * 142 * DifficultySpeed(), 6, new Color(1, .95f, .74f), "diamond");
                    queen.subTimer = 1.08f;
                }
            }
        }

        private void BeginQueenPhase()
        {
            queen.phaseTime = 0; queen.timer = .58f; queen.subTimer = .92f; queen.flash = .25f;
            ShowToast("SPELL CARD " + (queen.phase + 1) + "　" + GameCatalog.SpellCardName(stage, queen.phase));
            PlayTone(420 + queen.phase * 80, .24f, .38f);
        }

        private void UpdatePlayerShots(float dt)
        {
            for (int i = playerShots.Count - 1; i >= 0; i--)
            {
                var shot = playerShots[i];
                shot.pos += shot.velocity * dt; shot.life -= dt;
                bool used = shot.pos.y < -25 || shot.life <= 0;
                for (int j = foes.Count - 1; j >= 0 && !used; j--)
                {
                    var foe = foes[j];
                    if ((shot.pos - foe.pos).sqrMagnitude < Mathf.Pow(shot.radius + foe.radius, 2))
                    {
                        foe.hp -= shot.damage; foe.flash = .08f; used = true; Burst(shot.pos, new Color(.86f, .97f, 1), 3, 55);
                        if (foe.hp <= 0) DefeatFoe(j);
                    }
                }
                if (queen != null && !used && !queen.entering && (shot.pos - queen.pos).sqrMagnitude < Mathf.Pow(shot.radius + 38, 2))
                {
                    queen.hp -= shot.damage; queen.flash = .07f; used = true; Burst(shot.pos, new Color(1, .95f, .78f), 2, 42);
                    if (queen.hp <= 0) DefeatQueenPhase();
                }
                if (used) playerShots.RemoveAt(i);
            }
        }

        private void DefeatFoe(int index)
        {
            var foe = foes[index];
            AddScore(foe.score);
            Burst(foe.pos, foe.kind == "rabbit" ? new Color(1, .69f, .76f) : new Color(.85f, .7f, 1), 13, 130);
            float roll = UnityEngine.Random.value;
            pickups.Add(new Pickup { pos = foe.pos, kind = roll < .1f ? "bomb" : roll < .54f ? "power" : "star", spin = UnityEngine.Random.Range(0f, Mathf.PI * 2) });
            foes.RemoveAt(index);
            PlayTone(180, .07f, .18f);
        }

        private void DefeatQueenPhase()
        {
            if (queen == null) return;
            Burst(queen.pos, new Color(1, .94f, .69f), 58, 230);
            MarkEnemyShotsCleared();
            AddScore(50000 + queen.phase * 25000);
            queen.phase++;
            if (queen.phase >= GameCatalog.SpellCardCount)
            {
                queen = null;
                ending = true;
                if (!practice && stage < GameCatalog.StageCount) Invoke(nameof(AdvanceStage), .78f);
                else Invoke(nameof(ClearStage), .78f);
                return;
            }
            int hp = queen.phase == 1 ? (practice ? 400 : 500) : (practice ? 500 : 640);
            queen.hp = queen.maxHp = hp;
            BeginQueenPhase();
        }

        private void AdvanceStage()
        {
            stage++;
            save.unlockedStage = Mathf.Max(save.unlockedStage, stage);
            Save();
            ending = false;
            stageTime = 0;
            waveTimer = .8f;
            wave = 0;
            player.bombs = Mathf.Min(5, player.bombs + 1);
            player.invincible = 1.5f;
            ShowToast("STAGE " + stage + "　" + StageName() + "　BOMB +1");
        }

        private void ClearStage() { FinishGame(true); }

        private void MarkEnemyShotsCleared()
        {
            foreach (var shot in enemyShots) shot.cleared = true;
        }

        private void UpdateEnemyShots(float dt)
        {
            for (int i = enemyShots.Count - 1; i >= 0; i--)
            {
                var shot = enemyShots[i];
                if (shot.cleared) { SparkAt(shot.pos, new Vector2(0, -18), 2, new Color(1, .94f, .72f)); enemyShots.RemoveAt(i); continue; }
                shot.pos += shot.velocity * dt; shot.age += dt;
                if (shot.pos.y < -45 || shot.pos.y > 685 || shot.pos.x < -45 || shot.pos.x > 525 || shot.age > 9) { enemyShots.RemoveAt(i); continue; }
                float d = (shot.pos - player.pos).sqrMagnitude;
                if (d < Mathf.Pow(shot.radius + 3.4f, 2)) { enemyShots.RemoveAt(i); HitPlayer(); continue; }
                if (!shot.grazed && d < Mathf.Pow(shot.radius + 19, 2) && player.invincible <= 0)
                {
                    shot.grazed = true; graze++; AddScore(50);
                    if (graze >= 50) UnlockBadge("grazer");
                    SparkAt(shot.pos, Vector2.zero, 3, primary);
                }
            }
        }

        private void UpdatePickups(float dt)
        {
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var item = pickups[i];
                item.pos += new Vector2(0, 48 * dt); item.spin += dt * 4;
                Vector2 delta = player.pos - item.pos; float d = delta.magnitude;
                if ((player.pos.y < 180 || FocusHeld()) && d < 160) item.pos += delta * dt * 5.7f;
                if (d < 19) { Collect(item); pickups.RemoveAt(i); }
                else if (item.pos.y > 664) pickups.RemoveAt(i);
            }
        }

        private void Collect(Pickup item)
        {
            if (item.kind == "power")
            {
                player.fragments = Mathf.Min(16, player.fragments + 2);
                player.power = Mathf.Min(4, 1 + player.fragments / 4f);
                AddScore(600);
                if (player.power >= 4) UnlockBadge("full-power");
            }
            else if (item.kind == "bomb") { player.bombs = Mathf.Min(5, player.bombs + 1); AddScore(1200); ShowToast("BOMB +1"); }
            else AddScore(1100 + Mathf.FloorToInt(player.pos.y / FieldHeight * 900));
            Burst(item.pos, item.kind == "power" ? new Color(.65f, .85f, 1) : primary, 7, 75);
            PlayTone(item.kind == "bomb" ? 320 : 580, .06f, .18f);
        }

        private void HitPlayer()
        {
            if (player.invincible > 0 || state != GameState.Playing) return;
            player.lives--; player.invincible = 2.3f; player.pos = new Vector2(240, 568); player.power = Mathf.Max(1, player.power - .55f); chain = 0; misses++;
            MarkEnemyShotsCleared();
            Burst(player.pos, Color.white, 35, 220); PlayTone(105, .32f, .5f);
            ShowToast(player.lives >= 0 ? "MISS… 残りLIFE " + Mathf.Max(0, player.lives) : "夢がほどけていく…");
            if (player.lives < 0) Invoke(nameof(GameOver), .6f);
        }

        private void GameOver() { FinishGame(false); }

        private void UseBomb()
        {
            if (state != GameState.Playing || player == null || player.bombs <= 0) { if (state == GameState.Playing) ShowToast("ボムがありません"); return; }
            player.bombs--; player.invincible = Mathf.Max(player.invincible, 1.3f); bombsUsed++;
            MarkEnemyShotsCleared();
            foreach (var foe in foes) { foe.hp -= 26; foe.flash = .3f; }
            if (queen != null && !queen.entering) { queen.hp -= 70; queen.flash = .4f; if (queen.hp <= 0) DefeatQueenPhase(); }
            Burst(player.pos, primary, 58, 310); ShowToast("LAST WORD 「白兎の懐中時計」"); PlayTone(110, .38f, .46f);
        }

        private void AddScore(int amount)
        {
            comboTimer = 2.8f; chain = Mathf.Min(99, chain + 1); maxChain = Mathf.Max(maxChain, Mathf.FloorToInt(chain));
            score += ProductRules.ScoreWithChain(amount, Mathf.FloorToInt(chain));
            while (score >= nextExtend)
            {
                player.lives = Mathf.Min(6, player.lives + 1);
                nextExtend = ProductRules.NextExtendThreshold(nextExtend);
                ShowToast("EXTEND!　LIFE +1");
            }
            if (maxChain >= 25) UnlockBadge("chain");
        }

        private void FireAimed(Vector2 origin, float speed, Color color, float radius, string shape) { SpawnEnemyShot(origin, Direction(Mathf.Atan2(player.pos.y - origin.y, player.pos.x - origin.x)) * speed, radius, color, shape); }
        private void FireRing(Vector2 origin, int count, float speed, float offset, Color color, string shape) { for (int i = 0; i < count; i++) SpawnEnemyShot(origin, Direction(offset + Mathf.PI * 2 * i / count) * speed, shape == "heart" ? 6.1f : 5, color, shape); }
        private void SpawnEnemyShot(Vector2 origin, Vector2 velocity, float radius, Color color, string shape)
        {
            if (enemyShots.Count >= 1200) return;
            enemyShots.Add(new EnemyShot { pos = origin, velocity = velocity, radius = radius, color = color, shape = shape });
        }
        private static Vector2 Direction(float angle) { return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); }

        private void Burst(Vector2 position, Color color, int count, float speed)
        {
            if (save.reducedMotion) count = Mathf.CeilToInt(count / 3f);
            count = Mathf.Min(count, Mathf.Max(0, 550 - sparks.Count));
            for (int i = 0; i < count; i++)
            {
                float life = UnityEngine.Random.Range(.25f, .7f);
                sparks.Add(new Spark { pos = position, velocity = Direction(UnityEngine.Random.value * Mathf.PI * 2) * UnityEngine.Random.Range(speed * .25f, speed), size = UnityEngine.Random.Range(1.5f, 4.5f), life = life, maxLife = life, color = color });
            }
        }
        private void SparkAt(Vector2 position, Vector2 velocity, float size, Color color) { sparks.Add(new Spark { pos = position, velocity = velocity, size = size, life = .26f, maxLife = .26f, color = color }); }
        private void UpdateSparks(float dt) { for (int i = sparks.Count - 1; i >= 0; i--) { var p = sparks[i]; p.pos += p.velocity * dt; p.velocity *= .97f; p.life -= dt; if (p.life <= 0) sparks.RemoveAt(i); } }

        private void FinishGame(bool clear)
        {
            if (state != GameState.Playing) return;
            int bonus = clear ? 150000 + player.lives * 40000 + player.bombs * 25000 : 0;
            score += bonus;
            if (clear) { save.clears++; UnlockBadge("queen-clear"); if (bombsUsed == 0) UnlockBadge("no-bomb"); }
            save.bestScore = Mathf.Max(save.bestScore, score); save.bestChain = Mathf.Max(save.bestChain, maxChain); save.runs++; save.totalGraze += graze;
            save.runHistory.Add(new RunRecord { score = score, graze = graze, chain = maxChain, stage = stage, difficulty = save.difficulty, character = CharacterName(), shots = shotsFired, bombs = bombsUsed, misses = misses, clear = clear, dateUtc = DateTime.UtcNow.ToString("o") });
            save.runHistory = save.runHistory.OrderByDescending(entry => entry.score).Take(20).ToList();
            if (recording != null && recording.points.Count > 1)
            {
                recording.score = score; recording.clear = clear;
                save.replays.Insert(0, recording);
                save.replays = save.replays.Take(3).ToList();
            }
            Save();
            state = GameState.Result; ending = false; StopMusic(); panel = clear ? "clear" : "gameover";
            PlayTone(clear ? 660 : 150, clear ? .6f : .4f, .55f);
        }

        private void SaveResultAndTitle()
        {
            string name = ProductRules.SanitizeInitials(initials);
            save.scores.Add(new ScoreEntry { name = name, score = score, clear = panel == "clear" });
            save.scores = save.scores.OrderByDescending(entry => entry.score).Take(8).ToList();
            LeaderboardSync.QueueLocal(name, score, stage, save.difficulty);
            Save(); ReturnToTitle();
        }

        private void ContinueGame()
        {
            if (state != GameState.Result || panel != "gameover" || continues <= 0) return;
            continues--;
            state = GameState.Playing;
            player.lives = 2;
            player.bombs = 2;
            player.power = 1;
            player.invincible = 2.5f;
            score = Mathf.FloorToInt(score * .7f);
            chain = 0;
            enemyShots.Clear();
            panel = "";
            ShowToast("CONTINUE　残り " + continues);
            StartMusic();
        }

        private void CreateTextures()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false); pixel.SetPixel(0, 0, Color.white); pixel.Apply();
            circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)); circle.SetPixel(x, y, new Color(1, 1, 1, d <= 31 ? 1 : 0)); }
            circle.Apply();
        }

        private void CreateStars() { for (int i = 0; i < 78; i++) stars.Add(new Vector4(UnityEngine.Random.Range(0f, 480), UnityEngine.Random.Range(0f, 640), UnityEngine.Random.Range(.4f, 2.2f), UnityEngine.Random.Range(9f, 40f))); }
        private void UpdateStars(float dt) { for (int i = 0; i < stars.Count; i++) { var star = stars[i]; star.y += star.w * dt; if (star.y > 643) { star.y = -3; star.x = UnityEngine.Random.Range(0f, 480); } stars[i] = star; } }

        private void OnGUI()
        {
            InitStyles();
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / 1000f, safe.height / 720f);
            float offsetX = safe.x + (safe.width - 1000 * scale) * .5f;
            float offsetY = safe.y + (safe.height - 720 * scale) * .5f;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            DrawApp();
            GUI.matrix = previous;
        }

        private void DrawApp()
        {
            Fill(new Rect(0, 0, 1000, 720), new Color(.045f, .025f, .11f));
            var cabinet = new Rect(34, 29, 492, 662);
            Fill(cabinet, outline); Fill(new Rect(40, 35, 480, 640), surface);
            var field = new Rect(40, 35, 480, 640);
            DrawBackground(field);
            if (state == GameState.Title) { DrawAttract(field); DrawTitle(field); }
            else
            {
                DrawWorld(field);
                if (state == GameState.Playing) { DrawHud(field); DrawTouch(field); }
                if (state == GameState.Paused) DrawPause(field);
                if (state == GameState.Result) DrawResult(field);
            }
            DrawSidePanel();
            if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil) { Fill(new Rect(84, 78, 392, 34), new Color(.11f, .05f, .23f, .94f)); Label(new Rect(92, 83, 376, 25), toast, centeredStyle); }
            if (GUI.Button(new Rect(947, 22, 30, 30), "⛶", buttonStyle)) Screen.fullScreen = !Screen.fullScreen;
        }

        private void DrawBackground(Rect field)
        {
            for (int n = 0; n < 8; n++) Fill(new Rect(field.x, field.y + n * 80, field.width, 80), Color.Lerp(new Color(.10f, .055f, .23f), new Color(.2f, .12f, .35f), n / 8f));
            foreach (var star in stars) { GUI.color = new Color(.98f, .9f, 1, .45f); GUI.DrawTexture(new Rect(field.x + star.x, field.y + star.y, star.z, star.z), pixel); }
            GUI.color = new Color(.8f, .63f, .88f, .13f);
            for (float y = field.y - 42 + scroll % 42; y < field.yMax; y += 42) for (int x = 0; x < 12; x++) if (((int)((y - field.y) / 42) + x) % 2 == 0) GUI.DrawTexture(new Rect(field.x + x * 42, y, 42, 42), pixel);
            GUI.color = Color.white;
            GUI.color = new Color(.96f, .83f, .43f, .15f);
            for (float y = field.y - 80 + scroll * .5f % 115; y < field.yMax; y += 115) { GUI.DrawTexture(new Rect(field.x, y, 480, 1), pixel); GUI.DrawTexture(new Rect(field.x + 74, y + 23, 264, 1), pixel); }
            GUI.color = Color.white;
        }

        private void DrawAttract(Rect field)
        {
            float x = 240 + Mathf.Sin(attractTime) * 60;
            DrawQueen(field, new Vector2(x, 150), 1.18f, false);
            if (viewingReplay != null) DrawReplayGhost(field);
            GUI.color = new Color(primary.r, primary.g, primary.b, .25f);
            for (int i = 0; i < 10; i++) GUI.DrawTexture(new Rect(field.x + 240, field.y + 320, 2, 130), pixel);
            GUI.color = Color.white;
        }

        private void DrawReplayGhost(Rect field)
        {
            if (viewingReplay.points.Count == 0) return;
            ReplayPoint point = viewingReplay.points[0];
            for (int i = 1; i < viewingReplay.points.Count; i++)
            {
                if (viewingReplay.points[i].time > replayClock) break;
                point = viewingReplay.points[i];
            }
            GUI.color = new Color(.45f, .96f, 1f, .6f);
            GUI.DrawTexture(new Rect(field.x + point.x - 10, field.y + point.y - 12, 20, 27), circle);
            GUI.color = Color.white;
            Label(new Rect(field.x + 115, field.y + 548, 250, 19), "GHOST REPLAY  " + viewingReplay.label, centeredStyle);
        }

        private void DrawWorld(Rect field)
        {
            foreach (var spark in sparks) { GUI.color = new Color(spark.color.r, spark.color.g, spark.color.b, spark.life / spark.maxLife); GUI.DrawTexture(new Rect(field.x + spark.pos.x - spark.size, field.y + spark.pos.y - spark.size, spark.size * 2, spark.size * 2), circle); }
            GUI.color = Color.white;
            foreach (var item in pickups) DrawPickup(field, item);
            foreach (var foe in foes) DrawFoe(field, foe);
            if (queen != null) DrawQueen(field, queen.pos, 1, queen.flash > 0);
            foreach (var shot in enemyShots) DrawEnemyShot(field, shot);
            foreach (var shot in playerShots) { GUI.color = new Color(.78f, .96f, 1); GUI.DrawTexture(new Rect(field.x + shot.pos.x - shot.radius, field.y + shot.pos.y - 8, shot.radius * 2, 15), pixel); }
            GUI.color = Color.white;
            if (player != null) DrawPlayer(field);
        }

        private void DrawFoe(Rect field, EnemyUnit foe)
        {
            Vector2 p = new Vector2(field.x + foe.pos.x, field.y + foe.pos.y);
            if (foe.kind == "rabbit")
            {
                GUI.color = foe.flash > 0 ? Color.white : new Color(1, .65f, .74f); GUI.DrawTexture(new Rect(p.x - 12, p.y - 10, 24, 24), circle);
                GUI.color = new Color(1, .85f, .88f); GUI.DrawTexture(new Rect(p.x - 9, p.y - 30, 7, 22), circle); GUI.DrawTexture(new Rect(p.x + 2, p.y - 30, 7, 22), circle);
            }
            else if (foe.kind == "card") { GUI.color = foe.flash > 0 ? Color.white : new Color(.89f, .72f, 1); GUI.DrawTexture(new Rect(p.x - 12, p.y - 17, 24, 34), pixel); Label(new Rect(p.x - 10, p.y - 8, 20, 20), "♠", centeredStyle); }
            else { GUI.color = foe.flash > 0 ? Color.white : primary; GUI.DrawTexture(new Rect(p.x - 17, p.y - 17, 34, 34), circle); GUI.color = new Color(.25f, .1f, .3f); GUI.DrawTexture(new Rect(p.x - 10, p.y - 10, 20, 20), circle); }
            GUI.color = Color.white;
        }

        private void DrawQueen(Rect field, Vector2 pos, float size, bool flash)
        {
            Vector2 p = new Vector2(field.x + pos.x, field.y + pos.y);
            Color costume = stage == 1 ? new Color(.46f, .38f, .74f) : stage == 2 ? new Color(.16f, .55f, .52f) : new Color(.93f, .28f, .46f);
            GUI.color = flash ? Color.white : Color.Lerp(costume, new Color(.13f, .05f, .25f), .55f); GUI.DrawTexture(new Rect(p.x - 42 * size, p.y - 2 * size, 84 * size, 72 * size), pixel);
            GUI.color = flash ? Color.white : costume; GUI.DrawTexture(new Rect(p.x - 30 * size, p.y + 17 * size, 60 * size, 44 * size), circle);
            GUI.color = new Color(1, .84f, .79f); GUI.DrawTexture(new Rect(p.x - 22 * size, p.y - 34 * size, 44 * size, 44 * size), circle);
            GUI.color = primary; GUI.DrawTexture(new Rect(p.x - 18 * size, p.y - 49 * size, 36 * size, 16 * size), pixel);
            GUI.color = Color.white;
            Label(new Rect(p.x - 18, p.y - 30, 36, 22), "♛", centeredStyle);
        }

        private void DrawEnemyShot(Rect field, EnemyShot shot)
        {
            float s = shot.radius * 2;
            GUI.color = shot.color;
            GUI.DrawTexture(new Rect(field.x + shot.pos.x - shot.radius, field.y + shot.pos.y - shot.radius, s, s), shot.shape == "diamond" || shot.shape == "rose" ? pixel : circle);
            GUI.color = Color.white;
        }

        private void DrawPickup(Rect field, Pickup item)
        {
            Color color = item.kind == "power" ? new Color(.65f, .85f, 1) : item.kind == "bomb" ? new Color(.85f, .55f, 1) : primary;
            GUI.color = color; float s = item.kind == "star" ? 14 : 12;
            GUI.DrawTexture(new Rect(field.x + item.pos.x - s / 2, field.y + item.pos.y - s / 2, s, s), item.kind == "bomb" ? circle : pixel);
            GUI.color = Color.white;
        }

        private void DrawPlayer(Rect field)
        {
            if (player.invincible > 0 && Mathf.FloorToInt(player.invincible * 12) % 2 == 0) GUI.color = new Color(1, 1, 1, .45f);
            var p = new Vector2(field.x + player.pos.x, field.y + player.pos.y);
            GUI.DrawTexture(new Rect(p.x - 13, p.y - 13, 26, 36), pixel);
            GUI.color = new Color(.68f, .88f, 1); GUI.DrawTexture(new Rect(p.x - 13, p.y - 3, 26, 26), circle);
            GUI.color = new Color(1, .85f, .74f); GUI.DrawTexture(new Rect(p.x - 8, p.y - 13, 16, 16), circle);
            GUI.color = primary; GUI.DrawTexture(new Rect(p.x - 11, p.y - 20, 22, 5), pixel);
            if (FocusHeld()) { GUI.color = primary; GUI.DrawTexture(new Rect(p.x - 4, p.y + 9, 8, 8), circle); }
            GUI.color = Color.white;
        }

        private void DrawHud(Rect field)
        {
            Fill(new Rect(field.x + 8, field.y + 8, 464, 56), new Color(.055f, .025f, .14f, .88f));
            Label(new Rect(field.x + 16, field.y + 12, 252, 45), "SCORE\n" + ScoreText(score) + "   <color=#f6d56e>HI " + ScoreText(save.bestScore) + "</color>", smallStyle);
            Label(new Rect(field.x + 315, field.y + 14, 150, 45), "LIFE  " + new string('●', Mathf.Max(0, player.lives)) + "\nBOMB  " + new string('◆', player.bombs), smallStyle);
            Label(new Rect(field.x + 16, field.y + 66, 160, 23), "GRAZE " + graze.ToString("0000") + "   CHAIN x" + Mathf.FloorToInt(chain), smallStyle);
            Label(new Rect(field.x + 350, field.y + 66, 110, 23), "POWER " + Mathf.FloorToInt(player.power) + "/4", smallStyle);
            if (queen != null && !queen.entering)
            {
                Fill(new Rect(field.x + 68, field.y + 612, 344, 20), new Color(.06f, .025f, .14f, .9f));
                Fill(new Rect(field.x + 74, field.y + 620, 332 * Mathf.Clamp01(queen.hp / queen.maxHp), 7), queen.phase == 2 ? new Color(1, .34f, .49f) : primary);
                Label(new Rect(field.x + 75, field.y + 610, 330, 12), BossName() + "  //  SPELL " + (queen.phase + 1), centeredStyle);
            }
            if (introTimer > 0 && queen == null) { Fill(new Rect(field.x + 92, field.y + 114, 296, 47), new Color(.12f, .06f, .25f, .92f)); Label(new Rect(field.x + 100, field.y + 120, 280, 35), practice ? "SPELL PRACTICE\n女王の弾幕を練習しよう" : "STAGE 1  -  RABBIT HOLE\n時計を追って、落ちていこう", centeredStyle); }
        }

        private void DrawTouch(Rect field)
        {
            if (GUI.RepeatButton(new Rect(field.x + 17, field.y + 548, 34, 31), "▲", buttonStyle)) MoveTouch(Vector2.up);
            if (GUI.RepeatButton(new Rect(field.x + 0, field.y + 580, 34, 31), "◀", buttonStyle)) MoveTouch(Vector2.left);
            if (GUI.RepeatButton(new Rect(field.x + 36, field.y + 580, 34, 31), "▼", buttonStyle)) MoveTouch(Vector2.down);
            if (GUI.RepeatButton(new Rect(field.x + 72, field.y + 580, 34, 31), "▶", buttonStyle)) MoveTouch(Vector2.right);
            if (GUI.RepeatButton(new Rect(field.x + 397, field.y + 565, 72, 47), "SHOT", primaryButtonStyle)) ShootPlayer();
            if (GUI.Button(new Rect(field.x + 324, field.y + 565, 67, 47), "BOMB", buttonStyle)) UseBomb();
        }
        private void MoveTouch(Vector2 direction) { if (player != null) player.pos = ClampPlayerPosition(player.pos + direction * 5); }

        private void DrawTitle(Rect field)
        {
            Fill(new Rect(field.x + 39, field.y + 75, 402, 500), new Color(.10f, .05f, .22f, .94f));
            Label(new Rect(field.x + 61, field.y + 90, 358, 20), "WONDERLAND BULLET FANTASIA", centeredStyle);
            Label(new Rect(field.x + 58, field.y + 119, 364, 110), "ALICE\n<color=#f6d56e>MIRRORFALL</color>", titleStyle);
            Label(new Rect(field.x + 75, field.y + 235, 330, 45), L("落ちる先は、夢か現か。\n時計を追って、弾幕をほどこう。", "Fall through the rabbit hole.\nThread the gaps in every barrage."), centeredStyle);
            if (Button(new Rect(field.x + 81, field.y + 294, 318, 42), L("はじめる   [ Enter ]", "START   [ Enter ]"), true)) panel = "start";
            if (Button(new Rect(field.x + 81, field.y + 344, 318, 34), L("スペル練習", "SPELL PRACTICE"), false)) BeginGame(true);
            if (Button(new Rect(field.x + 81, field.y + 386, 153, 33), L("あそびかた", "HOW TO PLAY"), false)) panel = "howto";
            if (Button(new Rect(field.x + 246, field.y + 386, 153, 33), L("設定", "SETTINGS"), false)) panel = "settings";
            if (Button(new Rect(field.x + 81, field.y + 427, 318, 33), L("記録と実績", "RECORDS"), false)) panel = "archive";
            Label(new Rect(field.x + 70, field.y + 500, 340, 25), "BEST " + ScoreText(save.bestScore) + "    CLEARS " + save.clears, centeredStyle);
            if (panel == "howto") DrawHowTo(field);
            else if (panel == "settings") DrawSettings(field);
            else if (panel == "bindings") DrawBindings(field);
            else if (panel == "archive") DrawArchive(field);
            else if (panel == "start") DrawStartOptions(field);
            else if (panel == "replay") DrawReplayLibrary(field);
        }

        private void DrawStartOptions(Rect field)
        {
            Fill(new Rect(field.x + 54, field.y + 119, 372, 398), surfaceRaised);
            Label(new Rect(field.x + 70, field.y + 137, 340, 55), L("FLIGHT PREPARATION\n<size=29>出撃準備</size>", "FLIGHT PREPARATION\n<size=29>READY TO FLY</size>"), displayStyle);
            Label(new Rect(field.x + 85, field.y + 207, 120, 24), L("難易度", "DIFFICULTY"), bodyStyle);
            if (Button(new Rect(field.x + 205, field.y + 202, 150, 30), DifficultyName(), false)) save.difficulty = ProductRules.CycleOption(save.difficulty, GameCatalog.DifficultyCount);
            Label(new Rect(field.x + 85, field.y + 248, 120, 24), L("主人公", "PILOT"), bodyStyle);
            if (Button(new Rect(field.x + 205, field.y + 243, 150, 30), CharacterName(), false)) save.character = ProductRules.CycleOption(save.character, GameCatalog.CharacterCount);
            Label(new Rect(field.x + 85, field.y + 289, 120, 24), L("開始ステージ", "STAGE"), bodyStyle);
            if (Button(new Rect(field.x + 205, field.y + 284, 150, 30), "STAGE " + selectedStage + "  " + SelectedStageName(), false)) selectedStage = ProductRules.CycleStage(selectedStage, save.unlockedStage);
            Label(new Rect(field.x + 85, field.y + 328, 270, 33), CharacterHint(), centeredStyle);
            if (Button(new Rect(field.x + 85, field.y + 381, 270, 35), L("ストーリーモード開始", "START STORY MODE"), true)) { Save(); BeginGame(false); }
            if (Button(new Rect(field.x + 85, field.y + 425, 270, 31), L("このステージを練習", "PRACTICE THIS STAGE"), false)) { Save(); BeginGame(true); }
            if (Button(new Rect(field.x + 85, field.y + 466, 270, 31), L("戻る", "BACK"), false)) panel = "";
        }

        private string SelectedStageName() { return GameCatalog.StageName(selectedStage); }
        private string CharacterHint() { return GameCatalog.CharacterHint(save.character); }

        private void DrawReplayLibrary(Rect field)
        {
            Fill(new Rect(field.x + 48, field.y + 116, 384, 410), surfaceRaised);
            Label(new Rect(field.x + 68, field.y + 133, 344, 54), "REPLAY LIBRARY\n<size=29>ゴーストリプレイ</size>", displayStyle);
            if (save.replays.Count == 0) Label(new Rect(field.x + 80, field.y + 220, 320, 65), "保存されたリプレイはありません。\nプレイ終了後に自動保存されます。", centeredStyle);
            for (int i = 0; i < save.replays.Count; i++)
            {
                ReplayData replay = save.replays[i];
                if (Button(new Rect(field.x + 77, field.y + 205 + i * 51, 326, 41), replay.label + "  " + ScoreText(replay.score) + (replay.clear ? " ♛" : ""), false)) { viewingReplay = replay; replayClock = 0; panel = ""; }
            }
            if (Button(new Rect(field.x + 105, field.y + 472, 270, 32), "戻る", true)) panel = "";
        }

        private void DrawPause(Rect field)
        {
            Fill(new Rect(field.x + 73, field.y + 185, 334, 272), new Color(.1f, .05f, .22f, .97f));
            Label(new Rect(field.x + 95, field.y + 205, 290, 78), "TEA BREAK\n<size=36>PAUSED</size>", displayStyle);
            Label(new Rect(field.x + 95, field.y + 283, 290, 27), "夢の時間は止まっている。", centeredStyle);
            if (Button(new Rect(field.x + 110, field.y + 327, 260, 35), "つづける   [ P ]", true)) ResumeGame();
            if (Button(new Rect(field.x + 110, field.y + 371, 260, 32), "最初からやりなおす", false)) BeginGame(practice);
            if (Button(new Rect(field.x + 110, field.y + 411, 260, 32), "タイトルへ", false)) ReturnToTitle();
        }

        private void DrawResult(Rect field)
        {
            bool clear = panel == "clear";
            Fill(new Rect(field.x + 61, field.y + 126, 358, 409), new Color(.14f, .07f, .29f, .98f));
            Label(new Rect(field.x + 78, field.y + 143, 324, 58), clear ? "WONDERFUL!" : "TIME IS UP", displayStyle);
            Label(new Rect(field.x + 78, field.y + 204, 324, 37), clear ? "女王を退けた。ティーパーティーの始まりだ。" : "時計の針に追いつかれてしまった。", centeredStyle);
            Label(new Rect(field.x + 103, field.y + 258, 274, 90), "SCORE  " + ScoreText(score) + "\nGRAZE  " + graze.ToString("0000") + "\nMAX CHAIN  x" + maxChain + "\nBEST  " + ScoreText(save.bestScore), centeredStyle);
            Label(new Rect(field.x + 105, field.y + 361, 270, 19), "ランキングの名前（3文字まで）", centeredStyle);
            initials = GUI.TextField(new Rect(field.x + 170, field.y + 384, 140, 30), initials, 3, centeredStyle);
            if (!clear && continues > 0 && Button(new Rect(field.x + 96, field.y + 430, 288, 34), "CONTINUE　残り " + continues, false)) ContinueGame();
            if (Button(new Rect(field.x + 96, field.y + (clear ? 439 : 473), 288, 39), "記録してタイトルへ  [ Enter ]", true)) SaveResultAndTitle();
        }

        private void DrawHowTo(Rect field)
        {
            Fill(new Rect(field.x + 43, field.y + 84, 394, 468), surfaceRaised);
            Label(new Rect(field.x + 58, field.y + 98, 364, 57), "FIELD NOTES\n<size=29>あそびかた</size>", displayStyle);
            Label(new Rect(field.x + 78, field.y + 164, 324, 275), "<b>矢印 / WASD</b>　移動\n<b>Z / Space</b>　ショットを撃つ\n<b>Shift</b>　低速移動（当たり判定を表示）\n<b>X</b>　ボム：敵弾を消して大ダメージ\n<b>P / Escape</b>　ポーズ\n\nP=火力、★=得点、B=ボム。\n赤い弾に近づくと GRAZE が増え、\nCHAIN をつなぐほどスコアが伸びます。", bodyStyle);
            if (Button(new Rect(field.x + 115, field.y + 488, 250, 35), "閉じる", false)) panel = "";
        }

        private void DrawSettings(Rect field)
        {
            Fill(new Rect(field.x + 47, field.y + 82, 386, 520), surfaceRaised);
            Label(new Rect(field.x + 68, field.y + 95, 344, 52), "CABINET SETTINGS\n<size=29>設定</size>", displayStyle);
            int oldMaster = save.master, oldMusic = save.music, oldSfx = save.sfx;
            Label(new Rect(field.x + 74, field.y + 165, 125, 21), "マスター  " + save.master + "%", bodyStyle);
            save.master = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(field.x + 205, field.y + 172, 157, 18), save.master, 0, 100) / 5f) * 5;
            Label(new Rect(field.x + 74, field.y + 201, 125, 21), "音楽  " + save.music + "%", bodyStyle);
            save.music = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(field.x + 205, field.y + 208, 157, 18), save.music, 0, 100) / 5f) * 5;
            Label(new Rect(field.x + 74, field.y + 237, 125, 21), "効果音  " + save.sfx + "%", bodyStyle);
            save.sfx = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(field.x + 205, field.y + 244, 157, 18), save.sfx, 0, 100) / 5f) * 5;
            if (oldMaster != save.master || oldMusic != save.music || oldSfx != save.sfx) RefreshVolumes();
            Label(new Rect(field.x + 74, field.y + 272, 125, 21), "文字サイズ  " + save.textScale + "%", bodyStyle);
            int previousScale = save.textScale;
            save.textScale = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(field.x + 205, field.y + 279, 157, 18), save.textScale, 80, 140) / 10f) * 10;
            if (previousScale != save.textScale) titleStyle = null;
            save.reducedMotion = GUI.Toggle(new Rect(field.x + 76, field.y + 310, 280, 23), save.reducedMotion, " 演出をひかえめにする", bodyStyle);
            save.autoFire = GUI.Toggle(new Rect(field.x + 76, field.y + 338, 280, 23), save.autoFire, " オートショット", bodyStyle);
            bool previousContrast = save.highContrast;
            save.highContrast = GUI.Toggle(new Rect(field.x + 76, field.y + 366, 280, 23), save.highContrast, " 高コントラスト表示", bodyStyle);
            if (previousContrast != save.highContrast) titleStyle = null;
            if (Button(new Rect(field.x + 76, field.y + 398, 136, 31), "操作キー設定", false)) panel = "bindings";
            if (Button(new Rect(field.x + 220, field.y + 398, 136, 31), save.language == "ja" ? "Language: 日本語" : "Language: English", false)) { save.language = save.language == "ja" ? "en" : "ja"; }
            if (!confirmReset)
            {
                if (Button(new Rect(field.x + 76, field.y + 439, 280, 31), "ローカル記録をリセット", false)) confirmReset = true;
            }
            else
            {
                Label(new Rect(field.x + 77, field.y + 438, 278, 21), "すべての記録を削除しますか？", centeredStyle);
                if (Button(new Rect(field.x + 77, field.y + 462, 133, 31), "削除する", false)) { save = new SaveData(); Save(); confirmReset = false; ShowToast("ローカル記録をリセットしました"); }
                if (Button(new Rect(field.x + 220, field.y + 462, 136, 31), "キャンセル", true)) confirmReset = false;
            }
            if (Button(new Rect(field.x + 76, field.y + 551, 280, 34), "閉じる", true)) { Save(); RefreshVolumes(); panel = ""; }
        }

        private void DrawBindings(Rect field)
        {
            Fill(new Rect(field.x + 52, field.y + 100, 376, 470), surfaceRaised);
            Label(new Rect(field.x + 68, field.y + 115, 344, 54), "INPUT PROFILE\n<size=29>操作キー設定</size>", displayStyle);
            if (!string.IsNullOrEmpty(captureAction))
            {
                Label(new Rect(field.x + 78, field.y + 175, 324, 35), "割り当てたいキーを押してください\nEscapeでキャンセル", centeredStyle);
                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Escape) captureAction = "";
                    else if (Event.current.keyCode != KeyCode.None) { SetBinding(captureAction, Event.current.keyCode); captureAction = ""; Save(); }
                    Event.current.Use();
                }
            }
            BindingButton(field, 220, "上", "up", save.input.up);
            BindingButton(field, 258, "下", "down", save.input.down);
            BindingButton(field, 296, "左", "left", save.input.left);
            BindingButton(field, 334, "右", "right", save.input.right);
            BindingButton(field, 372, "ショット", "shoot", save.input.shoot);
            BindingButton(field, 410, "低速", "focus", save.input.focus);
            BindingButton(field, 448, "ボム", "bomb", save.input.bomb);
            BindingButton(field, 486, "ポーズ", "pause", save.input.pause);
            if (Button(new Rect(field.x + 91, field.y + 523, 130, 32), "初期設定に戻す", false)) { save.input = new InputProfile(); Save(); }
            if (Button(new Rect(field.x + 231, field.y + 523, 130, 32), "戻る", true)) { captureAction = ""; panel = "settings"; }
        }

        private void BindingButton(Rect field, float y, string label, string action, KeyCode key)
        {
            Label(new Rect(field.x + 89, field.y + y, 120, 25), label, bodyStyle);
            if (Button(new Rect(field.x + 213, field.y + y - 3, 152, 29), captureAction == action ? "入力待ち…" : key.ToString(), false)) captureAction = action;
        }

        private void SetBinding(string action, KeyCode key)
        {
            save.input.TrySetBinding(action, key);
        }

        private void DrawArchive(Rect field)
        {
            Fill(new Rect(field.x + 40, field.y + 72, 400, 524), surfaceRaised);
            Label(new Rect(field.x + 60, field.y + 87, 360, 58), "WONDERLAND ARCHIVE\n<size=29>記録と実績</size>", displayStyle);
            string ranking = save.scores.Count == 0 ? "01   ---   000000000" : string.Join("\n", save.scores.Select((e, i) => (i + 1).ToString("00") + "   " + e.name + "   " + ScoreText(e.score) + (e.clear ? " ♛" : "")));
            string badges = "";
            for (int i = 0; i < BadgeIds.Length; i++)
            {
                bool unlocked = save.badges.Contains(BadgeIds[i]);
                badges += (unlocked ? BadgeIcons[i] : "○") + "  <b>" + BadgeNames[i] + "</b>  " + (unlocked ? "UNLOCKED" : BadgeDescriptions[i]) + "\n";
            }
            string run = save.runHistory.Count == 0 ? "NO RUN DATA" : "BEST RUN  " + CharacterForRecord(save.runHistory[0].character) + "  ST" + save.runHistory[0].stage + "  " + DifficultyForRecord(save.runHistory[0].difficulty) + "\nSHOTS " + save.runHistory[0].shots + "  BOMBS " + save.runHistory[0].bombs + "  MISSES " + save.runHistory[0].misses;
            Label(new Rect(field.x + 65, field.y + 151, 350, 385), "<color=#f6d56e><b>BEST SCORE</b></color>  " + ScoreText(save.bestScore) + "\n<color=#f6d56e><b>TOTAL RUNS</b></color>  " + save.runs + "    <color=#f6d56e><b>TOTAL GRAZE</b></color>  " + save.totalGraze + "\n" + run + "\n\n<color=#f6d56e><b>RANKING</b></color>\n" + ranking + "\n\n<color=#f6d56e><b>BADGES</b></color>\n" + badges, bodyStyle);
            if (Button(new Rect(field.x + 115, field.y + 512, 250, 30), "ゴーストリプレイ", false)) panel = "replay";
            if (Button(new Rect(field.x + 115, field.y + 550, 250, 30), "閉じる", true)) panel = "";
        }

        private static string CharacterForRecord(string value) { return string.IsNullOrEmpty(value) ? "ALICE" : value; }
        private static string DifficultyForRecord(int value) { return GameCatalog.DifficultyName(value); }

        private void DrawSidePanel()
        {
            Fill(new Rect(553, 72, 412, 583), new Color(.1f, .05f, .21f, .92f));
            Label(new Rect(580, 98, 358, 64), "WONDERLAND\n<size=31>FLIGHT LOG</size>", displayStyle);
            Label(new Rect(591, 184, 336, 221), "STAGE 01\nRABBIT HOLE\n\n弾幕を『消す』のではなく、\n細い隙間を抜けていく。\n\n<b>GRAZE</b> で得点を集める\n<b>BOMB</b> は絶体絶命の切り札\n<b>SHIFT</b> で精密に避ける", bodyStyle);
            Label(new Rect(591, 449, 336, 106), "<color=#f6d56e><b>CONTROLS</b></color>\nARROWS / WASD　MOVE\nZ / SPACE　SHOT\nX　BOMB\nP / ESC　PAUSE", bodyStyle);
            Label(new Rect(591, 598, 336, 20), "<color=#8de3bc>●  LOCAL SAVE ACTIVE</color>", centeredStyle);
        }

        private bool Button(Rect rect, string label, bool primaryButton)
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = primaryButton ? primary : surfaceRaised;
            bool clicked = GUI.Button(rect, label, primaryButton ? primaryButtonStyle : buttonStyle);
            GUI.backgroundColor = previous;
            return clicked;
        }
        private void Label(Rect rect, string content, GUIStyle style) { GUI.Label(rect, content, style); }
        private void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = Color.white; }

        private void InitStyles()
        {
            if (titleStyle != null) return;
            float scale = save == null ? 1 : save.textScale / 100f;
            Color copy = save != null && save.highContrast ? Color.white : muted;
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(43 * scale), fontStyle = FontStyle.Bold, richText = true, wordWrap = true, normal = { textColor = ink } };
            displayStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(14 * scale), fontStyle = FontStyle.Bold, richText = true, wordWrap = true, normal = { textColor = primary } };
            bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(14 * scale), richText = true, wordWrap = true, normal = { textColor = copy } };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = Mathf.RoundToInt(12 * scale), normal = { textColor = ink } };
            centeredStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = copy } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(13 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = ink, background = pixel }, hover = { textColor = primary, background = pixel }, active = { textColor = primary, background = pixel } };
            primaryButtonStyle = new GUIStyle(buttonStyle) { normal = { textColor = new Color(.15f, .08f, .2f), background = pixel }, hover = { textColor = new Color(.15f, .08f, .2f), background = pixel }, active = { textColor = new Color(.15f, .08f, .2f), background = pixel } };
        }
    }
}
