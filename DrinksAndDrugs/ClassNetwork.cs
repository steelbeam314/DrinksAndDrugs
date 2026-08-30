using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CUCoreLib.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DrinksAndDrugs
{
    /// <summary>
    /// Host simulation owns BodyStatus. Each client tells the host its chosen class
    /// so the matching body can be assigned; otherwise only the host's class works.
    /// </summary>
    internal static class ClassNetwork
    {
        private const string Channel = "drinksanddrugs.class";
        private const float SendInterval = 1f;

        private static readonly Dictionary<string, string> ClassByPlayerName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ulong, string> ClassBySteamId = new Dictionary<ulong, string>();

        private static bool _registered;
        private static float _nextSendTime;
        private static Type _netPlayerType;
        private static FieldInfo _localPlayerField;
        private static FieldInfo _allLivingPlayersField;
        private static FieldInfo _playerNameField;
        private static FieldInfo _steamIdField;
        private static FieldInfo _bodyField;
        private static MethodInfo _getNetPlayerFromBody;

        public static void Register()
        {
            if (_registered)
                return;

            _registered = true;
            MultiplayerApi.RegisterServerHandler(Channel, HandleServer);
        }

        public static void Tick()
        {
            if (!ClassSelection.IsMultiplayerEnabled() || !MultiplayerApi.IsRunning)
                return;

            if (Time.unscaledTime < _nextSendTime)
                return;

            _nextSendTime = Time.unscaledTime + SendInterval;

            if (MultiplayerApi.IsClient && MultiplayerBridge.IsConnected)
                SendLocalClass();

            if (MultiplayerApi.IsServer || MultiplayerApi.IsHost)
                ApplyToMatchingPlayers();
        }

        public static bool TryGetClassForBody(Body body, out string classId)
        {
            classId = null;
            object netPlayer = GetNetPlayerFromBody(body);
            return TryGetClassForNetPlayer(netPlayer, out classId);
        }

        private static void SendLocalClass()
        {
            object local = GetLocalNetPlayer();
            var payload = new JObject
            {
                ["classId"] = PlayerClasses.NormalizeClassId(Plugin.SelectedClassId)
            };

            string playerName = GetPlayerName(local);
            if (!string.IsNullOrEmpty(playerName))
                payload["playerName"] = playerName;

            ulong steamId = GetSteamId(local);
            if (steamId != 0)
                payload["steamId"] = steamId;

            MultiplayerApi.SendToServer(Channel, payload, true);
        }

        private static JToken HandleServer(JToken payload)
        {
            JObject obj = payload as JObject;
            if (obj == null)
                return null;

            string classId = PlayerClasses.NormalizeClassId(obj.Value<string>("classId"));
            string playerName = obj.Value<string>("playerName");
            ulong steamId = obj.Value<ulong?>("steamId") ?? 0UL;

            if (!string.IsNullOrEmpty(playerName))
                ClassByPlayerName[playerName] = classId;

            if (steamId != 0)
                ClassBySteamId[steamId] = classId;

            ApplyToMatchingPlayers();
            return null;
        }

        private static void ApplyToMatchingPlayers()
        {
            IEnumerable players = GetLivingPlayers();
            if (players == null)
                return;

            foreach (object netPlayer in players)
            {
                if (!TryGetClassForNetPlayer(netPlayer, out string classId))
                    continue;

                Body body = GetBody(netPlayer);
                if (body != null)
                    PlayerClasses.ApplyClass(body, classId);
            }
        }

        private static bool TryGetClassForNetPlayer(object netPlayer, out string classId)
        {
            classId = null;
            if (netPlayer == null)
                return false;

            ulong steamId = GetSteamId(netPlayer);
            if (steamId != 0 && ClassBySteamId.TryGetValue(steamId, out classId))
                return true;

            string playerName = GetPlayerName(netPlayer);
            return !string.IsNullOrEmpty(playerName) && ClassByPlayerName.TryGetValue(playerName, out classId);
        }

        private static bool TryResolveNetPlayer()
        {
            if (_netPlayerType != null)
                return true;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("KrokoshaCasualtiesMP.NetPlayer", false);
                if (type == null)
                    continue;

                _netPlayerType = type;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                _localPlayerField = type.GetField("LOCAL_PLAYER", flags);
                _allLivingPlayersField = type.GetField("AllLivingPlayers", flags);
                _playerNameField = type.GetField("playername", flags);
                _steamIdField = type.GetField("steam_id", flags);
                _bodyField = type.GetField("body", flags);
                _getNetPlayerFromBody = type.GetMethod(
                    "GetNetPlayerFromBody",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Body) },
                    null);
                return true;
            }

            return false;
        }

        private static object GetLocalNetPlayer()
        {
            return TryResolveNetPlayer() ? _localPlayerField?.GetValue(null) : null;
        }

        private static object GetNetPlayerFromBody(Body body)
        {
            if (body == null || !TryResolveNetPlayer() || _getNetPlayerFromBody == null)
                return null;

            return _getNetPlayerFromBody.Invoke(null, new object[] { body });
        }

        private static IEnumerable GetLivingPlayers()
        {
            if (!TryResolveNetPlayer())
                return null;

            return _allLivingPlayersField?.GetValue(null) as IEnumerable;
        }

        private static string GetPlayerName(object netPlayer)
        {
            return netPlayer == null ? null : _playerNameField?.GetValue(netPlayer) as string;
        }

        private static ulong GetSteamId(object netPlayer)
        {
            if (netPlayer == null || _steamIdField == null)
                return 0;

            object raw = _steamIdField.GetValue(netPlayer);
            return raw == null ? 0UL : Convert.ToUInt64(raw);
        }

        private static Body GetBody(object netPlayer)
        {
            return netPlayer == null ? null : _bodyField?.GetValue(netPlayer) as Body;
        }
    }
}
