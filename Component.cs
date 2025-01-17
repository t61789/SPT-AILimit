using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BepInEx;
using EFT;
using EFT.UI;
using EFTApi;
using UnityEngine;

namespace AiLimit
{
    [BepInPlugin("com.dvize.AILimit", "dvize.AILimit", "1.9.0")]
    public class AiLimitComponent : BaseUnityPlugin
    {
        private static readonly Dictionary<string, Func<float>> _maxDistanceConfigTaker = new Dictionary<string, Func<float>>
        {
            { "factory4_day", () => Settings.factoryDistance.Value },
            { "factory4_night", () => Settings.factoryDistance.Value },
            { "bigmap", () => Settings.customsDistance.Value },
            { "sandbox", () => Settings.groundZeroDistance.Value },
            { "interchange", () => Settings.interchangeDistance.Value },
            { "rezervbase", () => Settings.reserveDistance.Value },
            { "laboratory", () => Settings.laboratoryDistance.Value },
            { "lighthouse", () => Settings.lighthouseDistance.Value },
            { "shoreline", () => Settings.shorelineDistance.Value },
            { "woods", () => Settings.woodsDistance.Value },
            { "tarkovstreets", () => Settings.tarkovstreetsDistance.Value }
        };

        private static bool EnableDebugLog => Settings.EnableDebugLog.Value;
        private static readonly Stopwatch _sw = new Stopwatch();
        
        private float _checkTime;
        private readonly HashSet<Player> _centerPlayers = new HashSet<Player>();
        private SortedList<float, Player> _aiPlayers = new SortedList<float, Player>();
        private IEnumerator _processing;

        private void Awake()
        {
            Settings.Init(Config);
        }

        private void Update()
        {
            if (!Settings.PluginEnabled.Value || EFTHelpers._GameWorldHelper.GameWorld == null || EFTHelpers._PlayerHelper.Player == null)
            {
                // 不在战局里就不用排序
                _centerPlayers.Clear();
                _aiPlayers.Clear();
                _processing = null;
                return;
            }

            SetPlayersEnable();

            if (_processing != null)
            {
                if (_processing.MoveNext())
                {
                    return;
                }

                _processing = null;
                _checkTime = Time.time;
            }
            
            if (Time.time - _checkTime > Settings.CheckInterval.Value)
            {
                _processing = LoadAndSortPlayers();
            }
        }

        private IEnumerator LoadAndSortPlayers()
        {
            UpdateCenterPlayers(_centerPlayers);

            if (_centerPlayers.Count == 0)
            {
                // 玩家全死完了就什么也不干
                yield break;
            }
            
            var deadCount = 0;
            var sortingPlayers = new SortedList<float, Player>();
            
            foreach (var player in EFTHelpers._GameWorldHelper.AllOtherPlayer)
            {
                yield return null;
                
                if (player == null)
                {
                    continue;
                }

                // 是中心角色不隐藏
                if (_centerPlayers.Contains(player) || !player.IsAI)
                {
                    continue;
                }
                
                if (!player.HealthController.IsAlive)
                {
                    // 有时候尸体会被隐藏，不知道为啥，这里显示一下
                    // SetBotEnabled(player, true);
                    deadCount++;
                    continue;
                }

                // btr射手不隐藏
                if (player.AIData.BotOwner.IsRole(WildSpawnType.shooterBTR))
                {
                    continue;
                }

                // 还没激活不隐藏
                if (player.AIData.BotOwner.BotState != EBotState.Active)
                {
                    continue;
                }
                
                var distance = GetDistance(player, _centerPlayers);

                sortingPlayers.Add(distance, player);
            }
            
            if (EnableDebugLog)
            {
                ConsoleScreen.Log($"当前活跃角色 {sortingPlayers.Count} 个，死亡角色 {deadCount} 个， 中心角色 {string.Join(" ", from p in _centerPlayers select p.Profile.GetCorrectedNickname())}");
                var s = from p in sortingPlayers select $"({p.Value.Profile.GetCorrectedNickname()}, {Mathf.Sqrt(p.Key)}m)";
                ConsoleScreen.Log($"所有角色最小距离 {string.Join(" ", s)}");
            }

            _aiPlayers = sortingPlayers;
        }

