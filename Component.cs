using System;
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
        private struct SortingPlayer
        {
            public float distance;
            public Player player;
        }
        
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
        private readonly HashSet<Player> _corpPlayers = new HashSet<Player>();
        private readonly List<SortingPlayer> _aiPlayers = new List<SortingPlayer>();

        private void Awake()
        {
            Settings.Init(Config);
        }

        private void Update()
        {
            if (!Settings.PluginEnabled.Value || EFTHelpers._GameWorldHelper.GameWorld == null || EFTHelpers._PlayerHelper.Player == null)
            {
                // 不在战局里就不用排序
                _corpPlayers.Clear();
                return;
            }
            
            if (Time.time - _checkTime > Settings.CheckInterval.Value)
            {
                _checkTime = Time.time;
                
                ExecuteWithTiming(LoadAndSortPlayers, "读取和排序角色耗时 {0}ms", EnableDebugLog);
            }
            
            ExecuteWithTiming(SetPlayersEnable, "设置角色是否显示耗时 {0}ms", EnableDebugLog);
        }

        private void LoadAndSortPlayers()
        {
            _corpPlayers.Clear();
            _aiPlayers.Clear();
            
            var deadCount = 0;
            foreach (var player in EFTHelpers._GameWorldHelper.AllOtherPlayer)
            {
                if (player == null || 
                    player.gameObject == null)
                {
                    continue;
                }
                
                // 遍历其他角色的时候顺便找一下队友
                // 第一次遍历的时候可能不太对，但是遍历完第一次以后就对了
                // 以及如果这个人是队友的话直接跳过，不隐藏
                if (IsCorpPlayer(player))
                {
                    _corpPlayers.Add(player);
                    continue;
                }

                if (!player.HealthController.IsAlive)
                {
                    // 有时候尸体会被隐藏，不知道为啥，这里显示一下
                    SetBotEnabled(player, true);
                    deadCount++;
                    continue;
                }
                
                var centerPlayer = GetCenterPlayer();
                var distance = Vector3.SqrMagnitude(centerPlayer.PlayerBones.BodyTransform.position -
                                                    player.PlayerBones.BodyTransform.position);

                _aiPlayers.Add(new SortingPlayer
                {
                    distance = distance,
                    player = player
                });
            }
            
            _aiPlayers.Sort((x, y)=>x.distance.CompareTo(y.distance));

            if (EnableDebugLog)
            {
                ConsoleScreen.Log($"当前活跃角色 {_aiPlayers.Count} 个，尸体 {deadCount} 个，中心角色 {GetCenterPlayer()?.Profile.GetCorrectedNickname()}");
            }
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
            foreach (var p in _aiPlayers)
            {
                var player = p.player;
                var distance = p.distance;
                
                if (player == null || player.gameObject == null)
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

        private Player GetCenterPlayer()
        {
            var yourPlayer = EFTHelpers._PlayerHelper.Player;
            if (yourPlayer.HealthController.IsAlive)
            {
                return yourPlayer;
            }

            foreach (var p in _corpPlayers)
            {
                if (p != null && p.HealthController.IsAlive)
                {
                    return p;
                }
            }

            return yourPlayer;
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
                var aiData = player.AIData;
                if (aiData != null)
                {
                    var botOwner = aiData.BotOwner;
                    if (botOwner != null)
                    {
                        botOwner?.DecisionQueue.Clear();
                        var memory = botOwner.Memory;
                        if (memory != null)
                        {
                            memory.GoalEnemy = null;
                        }
                    }
                }
                player.gameObject.SetActive(false);
                player.WeaponRoot?.Original?.gameObject?.SetActive(false);
            }
            else if((!player.gameObject.activeSelf || !player.gameObject.activeInHierarchy) && enabled)
            {
                player.gameObject.SetActive(true);
                player.WeaponRoot?.Original?.gameObject?.SetActive(true);
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

        private void ExecuteWithTiming(Action action, string formatStr, bool needTiming)
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
    }
}
