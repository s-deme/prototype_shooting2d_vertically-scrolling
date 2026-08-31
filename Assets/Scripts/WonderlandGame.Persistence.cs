using System;
using UnityEngine;

namespace AliceMirrorfall
{
    public sealed partial class WonderlandGame
    {
        private void LoadSave()
        {
            save = LoadCandidate(PlayerPrefs.GetString(SaveKey, ""));
            if (save == null)
            {
                save = LoadCandidate(PlayerPrefs.GetString(SaveKey + ".backup", ""));
                saveRecovered = save != null;
            }

            if (save == null)
            {
                save = new SaveData();
                saveRecovered = PlayerPrefs.HasKey(SaveKey);
            }

            save.EnsureCollections();
            if (save.MigrateLegacyData()) Save();
        }

        private static SaveData LoadCandidate(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var candidate = JsonUtility.FromJson<SaveData>(json);
                return candidate != null && candidate.version <= SaveData.CurrentVersion ? candidate : null;
            }
            catch
            {
                return null;
            }
        }

        private void Save()
        {
            string old = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(old)) PlayerPrefs.SetString(SaveKey + ".backup", old);
            save.version = SaveData.CurrentVersion;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        private void UnlockBadge(string id)
        {
            if (save.badges.Contains(id)) return;

            save.badges.Add(id);
            Save();
            int index = Array.IndexOf(BadgeIds, id);
            if (index >= 0) ShowToast("実績解除：" + BadgeNames[index]);
        }

        private void ShowToast(string value)
        {
            toast = value;
            toastUntil = Time.unscaledTime + 2.5f;
        }

        private static string ScoreText(int value)
        {
            return Mathf.Max(0, value).ToString("000000000");
        }
    }
}
