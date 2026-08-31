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
    /// In multiplayer the host assigns classes with <c>setclass class username</c>.
    /// The host simulates every body, so that is the only assignment that actually applies.
    /// </summary>
    internal static class ClassNetwork
    {
        private const string Channel = "drinksanddrugs.class.assign";
        private const float ApplyInterval = 0.5f;

        private static readonly Dictionary<string, string> ClassByPlayerName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static bool _registered;
        private static float _nextApplyTime;
        private static Type _netPlayerType;
        private static FieldInfo _localPlayerField;
        private static FieldInfo _allLivingPlayersField;
        private static FieldInfo _allDeadPlayersField;
        private static FieldInfo _clientIdToPlayerField;
        private static FieldInfo _playerNameField;
        private static FieldInfo _bodyField;
        private static FieldInfo _isLocalField;
        private static MethodInfo _getNetPlayerFromBody;

        public static void Register()
        {
            if (_registered)
                return;

            _registered = true;
            MultiplayerApi.RegisterClientHandler(Channel, HandleClientAssign);
        }

        public static void Tick()
        {
            if (!ClassSelection.IsMultiplayerSession() || ClassByPlayerName.Count == 0)
                return;

            if (Time.unscaledTime < _nextApplyTime)
                return;

            _nextApplyTime = Time.unscaledTime + ApplyInterval;

            if (IsHost())
                ApplyToMatchingPlayers();
        }

        public static bool IsHost()
        {
            return MultiplayerApi.IsHost || MultiplayerApi.IsServer;
        }

        public static bool TryAssignByPlayerName(string classId, string playerName, out string error, out string matchedName, out bool appliedNow)
        {
            error = null;
            matchedName = null;
            appliedNow = false;
            classId = PlayerClasses.NormalizeClassId(classId);

            if (string.IsNullOrWhiteSpace(playerName))
            {
                error = "Missing username. Usage: setclass <class> <username>";
                return false;
            }

            object netPlayer = FindPlayerByName(playerName, out string fail);
            if (netPlayer == null)
            {
                error = fail;
                return false;
            }

            matchedName = GetPlayerName(netPlayer);
            if (string.IsNullOrEmpty(matchedName))
                matchedName = playerName.Trim();

            ClassByPlayerName[matchedName] = classId;

            Body body = GetBody(netPlayer);
            if (body != null)
            {
                PlayerClasses.ApplyClass(body, classId);
                appliedNow = true;
            }

            if (IsLocalNetPlayer(netPlayer))
                Plugin.SelectedClassId = classId;

            NotifyClients(matchedName, classId);
            return true;
        }

        public static bool TryGetClassForBody(Body body, out string classId)
        {
            classId = null;
            object netPlayer = GetNetPlayerFromBody(body);
            if (netPlayer == null)
                return false;

            string playerName = GetPlayerName(netPlayer);
            return !string.IsNullOrEmpty(playerName) && ClassByPlayerName.TryGetValue(playerName, out classId);
        }

        public static string FormatPlayerList()
        {
            List<string> names = GetOnlinePlayerNames();
            if (names.Count == 0)
                return "No players found yet.";

            var lines = new List<string>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                string className = ClassByPlayerName.TryGetValue(name, out string classId)
                    ? ClassSelection.DisplayName(classId)
                    : "unset";
                lines.Add(name + " (" + className + ")");
            }

            return string.Join(", ", lines.ToArray());
        }

        private static void NotifyClients(string playerName, string classId)
        {
            if (!MultiplayerApi.IsAvailable || !MultiplayerApi.IsServer)
                return;

            MultiplayerApi.Broadcast(
                Channel,
                new JObject
                {
                    ["playerName"] = playerName,
                    ["classId"] = classId
                },
                false,
                true);
        }

        private static void HandleClientAssign(JToken payload)
        {
            JObject obj = payload as JObject;
            if (obj == null)
                return;

            string playerName = obj.Value<string>("playerName");
            string classId = PlayerClasses.NormalizeClassId(obj.Value<string>("classId"));
            if (string.IsNullOrEmpty(playerName))
                return;

            ClassByPlayerName[playerName] = classId;

            object local = GetLocalNetPlayer();
            string localName = GetPlayerName(local);
            if (!NamesMatch(localName, playerName))
                return;

            Plugin.SelectedClassId = classId;
            Body body = GetBody(local) ?? PlayerClasses.LocalBody();
            if (body != null)
                PlayerClasses.ApplyClass(body, classId);
        }

        private static void ApplyToMatchingPlayers()
        {
            foreach (object netPlayer in EnumeratePlayers())
            {
                string playerName = GetPlayerName(netPlayer);
                if (string.IsNullOrEmpty(playerName) || !ClassByPlayerName.TryGetValue(playerName, out string classId))
                    continue;

                Body body = GetBody(netPlayer);
                if (body != null)
                    PlayerClasses.ApplyClass(body, classId);
            }
        }

        private static object FindPlayerByName(string rawName, out string error)
        {
            error = null;
            string wanted = rawName.Trim();
            var exact = new List<object>();
            var partial = new List<object>();

            foreach (object netPlayer in EnumeratePlayers())
            {
                string name = GetPlayerName(netPlayer);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (NamesMatch(name, wanted))
                    exact.Add(netPlayer);
                else if (name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                    partial.Add(netPlayer);
            }

            List<object> matches = exact.Count > 0 ? exact : partial;
            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1)
            {
                error = "Multiple players match '" + wanted + "'. Online: " + FormatPlayerList();
                return null;
            }

            error = "No player named '" + wanted + "'. Online: " + FormatPlayerList();
            return null;
        }

        private static List<string> GetOnlinePlayerNames()
        {
            var names = new List<string>();
            foreach (object netPlayer in EnumeratePlayers())
            {
                string name = GetPlayerName(netPlayer);
                if (string.IsNullOrEmpty(name) || ContainsName(names, name))
                    continue;

                names.Add(name);
            }

            return names;
        }

        private static bool ContainsName(List<string> names, string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<object> EnumeratePlayers()
        {
            var seen = new HashSet<object>();
            foreach (IEnumerable list in new IEnumerable[]
                     {
                         GetLivingPlayers(),
                         GetDeadPlayers(),
                         GetDictPlayers()
                     })
            {
                if (list == null)
                    continue;

                foreach (object netPlayer in list)
                {
                    if (netPlayer == null || !seen.Add(netPlayer))
                        continue;

                    yield return netPlayer;
                }
            }

            object local = GetLocalNetPlayer();
            if (local != null && seen.Add(local))
                yield return local;
        }

        private static bool NamesMatch(string a, string b)
        {
            return !string.IsNullOrEmpty(a)
                && !string.IsNullOrEmpty(b)
                && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
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
                _allDeadPlayersField = type.GetField("AllDeadPlayers", flags);
                _clientIdToPlayerField = type.GetField("ClientIdToPlayerDict", flags);
                _playerNameField = type.GetField("playername", flags);
                _bodyField = type.GetField("body", flags);
                _isLocalField = type.GetField("is_local", flags)
                    ?? type.GetField("<is_local>k__BackingField", flags);
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

        private static bool IsLocalNetPlayer(object netPlayer)
        {
            if (netPlayer == null)
                return false;

            if (_isLocalField != null)
            {
                object raw = _isLocalField.GetValue(netPlayer);
                if (raw is bool isLocal)
                    return isLocal;
            }

            return ReferenceEquals(netPlayer, GetLocalNetPlayer());
        }

        private static object GetNetPlayerFromBody(Body body)
        {
            if (body == null || !TryResolveNetPlayer() || _getNetPlayerFromBody == null)
                return null;

            return _getNetPlayerFromBody.Invoke(null, new object[] { body });
        }

        private static IEnumerable GetLivingPlayers()
        {
            return TryResolveNetPlayer() ? _allLivingPlayersField?.GetValue(null) as IEnumerable : null;
        }

        private static IEnumerable GetDeadPlayers()
        {
            return TryResolveNetPlayer() ? _allDeadPlayersField?.GetValue(null) as IEnumerable : null;
        }

        private static IEnumerable GetDictPlayers()
        {
            if (!TryResolveNetPlayer())
                return null;

            object dict = _clientIdToPlayerField?.GetValue(null);
            if (dict is IDictionary map)
                return map.Values;

            return dict as IEnumerable;
        }

        private static string GetPlayerName(object netPlayer)
        {
            return netPlayer == null ? null : _playerNameField?.GetValue(netPlayer) as string;
        }

        private static Body GetBody(object netPlayer)
        {
            return netPlayer == null ? null : _bodyField?.GetValue(netPlayer) as Body;
        }
    }
}
