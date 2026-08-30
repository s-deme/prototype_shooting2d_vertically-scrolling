using System;
using System.Collections.Generic;
using UnityEngine;

namespace AliceMirrorfall
{
    [Serializable]
    public sealed class PendingScoreSubmission
    {
        public string playerName;
        public int score;
        public int stage;
        public int difficulty;
        public string createdUtc;
    }

    /// <summary>
    /// Keeps score submissions safe while offline. A hosted provider can replace the adapter
    /// without changing game code once an authenticated endpoint has been selected.
    /// </summary>
    public static class LeaderboardSync
    {
        private const string QueueKey = "alice-mirrorfall-score-sync-queue-v1";

        [Serializable]
        private sealed class Queue { public List<PendingScoreSubmission> entries = new List<PendingScoreSubmission>(); }

        public static void QueueLocal(string name, int score, int stage, int difficulty)
        {
            Queue queue = Read();
            queue.entries.Add(new PendingScoreSubmission { playerName = name, score = score, stage = stage, difficulty = difficulty, createdUtc = DateTime.UtcNow.ToString("o") });
            if (queue.entries.Count > 20) queue.entries.RemoveRange(0, queue.entries.Count - 20);
            PlayerPrefs.SetString(QueueKey, JsonUtility.ToJson(queue));
            PlayerPrefs.Save();
        }

        public static IReadOnlyList<PendingScoreSubmission> Pending() => Read().entries;

        private static Queue Read()
        {
            try
            {
                Queue queue = JsonUtility.FromJson<Queue>(PlayerPrefs.GetString(QueueKey, ""));
                return queue ?? new Queue();
            }
            catch { return new Queue(); }
        }
    }
}
