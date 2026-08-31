using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using CUCoreLib.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DrinksAndDrugs
{
    /// <summary>
    /// In multiplayer the host assigns classes with <c>setclass class username</c>.
    /// The host simulates every body, then tells that client their class directly.
    /// </summary>
    internal static class ClassNetwork
    {
        private const string Channel = "drinksanddrugs.class.assign";
        private const float ApplyInterval = 0.25f;

        private static readonly Dictionary<string, string> ClassByPlayerName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ushort, string> ClassByClientId = new Dictionary<ushort, string>();

        private static bool _registered;
        private static float _nextApplyTime;
        private static string _localAssignedClassId;
        private static Type _netPlayerType;
        private static Type _netBodyType;
        private static FieldInfo _localPlayerField;
        private static FieldInfo _allLivingPlayersField;
        private static FieldInfo _allDeadPlayersField;
        private static FieldInfo _clientIdToPlayerField;
        private static FieldInfo _bodyToPlayerField;
        private static FieldInfo _playerNameField;
        private static FieldInfo _bodyField;
        private static FieldInfo _namePrefixField;
        private static FieldInfo _nametagPrefixField;
        private static PropertyInfo _isLocalProp;
        private static PropertyInfo _clientIdProp;
        private static PropertyInfo _playerBodyProp;
        private static PropertyInfo _netBodyBodyProp;
        private static PropertyInfo _netBodyNameProp;
        private static MethodInfo _getNetPlayerFromBody;
        private static MethodInfo _getBodyFromClientId;

        public static void Register()
        {
            if (_registered)
                return;

            _registered = true;
            MultiplayerApi.RegisterClientHandler(Channel, HandleClientAssign);
        }

        public static void Tick()
        {
            if (!ClassSelection.IsMultiplayerSession())
                return;

            if (Time.unscaledTime < _nextApplyTime)
                return;

            _nextApplyTime = Time.unscaledTime + ApplyInterval;

            if (IsHost())
            {
                try
                {
                    ApplyToMatchingPlayers();
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning("ClassNetwork apply failed: " + ex);
                }

                return;
            }

            ApplyLocalAssignment();
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

            matchedName = GetBestPlayerName(netPlayer) ?? playerName.Trim();
            RememberAssignment(netPlayer, classId);

            Body body = ResolveBody(netPlayer);
            if (body != null)
            {
                PlayerClasses.ApplyClass(body, classId);
                appliedNow = true;
            }

            if (IsLocalNetPlayer(netPlayer))
            {
                _localAssignedClassId = classId;
                Plugin.SelectedClassId = classId;
            }

            NotifyClient(netPlayer, classId);
            Plugin.Logger?.LogInfo("Assigned " + ClassSelection.DisplayName(classId) + " to " + matchedName +
                " (body=" + (body != null) + ", clientId=" + GetClientId(netPlayer) + ")");
            return true;
        }

        public static bool TryGetClassForBody(Body body, out string classId)
        {
            classId = null;
            if (body == null)
                return false;

            object netPlayer = GetNetPlayerFromBody(body);
            if (netPlayer != null)
            {
                ushort clientId = GetClientId(netPlayer);
                if (clientId != 0 && ClassByClientId.TryGetValue(clientId, out classId))
                    return true;

                if (TryGetClassForNames(GetNameCandidates(netPlayer), out classId))
                    return true;
            }

            if (IsLocalBody(body) && !string.IsNullOrEmpty(_localAssignedClassId))
            {
                classId = _localAssignedClassId;
                return true;
            }

            return false;
        }

        public static string FormatPlayerList()
        {
            try
            {
                var lines = new List<string>();
                foreach (object netPlayer in EnumeratePlayers())
                {
                    if (IsDestroyed(netPlayer))
                        continue;

                    string name = GetBestPlayerName(netPlayer);
                    if (string.IsNullOrEmpty(name) || ContainsName(lines, name))
                        continue;

                    string className = "unset";
                    ushort clientId = GetClientId(netPlayer);
                    if (clientId != 0 && ClassByClientId.TryGetValue(clientId, out string byId))
                        className = ClassSelection.DisplayName(byId);
                    else if (TryGetClassForNames(GetNameCandidates(netPlayer), out string byName))
                        className = ClassSelection.DisplayName(byName);

                    lines.Add(name + " (" + className + ")");
                }

                return lines.Count == 0 ? "No players found yet." : string.Join(", ", lines.ToArray());
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("ClassNetwork player list failed: " + ex);
                return "(could not list players: " + ex.GetType().Name + ")";
            }
        }

        private static void RememberAssignment(object netPlayer, string classId)
        {
            ushort clientId = GetClientId(netPlayer);
            if (clientId != 0)
                ClassByClientId[clientId] = classId;

            foreach (string name in GetNameCandidates(netPlayer))
                ClassByPlayerName[name] = classId;
        }

        private static void NotifyClient(object netPlayer, string classId)
        {
            if (!MultiplayerApi.IsAvailable || !MultiplayerApi.IsServer || IsLocalNetPlayer(netPlayer))
                return;

            try
            {
                var payload = new JObject
                {
                    ["classId"] = classId,
                    ["playerName"] = GetBestPlayerName(netPlayer)
                };

                ushort clientId = GetClientId(netPlayer);
                if (clientId != 0)
                {
                    payload["forLocal"] = true;
                    if (MultiplayerApi.SendToClient(clientId, Channel, payload, true))
                        return;
                }

                payload["forLocal"] = false;
                MultiplayerApi.Broadcast(Channel, payload, false, true);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("ClassNetwork notify failed: " + ex);
            }
        }

        private static void HandleClientAssign(JToken payload)
        {
            JObject obj = payload as JObject;
            if (obj == null)
                return;

            string classId = PlayerClasses.NormalizeClassId(obj.Value<string>("classId"));
            string playerName = obj.Value<string>("playerName");
            bool forLocal = obj.Value<bool?>("forLocal") == true;

            if (!string.IsNullOrEmpty(playerName))
                ClassByPlayerName[playerName] = classId;

            object local = GetLocalNetPlayer();
            bool isForMe = forLocal || NamesMatch(GetBestPlayerName(local), playerName);
            if (!isForMe && !string.IsNullOrEmpty(playerName))
            {
                foreach (string name in GetNameCandidates(local))
                {
                    if (NamesMatch(name, playerName))
                    {
                        isForMe = true;
                        break;
                    }
                }
            }

            if (!isForMe)
                return;

            _localAssignedClassId = classId;
            Plugin.SelectedClassId = classId;
            ApplyLocalAssignment();
        }

        private static void ApplyLocalAssignment()
        {
            if (string.IsNullOrEmpty(_localAssignedClassId))
                return;

            Body body = PlayerClasses.LocalBody();
            if (body != null)
                PlayerClasses.ApplyClass(body, _localAssignedClassId);
        }

        private static void ApplyToMatchingPlayers()
        {
            foreach (object netPlayer in EnumeratePlayers())
            {
                ushort clientId = GetClientId(netPlayer);
                string classId = null;
                if (clientId != 0)
                    ClassByClientId.TryGetValue(clientId, out classId);
                if (classId == null)
                    TryGetClassForNames(GetNameCandidates(netPlayer), out classId);
                if (classId == null)
                    continue;

                Body body = ResolveBody(netPlayer);
                if (body != null)
                    PlayerClasses.ApplyClass(body, classId);
            }
        }

        private static object FindPlayerByName(string rawName, out string error)
        {
            error = null;
            string wanted = NormalizeName(rawName);
            var exact = new List<object>();
            var partial = new List<object>();

            foreach (object netPlayer in EnumeratePlayers())
            {
                bool exactMatch = false;
                bool partialMatch = false;
                foreach (string name in GetNameCandidates(netPlayer))
                {
                    string normalized = NormalizeName(name);
                    if (normalized.Length == 0)
                        continue;

                    if (string.Equals(normalized, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        exactMatch = true;
                        break;
                    }

                    if (normalized.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0
                        || wanted.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                        partialMatch = true;
                }

                if (exactMatch)
                    exact.Add(netPlayer);
                else if (partialMatch)
                    partial.Add(netPlayer);
            }

            List<object> matches = exact.Count > 0 ? exact : partial;
            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1)
            {
                error = "Multiple players match '" + rawName.Trim() + "'. Online: " + FormatPlayerList();
                return null;
            }

            error = "No player named '" + rawName.Trim() + "'. Online: " + FormatPlayerList();
            return null;
        }

        private static bool TryGetClassForNames(IEnumerable<string> names, out string classId)
        {
            classId = null;
            if (names == null)
                return false;

            foreach (string name in names)
            {
                if (!string.IsNullOrEmpty(name) && ClassByPlayerName.TryGetValue(name, out classId))
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
                         GetDictPlayers(_clientIdToPlayerField),
                         GetDictPlayers(_bodyToPlayerField)
                     })
            {
                foreach (object netPlayer in YieldPlayers(list, seen))
                    yield return netPlayer;
            }

            object local = GetLocalNetPlayer();
            if (local != null && !IsDestroyed(local) && seen.Add(local))
                yield return local;
        }

        private static IEnumerable<object> YieldPlayers(IEnumerable list, HashSet<object> seen)
        {
            if (list == null)
                yield break;

            object[] snapshot;
            try
            {
                var copy = new List<object>();
                foreach (object item in list)
                    copy.Add(item);
                snapshot = copy.ToArray();
            }
            catch (Exception)
            {
                yield break;
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                object netPlayer = AsNetPlayer(snapshot[i]);
                if (netPlayer == null || IsDestroyed(netPlayer) || !seen.Add(netPlayer))
                    continue;

                yield return netPlayer;
            }
        }

        private static object AsNetPlayer(object item)
        {
            if (item == null || _netPlayerType == null || IsDestroyed(item))
                return null;

            try
            {
                if (_netPlayerType.IsInstanceOfType(item))
                    return item;
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static bool IsDestroyed(object obj)
        {
            if (obj == null)
                return true;

            return obj is UnityEngine.Object unityObj && unityObj == null;
        }

        private static List<string> GetNameCandidates(object netPlayer)
        {
            var names = new List<string>();
            AddName(names, GetPlayerName(netPlayer));
            AddName(names, StripPrefixes(GetPlayerName(netPlayer)));

            object netBody = GetNetBody(netPlayer);
            if (netBody != null)
            {
                try
                {
                    AddName(names, _netBodyNameProp?.GetValue(netBody, null) as string);
                    AddName(names, StripPrefixes(_netBodyNameProp?.GetValue(netBody, null) as string));
                }
                catch (Exception)
                {
                }
            }

            return names;
        }

        private static void AddName(List<string> names, string name)
        {
            name = NormalizeName(name);
            if (name.Length == 0 || ContainsName(names, name))
                return;

            names.Add(name);
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

        private static string GetBestPlayerName(object netPlayer)
        {
            List<string> names = GetNameCandidates(netPlayer);
            return names.Count > 0 ? names[0] : null;
        }

        private static object GetNetBody(object netPlayer)
        {
            if (netPlayer == null || IsDestroyed(netPlayer) || _playerBodyProp == null)
                return null;

            try
            {
                object netBody = _playerBodyProp.GetValue(netPlayer, null);
                return IsDestroyed(netBody) ? null : netBody;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GetPlayerName(object netPlayer)
        {
            if (netPlayer == null || IsDestroyed(netPlayer) || _playerNameField == null)
                return null;

            try
            {
                return _playerNameField.GetValue(netPlayer) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string StripPrefixes(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            string prefix = _namePrefixField?.GetValue(null) as string;
            string tagPrefix = _nametagPrefixField?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(prefix) && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(prefix.Length);
            if (!string.IsNullOrEmpty(tagPrefix) && name.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(tagPrefix.Length);

            return name;
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            name = Regex.Replace(name.Trim(), "<[^>]*>", string.Empty);
            return name.Trim();
        }

        private static bool NamesMatch(string a, string b)
        {
            a = NormalizeName(a);
            b = NormalizeName(b);
            return a.Length > 0 && b.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLocalBody(Body body)
        {
            return PlayerClasses.IsLocalBody(body);
        }

        private static Body ResolveBody(object netPlayer)
        {
            if (netPlayer == null)
                return null;

            object clientId = _clientIdProp?.GetValue(netPlayer, null);
            if (clientId != null && _getBodyFromClientId != null)
            {
                try
                {
                    if (_getBodyFromClientId.Invoke(null, new[] { clientId }) is Body fromId && fromId != null)
                        return fromId;
                }
                catch (Exception)
                {
                }
            }

            object netBody = GetNetBody(netPlayer);
            if (netBody != null && _netBodyBodyProp?.GetValue(netBody, null) is Body fromNetBody && fromNetBody != null)
                return fromNetBody;

            try
            {
                if (IsDestroyed(netPlayer) || _bodyField == null)
                    return null;

                return _bodyField.GetValue(netPlayer) as Body;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ushort GetClientId(object netPlayer)
        {
            object raw = netPlayer == null ? null : _clientIdProp?.GetValue(netPlayer, null);
            if (raw == null)
                return 0;

            if (raw is ushort already)
                return already;

            FieldInfo idField = raw.GetType().GetField("id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (idField != null)
                return Convert.ToUInt16(idField.GetValue(raw));

            try
            {
                return Convert.ToUInt16(raw);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool TryResolveNetPlayer()
        {
            if (_netPlayerType != null)
                return true;

            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType("KrokoshaCasualtiesMP.NetPlayer", false);
                    if (type == null)
                        continue;

                    _netPlayerType = type;
                    _netBodyType = assembly.GetType("KrokoshaCasualtiesMP.NetBody", false);
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    _localPlayerField = type.GetField("LOCAL_PLAYER", flags);
                    _allLivingPlayersField = type.GetField("AllLivingPlayers", flags);
                    _allDeadPlayersField = type.GetField("AllDeadPlayers", flags);
                    _clientIdToPlayerField = type.GetField("ClientIdToPlayerDict", flags);
                    _bodyToPlayerField = type.GetField("BodyToPlayerDict", flags);
                    _playerNameField = type.GetField("playername", flags);
                    _bodyField = type.GetField("body", flags);
                    _namePrefixField = type.GetField("plrnameprefix", flags);
                    _nametagPrefixField = type.GetField("plrnametagprefix", flags);
                    _isLocalProp = type.GetProperty("is_local", flags);
                    _clientIdProp = type.GetProperty("clientId", flags);
                    _playerBodyProp = type.GetProperty("playerbody", flags);
                    _getNetPlayerFromBody = type.GetMethod(
                        "GetNetPlayerFromBody",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                        null,
                        new[] { typeof(Body) },
                        null);
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (method.Name != "GetBodyFromClientId" || method.GetParameters().Length != 1)
                            continue;

                        _getBodyFromClientId = method;
                        break;
                    }

                    if (_netBodyType != null)
                    {
                        _netBodyBodyProp = _netBodyType.GetProperty("body", flags);
                        _netBodyNameProp = _netBodyType.GetProperty("playername", flags)
                            ?? _netBodyType.GetProperty("bodyname", flags);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogWarning("ClassNetwork could not resolve NetPlayer: " + ex);
            }

            return false;
        }

        private static object GetLocalNetPlayer()
        {
            try
            {
                object local = TryResolveNetPlayer() ? _localPlayerField?.GetValue(null) : null;
                return IsDestroyed(local) ? null : local;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object GetNetPlayerFromBody(Body body)
        {
            if (body == null || !TryResolveNetPlayer() || _getNetPlayerFromBody == null)
                return null;

            try
            {
                return _getNetPlayerFromBody.Invoke(null, new object[] { body });
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLocalNetPlayer(object netPlayer)
        {
            if (netPlayer == null || IsDestroyed(netPlayer))
                return false;

            try
            {
                if (_isLocalProp != null && _isLocalProp.GetValue(netPlayer, null) is bool isLocal)
                    return isLocal;
            }
            catch (Exception)
            {
            }

            return ReferenceEquals(netPlayer, GetLocalNetPlayer());
        }

        private static IEnumerable GetLivingPlayers()
        {
            return TryResolveNetPlayer() ? _allLivingPlayersField?.GetValue(null) as IEnumerable : null;
        }

        private static IEnumerable GetDeadPlayers()
        {
            return TryResolveNetPlayer() ? _allDeadPlayersField?.GetValue(null) as IEnumerable : null;
        }

        private static IEnumerable GetDictPlayers(FieldInfo field)
        {
            if (!TryResolveNetPlayer() || field == null)
                return null;

            object dict = field.GetValue(null);
            if (dict is IDictionary map)
                return map.Values;

            return dict as IEnumerable;
        }
    }
}