        private void SetPlayersEnable()
        {
            if (_aiPlayers.Count == 0)
            {
                return;
            }
            
            var botLimit = Settings.BotLimit.Value;
            var maxDistance = GetMaxDistance();
            
            var count = 0;
            foreach (var (distance, player) in _aiPlayers)
            {
                if (player == null)
                {
                    continue;
                }

                bool enable;

                if (!player.HealthController.IsAlive)
                {
                    enable = true;
                    // 如果死了就不加入显示计数了，让更远处的角色直接显示出来
                }
                else
                {
                    enable = count < botLimit && distance < maxDistance * maxDistance;
                    count++;
                }
                
                SetBotEnabled(player, enable);
            }
        }
        
        private static float GetMaxDistance()
        {
            var location = EFTHelpers._PlayerHelper.Player.Location.ToLower();
            
            if (_maxDistanceConfigTaker.TryGetValue(location, out var distance))
            {
                return distance();
            }

            return 200;
        }

        private static void SetBotEnabled(Player player, bool enabled)
        {
            if ((player.gameObject.activeSelf || player.gameObject.activeInHierarchy) && !enabled)
            {
                player.AIData.BotOwner.DecisionQueue.Clear();
                player.AIData.BotOwner.Memory.GoalEnemy = null;
                player.AIData.BotOwner.PatrollingData.Pause();
                player.AIData.BotOwner.ShootData.EndShoot();
                player.AIData.BotOwner.ShootData.CanShootByState = false;
                player.AIData.BotOwner.StandBy.StandByType = BotStandByType.paused;
                player.AIData.BotOwner.StandBy.CanDoStandBy = false;
                player.ActiveHealthController.PauseAllEffects();
                player.gameObject.SetActive(false);
            }
            else if((!player.gameObject.activeSelf || !player.gameObject.activeInHierarchy) && enabled)
            {
                player.gameObject.SetActive(true);
                player.AIData.BotOwner.PatrollingData.Unpause();
                player.AIData.BotOwner.StandBy.Activate();
                player.AIData.BotOwner.StandBy.CanDoStandBy = true;
                player.AIData.BotOwner.ShootData.CanShootByState = true;
                player.AIData.BotOwner.ShootData.BlockFor(1f);
                player.ActiveHealthController.UnpauseAllEffects();
            }
        }

        private static bool IsCorpPlayer(Player player)
        {
            if (player == EFTHelpers._PlayerHelper.Player)
            {
                return false;
            }

            if (player.GroupId == "Fika")
            {
                return true;
            }

            return false;
        }

        private static void ExecuteWithProfiling(Action action, string formatStr, bool needTiming)
        {
            if (needTiming)
            {
                _sw.Restart();
            }

            action();

            if (needTiming)
            {
                _sw.Stop();
                ConsoleScreen.Log(string.Format(formatStr, _sw.Elapsed.TotalMilliseconds));
            }
        }

        private static void UpdateCenterPlayers(HashSet<Player> centerPlayers)
        {
            centerPlayers.Clear();
            
            TryAddToSet(EFTHelpers._PlayerHelper.Player);
            foreach (var p in EFTHelpers._GameWorldHelper.AllOtherPlayer)
            {
                TryAddToSet(p);
            }

            return;

            void TryAddToSet(Player player)
            {
                if (player == null)
                {
                    return;
                }

                if (!player.IsYourPlayer && !IsCorpPlayer(player))
                {
                    return;
                }

                if (!player.HealthController.IsAlive)
                {
                    return;
                }
                
                centerPlayers.Add(player);
            }
        }

        private static float GetDistance(Player bot, HashSet<Player> centerPlayers)
        {
            var minDistance = float.MaxValue;
            foreach (var centerPlayer in centerPlayers)
            {
                minDistance = Mathf.Min(minDistance, (centerPlayer.PlayerBones.BodyTransform.position - bot.PlayerBones.BodyTransform.position).sqrMagnitude);
            }

            return minDistance;
        }
    }
}
