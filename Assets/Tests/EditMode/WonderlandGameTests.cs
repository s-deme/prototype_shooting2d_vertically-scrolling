using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AliceMirrorfall.Tests
{
    public sealed class WonderlandGameTests
    {
        private GameObject host;
        private WonderlandGame game;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("WonderlandGameTests");
            game = host.AddComponent<WonderlandGame>();
            FieldInfo save = Field("save");
            save.SetValue(game, Activator.CreateInstance(save.FieldType));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void ClearedEnemyShotsRemainUntilUpdateCreatesTheirSparks()
        {
            IList shots = (IList)Field("enemyShots").GetValue(game);
            Type shotType = Field("enemyShots").FieldType.GetGenericArguments()[0];
            for (int i = 0; i < 2; i++)
            {
                object shot = Activator.CreateInstance(shotType);
                shotType.GetField("pos").SetValue(shot, new Vector2(100 + i, 200));
                shots.Add(shot);
            }
            IList sparks = (IList)Field("sparks").GetValue(game);
            IList stars = (IList)Field("stars").GetValue(game);
            stars.Add(new Vector4(1, 2, 3, 4));

            Invoke("MarkEnemyShotsCleared");
            Assert.That(shots.Count, Is.EqualTo(2));
            Assert.That(sparks.Count, Is.EqualTo(0));
            foreach (object shot in shots) Assert.That((bool)shotType.GetField("cleared").GetValue(shot), Is.True);

            Invoke("UpdateEnemyShots", .02f);
            Assert.That(shots.Count, Is.EqualTo(0));
            Assert.That(sparks.Count, Is.EqualTo(2));
            Assert.That(stars.Count, Is.EqualTo(1));
            Assert.That((Vector4)stars[0], Is.EqualTo(new Vector4(1, 2, 3, 4)));
        }

        [Test]
        public void ClearingRunEntitiesEmptiesAllFiveListsAndKeepsTheStars()
        {
            string[] names = { "foes", "playerShots", "enemyShots", "pickups", "sparks" };
            foreach (string name in names)
            {
                FieldInfo field = Field(name);
                ((IList)field.GetValue(game)).Add(Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]));
            }
            IList stars = (IList)Field("stars").GetValue(game);
            stars.Add(new Vector4(5, 6, 7, 8));

            Invoke("ClearRunEntities");

            foreach (string name in names) Assert.That(((IList)Field(name).GetValue(game)).Count, Is.EqualTo(0), name);
            Assert.That(stars.Count, Is.EqualTo(1));
            Assert.That((Vector4)stars[0], Is.EqualTo(new Vector4(5, 6, 7, 8)));
        }

        [TestCase(-10f, 700f, 18f, 621f)]
        [TestCase(500f, 0f, 462f, 74f)]
        [TestCase(240f, 300f, 240f, 300f)]
        public void PlayerPositionIsClampedToTheExistingBounds(float x, float y, float expectedX, float expectedY)
        {
            Vector2 result = (Vector2)Invoke("ClampPlayerPosition", new Vector2(x, y));
            Assert.That(result, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void TouchMovementKeepsFivePixelStepsAndStopsAtTheBoundary()
        {
            Invoke("MoveTouch", Vector2.right);
            FieldInfo player = Field("player");
            object unit = Activator.CreateInstance(player.FieldType);
            player.SetValue(game, unit);
            FieldInfo position = player.FieldType.GetField("pos");
            position.SetValue(unit, new Vector2(240, 300));

            Invoke("MoveTouch", Vector2.right);
            Assert.That((Vector2)position.GetValue(unit), Is.EqualTo(new Vector2(245, 300)));
            position.SetValue(unit, new Vector2(461, 620));
            Invoke("MoveTouch", Vector2.right);
            Assert.That((Vector2)position.GetValue(unit), Is.EqualTo(new Vector2(462, 620)));
            Invoke("MoveTouch", Vector2.down);
            Assert.That((Vector2)position.GetValue(unit), Is.EqualTo(new Vector2(462, 615)));
        }

        [Test]
        public void ManualPauseAndResumeKeepTheExistingToast()
        {
            Field("state").SetValue(game, GameState.Playing);
            Field("toast").SetValue(game, "existing notification");
            Field("toastUntil").SetValue(game, 123f);

            Invoke("PauseGame", new object[] { null });
            Assert.That((GameState)Field("state").GetValue(game), Is.EqualTo(GameState.Paused));
            Invoke("ResumeGame");
            Assert.That((GameState)Field("state").GetValue(game), Is.EqualTo(GameState.Playing));
            Assert.That((string)Field("toast").GetValue(game), Is.EqualTo("existing notification"));
            Assert.That((float)Field("toastUntil").GetValue(game), Is.EqualTo(123f));
        }

        [TestCase("OnApplicationFocus", false, "フォーカスが外れたため一時停止しました")]
        [TestCase("OnApplicationPause", true, "アプリが中断されたため一時停止しました")]
        public void ApplicationInterruptionOnlyPausesAnActiveGame(string method, bool interruption, string message)
        {
            FieldInfo state = Field("state");
            FieldInfo toast = Field("toast");
            state.SetValue(game, GameState.Playing);
            toast.SetValue(game, "unchanged");
            Invoke(method, !interruption);
            Assert.That((GameState)state.GetValue(game), Is.EqualTo(GameState.Playing));
            Assert.That((string)toast.GetValue(game), Is.EqualTo("unchanged"));

            Invoke(method, interruption);
            Assert.That((GameState)state.GetValue(game), Is.EqualTo(GameState.Paused));
            Assert.That((string)toast.GetValue(game), Is.EqualTo(message));

            foreach (GameState inactive in new[] { GameState.Title, GameState.Paused, GameState.Result })
            {
                state.SetValue(game, inactive);
                toast.SetValue(game, "unchanged");
                Invoke(method, interruption);
                Assert.That((GameState)state.GetValue(game), Is.EqualTo(inactive));
                Assert.That((string)toast.GetValue(game), Is.EqualTo("unchanged"));
            }
        }

        private static FieldInfo Field(string name)
        {
            return typeof(WonderlandGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private object Invoke(string name, params object[] arguments)
        {
            return typeof(WonderlandGame).GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).Invoke(game, arguments);
        }
    }
}
