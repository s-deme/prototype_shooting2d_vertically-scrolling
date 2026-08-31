using System;
using System.Collections.Generic;
using UnityEngine;

namespace AliceMirrorfall
{
    public enum GameState { Title, Playing, Paused, Result }

    [Serializable]
    internal sealed class ScoreEntry
    {
        public string name;
        public int score;
        public bool clear;
    }

    [Serializable] internal sealed class InputProfile
    {
        public KeyCode up = KeyCode.UpArrow, down = KeyCode.DownArrow, left = KeyCode.LeftArrow, right = KeyCode.RightArrow;
        public KeyCode shoot = KeyCode.Z, focus = KeyCode.LeftShift, bomb = KeyCode.X, pause = KeyCode.P;

        public bool TrySetBinding(string action, KeyCode key)
        {
            switch (action)
            {
                case "up": up = key; return true;
                case "down": down = key; return true;
                case "left": left = key; return true;
                case "right": right = key; return true;
                case "shoot": shoot = key; return true;
                case "focus": focus = key; return true;
                case "bomb": bomb = key; return true;
                case "pause": pause = key; return true;
                default: return false;
            }
        }
    }

    [Serializable] internal sealed class RunRecord
    {
        public int score, graze, chain, stage, difficulty, shots, bombs, misses;
        public string character, dateUtc;
        public bool clear;
    }

    [Serializable]
    internal sealed class ReplayPoint
    {
        public float time;
        public float x;
        public float y;
    }

    [Serializable] internal sealed class ReplayData
    {
        public string label, character;
        public int score, stage, difficulty;
        public bool clear;
        public List<ReplayPoint> points = new List<ReplayPoint>();
    }

    [Serializable] internal sealed class SaveData
    {
        public const int CurrentVersion = 3;

        public int version = 3, bestScore, clears, runs, totalGraze, bestChain, unlockedStage = 1;
        public int master = 100, music = 35, sfx = 55, textScale = 100, difficulty, character;
        public bool reducedMotion, autoFire, highContrast;
        public string language = "ja";
        public InputProfile input = new InputProfile();
        public List<ScoreEntry> scores = new List<ScoreEntry>();
        public List<RunRecord> runHistory = new List<RunRecord>();
        public List<ReplayData> replays = new List<ReplayData>();
        public List<string> badges = new List<string>();

        public void EnsureCollections()
        {
            if (scores == null) scores = new List<ScoreEntry>();
            if (runHistory == null) runHistory = new List<RunRecord>();
            if (replays == null) replays = new List<ReplayData>();
            if (badges == null) badges = new List<string>();
            if (input == null) input = new InputProfile();
        }

        public bool MigrateLegacyData()
        {
            if (version >= CurrentVersion) return false;

            version = CurrentVersion;
            master = Mathf.Clamp(master == 0 ? 100 : master, 0, 100);
            textScale = Mathf.Clamp(textScale == 0 ? 100 : textScale, 80, 140);
            unlockedStage = Mathf.Clamp(unlockedStage == 0 ? 1 : unlockedStage, 1, GameCatalog.StageCount);
            if (string.IsNullOrEmpty(language)) language = "ja";
            return true;
        }
    }

    internal sealed class PlayerUnit
    {
        public Vector2 pos;
        public float shotTimer;
        public float invincible;
        public float power = 1;
        public int fragments;
        public int lives = 3;
        public int bombs = 3;
    }

    internal sealed class EnemyUnit
    {
        public string kind;
        public Vector2 pos;
        public float drift;
        public float age;
        public float hp;
        public float radius;
        public float speed;
        public float timer;
        public float flash;
        public int score;
    }

    internal sealed class FriendlyShot
    {
        public Vector2 pos;
        public Vector2 velocity;
        public float radius;
        public float damage;
        public float life;
    }

    internal sealed class EnemyShot
    {
        public Vector2 pos;
        public Vector2 velocity;
        public float radius;
        public float age;
        public Color color;
        public string shape;
        public bool grazed;
        public bool cleared;
    }

    internal sealed class Pickup
    {
        public Vector2 pos;
        public string kind;
        public float spin;
    }

    internal sealed class Spark
    {
        public Vector2 pos;
        public Vector2 velocity;
        public float size;
        public float life;
        public float maxLife;
        public Color color;
    }

    internal sealed class Queen
    {
        public Vector2 pos = new Vector2(240, -80);
        public int phase;
        public float phaseTime = -1;
        public float hp;
        public float maxHp;
        public float timer;
        public float subTimer;
        public float flash;
        public bool entering = true;
    }
}
