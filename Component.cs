using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using EFT;
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

        private static readonly IComparer<(float distance, Player player)> _comparer = Comparer<(float distance, Player player)>.Create(
            (x, y) => x.distance.CompareTo(y.distance));
        private SortedSet<(float distance, Player player)> _sortedPlayers0 = new SortedSet<(float distance, Player player)>(_comparer);
        private SortedSet<(float distance, Player player)> _sortedPlayers1 = new SortedSet<(float distance, Player player)>(_comparer);
        private readonly Stopwatch _sw = new Stopwatch();
        private float _checkTime;
        private readonly HashSet<Player> _corpPlayers = new HashSet<Player>();

        private void Awake()
        {
            Settings.Init(Config);
        }

        private void Update()
        {
            if (!Settings.PluginEnabled.Value || EFTHelpers._GameWorldHelper.GameWorld == null || EFTHelpers._PlayerHelper.Player == null)
            {
                // 不在战局里就不用排序
                _sortedPlayers0.Clear();
                _sortedPlayers1.Clear();
                _corpPlayers.Clear();
                return;
            }
            
            if (Time.time - _checkTime > Settings.CheckInterval.Value)
            {
                _checkTime = Time.time;
                
                LoadAndSortPlayers();
            }
            
            SetPlayersEnable();
        }

        private void LoadAndSortPlayers()
        {
            _sw.Restart();
            
            _sortedPlayers0.Clear();
            foreach (var player in EFTHelpers._GameWorldHelper.AllOtherPlayer)
            {
                if (player == null || 
                    player.gameObject == null || 
                    !player.HealthController.IsAlive)
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
                
                var distance = Vector3.SqrMagnitude(GetCenterPlayer().PlayerBones.BodyTransform.position -
                                                    player.PlayerBones.BodyTransform.position);
                _sortedPlayers0.Add((distance, player));
            }
            
            (_sortedPlayers0, _sortedPlayers1) = (_sortedPlayers1, _sortedPlayers0);
            
            _sw.Stop();
            
            Logger.LogDebug($"ailimit 读取和排序角色耗时 {_sw.Elapsed.TotalMilliseconds}");
        }

        private void SetPlayersEnable()
        {
            if (_sortedPlayers1.Count == 0)
            {
                return;
            }
            
            // _sw.Restart();
            
            var maxDistance = GetMaxDistance();
            var botLimit = Settings.BotLimit.Value;
            
            var count = 0;
            foreach (var (distance, player) in _sortedPlayers1)
            {
                if (player == null || player.gameObject == null)
                {
                    continue;
                }
                
                var enable = !player.HealthController.IsAlive || (count < botLimit && distance < maxDistance * maxDistance);
                SetBotEnabled(player, enable);
                count++;
            }
            
            // _sw.Stop();
            
            // Logger.LogDebug($"ailimit 设置角色隐藏耗时 {_sw.Elapsed.TotalMilliseconds}");
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
    }
}
