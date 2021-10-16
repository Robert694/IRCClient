using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace IRCClient.Modules
{
    public class TwitchModule : MessageModule
    {
        public TwitchModule()
        {
            _roomStates = new Dictionary<string, RoomState>();
            RoomStates = new ReadOnlyDictionary<string, RoomState>(_roomStates);
            _RoomUsers = new Dictionary<string, Dictionary<string, HashSet<string>>>();
            RoomUsers = new ReadOnlyDictionary<string, Dictionary<string, HashSet<string>>>(_RoomUsers);
        }

        public static string CapCommands { get; } = "twitch.tv/commands";
        public static string CapTags { get; } = "twitch.tv/tags";
        public static string CapMembership { get; } = "twitch.tv/membership";

        public GlobalUserState UserState { get; private set; }
        public ReadOnlyDictionary<string, RoomState> RoomStates { get; }
        private Dictionary<string, RoomState> _roomStates { get; }

        public ReadOnlyDictionary<string, Dictionary<string, HashSet<string>>> RoomUsers { get; }
        private Dictionary<string, Dictionary<string, HashSet<string>>> _RoomUsers { get; }
        private string SelfHostMask { get; set; }
        private bool HostMaskSet { get; set; }
        public event EventHandler<ChatMessage> OnMessage;
        public event EventHandler<GlobalUserState> OnGlobalUserState;
        public event EventHandler<RoomState> OnRoomState;
        public event EventHandler<UserBan> OnUserBan;
        public event EventHandler<SubscribeData> OnUserSubscribe;
        public event EventHandler<string> OnUnsupportedTag;

        public override async Task Process(IrcMessage data)
        {
            switch (data.Command.ToUpper())
            {
                case "JOIN":
                {
                    if (!HostMaskSet && data.IsPrefixHostmask)
                    {
                        HostMaskSet = true;
                        SelfHostMask = data.Prefix;
                    }

                    var username = data.Prefix.Split('!')[0];
                    var room = data.Params[0];
                    if (!_RoomUsers.ContainsKey(room)) _RoomUsers[room] = new Dictionary<string, HashSet<string>>();
                    if (!_RoomUsers[room].ContainsKey(username)) _RoomUsers[room].Add(username, null);
                }
                    break;
                case "353":
                case "366":
                case "NAMES":
                {
                    //TODO: Implement
                }
                    break;
                case "MODE":
                {
                    var room = data.Params[0];
                    var pType = data.Params[1][0];
                    var pName = data.Params[1][1..];
                    var user = data.Params[2];

                    if (_RoomUsers.ContainsKey(room) && _RoomUsers[room].ContainsKey(user))
                        switch (pType)
                        {
                            case '-':
                                if (_RoomUsers[room][user] == null) break;
                                if (_RoomUsers[room][user].Contains(pName)) _RoomUsers[room][user].Remove(pName);

                                break;
                            case '+':
                                _RoomUsers[room][user] ??= new HashSet<string>();
                                if (!_RoomUsers[room][user].Contains(pName)) _RoomUsers[room][user].Add(pName);
                                break;
                        }
                }
                    break;
                case "PART":
                {
                    if (data.Params.Count == 0) break;
                    var username = data.Prefix.Split('!')[0];
                    var room = data.Params[0];
                    if (data.IsPrefixHostmask && data.Prefix == SelfHostMask)
                    {
                        _roomStates.Remove(room);
                        _RoomUsers.Remove(room);
                    }

                    if (_RoomUsers.ContainsKey(room) && _RoomUsers[room].ContainsKey(username))
                        _RoomUsers[room].Remove(username);
                }
                    break;
                case "PRIVMSG":
                {
                    if (data.Params.Count != 2) break;
                    if (!data.IsPrefixHostmask) break;
                    var msg = new ChatMessage
                    {
                        UserName = data.Prefix.Split('!')[0],
                        Channel = data.Params[0]
                    };
                    var m = data.Params[1];
                    if (m.StartsWith("\u0001ACTION ") && m.EndsWith("\u0001"))
                    {
                        var start = m.IndexOf('\u0001') + 8;
                        msg.Message = m.Substring(start, m.LastIndexOf('\u0001') - start);
                        msg.Action = true;
                    }
                    else
                    {
                        msg.Message = m;
                    }

                    foreach (var t in data.Tags.Where(x => !string.IsNullOrEmpty(x.Value)))
                        switch (t.Key)
                        {
                            case "badges":
                                msg.Badges = ParseBadges(t.Value);
                                break;
                            case "bits":
                                if (int.TryParse(t.Value, out var bits)) msg.Bits = bits;
                                break;
                            case "color":
                                msg.Color = t.Value;
                                break;
                            case "display-name":
                                msg.DisplayName = t.Value;
                                break;
                            case "emotes":
                                msg.Emotes = ParseEmotes(t.Value);
                                break;
                            case "flags":
                                msg.Flags = t.Value;
                                break;
                            case "id":
                                msg.Id = t.Value;
                                break;
                            case "mod":
                                msg.Mod = t.Value != "0";
                                break;
                            case "room-id":
                                msg.RoomId = t.Value;
                                break;
                            case "subscriber":
                                msg.Subscriber = t.Value != "0";
                                break;
                            case "tmi-sent-ts":
                                msg.SentTimestamp = t.Value;
                                break;
                            case "turbo":
                                msg.Turbo = t.Value != "0";
                                break;
                            case "user-id":
                                msg.UserId = t.Value;
                                break;
                            case "user-type":
                                msg.UserType = t.Value;
                                break;
                            case "emote-only":
                                msg.EmoteOnly = t.Value != "0";
                                break;
                            case "badge-info":
                                msg.BadgeInfo = t.Value;
                                break;
                            default:
                                if(msg.ExtraTags == null) msg.ExtraTags = new Dictionary<string, string>();
                                msg.ExtraTags[t.Key] = t.Value;
                                OnUnsupportedTag?.Invoke(this, $"{data.Command}:{t.Key}:{t.Value}");
                                break;
                        }
                    
                    OnMessage?.Invoke(this, msg);
                }
                    break;
                case "GLOBALUSERSTATE":
                {
                    var state = new GlobalUserState();
                    foreach (var t in data.Tags)
                        switch (t.Key)
                        {
                            case "badges":
                                state.Badges = ParseBadges(t.Value);
                                break;
                            case "color":
                                state.Color = t.Value;
                                break;
                            case "display-name":
                                state.DisplayName = t.Value;
                                break;
                            case "emote-sets":
                                state.EmoteSets = t.Value.Split(',');
                                break;
                            case "user-id":
                                state.UserId = t.Value;
                                break;
                            case "user-type":
                                state.UserType = t.Value;
                                break;
                            case "badge-info":
                                state.BadgeInfo = t.Value;
                                break;
                            default:
                                if (state.ExtraTags == null) state.ExtraTags = new Dictionary<string, string>();
                                state.ExtraTags[t.Key] = t.Value;
                                OnUnsupportedTag?.Invoke(this, $"{data.Command}:{t.Key}:{t.Value}");
                                break;
                        }

                    UserState = state;
                    OnGlobalUserState?.Invoke(this, state);
                }
                    break;
                case "ROOMSTATE":
                {
                    if (data.Params.Count == 0) break;
                    var rs = new RoomState {Room = data.Params[0]};
                    foreach (var t in data.Tags)
                        switch (t.Key)
                        {
                            case "broadcaster-lang":
                                rs.BroadcasterLanguage = t.Value;
                                break;
                            case "emote-only":
                                rs.EmoteOnly = t.Value != "0";
                                break;
                            case "r9k":
                                rs.R9K = t.Value != "0";
                                break;
                            case "slow":
                                rs.Slow = t.Value != "0";
                                break;
                            case "subs-only":
                                rs.SubsOnly = t.Value != "0";
                                break;
                            case "rituals":
                                rs.Rituals = t.Value;
                                break;
                            case "room-id":
                                rs.RoomId = t.Value;
                                break;
                            case "followers-only":
                                rs.FollowersOnly = t.Value;
                                break;
                            default:
                                if (rs.ExtraTags == null) rs.ExtraTags = new Dictionary<string, string>();
                                rs.ExtraTags[t.Key] = t.Value;
                                OnUnsupportedTag?.Invoke(this, $"{data.Command}:{t.Key}:{t.Value}");
                                break;
                        }

                    _roomStates[rs.Room] = rs;
                    OnRoomState?.Invoke(this, rs);
                }
                    break;
                case "RECONNECT":
                    //TODO: Implement
                    break;
                case "USERSTATE":
                    //TODO: Implement
                    break;
                case "CLEARCHAT":
                {
                    var b = new UserBan {Channel = data.Params[0]};
                    if (data.Params.Count > 1) b.UserName = data.Params[1];
                    foreach (var t in data.Tags)
                        switch (t.Key)
                        {
                            case "ban-reason":
                                b.Reason = t.Value;
                                break;
                            case "ban-duration":
                                long.TryParse(t.Value, out var duration);
                                b.Duration = duration;
                                break;
                            case "room-id":
                                b.RoomId = t.Value;
                                break;
                            case "target-user-id":
                                b.UserId = t.Value;
                                break;
                            case "tmi-sent-ts":
                                b.Timestamp = t.Value;
                                break;
                            default:
                                OnUnsupportedTag?.Invoke(this, $"{data.Command}:{t.Key}:{t.Value}");
                                break;
                        }
                    OnUserBan?.Invoke(this, b);
                }
                    break;
                case "USERNOTICE":
                {
                    if (data.Params.Count == 0) break;
                    var s = new SubscribeData {Channel = data.Params[0]};
                    if (data.Params.Count > 1) s.Message = data.Params[1];
                    foreach (var t in data.Tags)
                        switch (t.Key)
                        {
                            case "badges":
                                s.Badges = ParseBadges(t.Value);
                                break;
                            case "color":
                                s.Color = t.Value;
                                break;
                            case "display-name":
                                s.DisplayName = t.Value;
                                break;
                            case "emotes":
                                s.Emotes = ParseEmotes(t.Value);
                                break;
                            case "flags":
                                s.Flags = t.Value;
                                break;
                            case "id":
                                s.Id = t.Value;
                                break;
                            case "login":
                                s.Login = t.Value;
                                break;
                            case "mod":
                                s.Mod = t.Value != "0";
                                break;
                            case "msg-id":
                                s.MsgId = t.Value;
                                break;
                            case "msg-param-months":
                                s.MsgParamMonths = t.Value;
                                break;
                            case "msg-param-sub-plan-name":
                                s.MsgParamSubPlanName = t.Value;
                                break;
                            case "msg-param-sub-plan":
                                s.MsgParamSubPlan = t.Value;
                                break;
                            case "room-id":
                                s.RoomId = t.Value;
                                break;
                            case "subscriber":
                                s.Subscriber = t.Value != "0";
                                break;
                            case "system-msg":
                                s.SystemMsg = t.Value;
                                break;
                            case "tmi-sent-ts":
                                s.Timestamp = t.Value;
                                break;
                            case "turbo":
                                s.Turbo = t.Value != "0";
                                break;
                            case "user-id":
                                s.UserId = t.Value;
                                break;
                            case "user-type":
                                s.UserType = t.Value;
                                break;
                            case "msg-param-recipient-display-name":
                                s.MsgParamRecipientDisplayName = t.Value;
                                break;
                            case "msg-param-recipient-id":
                                s.MsgParamRecipientId = t.Value;
                                break;
                            case "msg-param-recipient-user-name":
                                s.MsgParamRecipientUserName = t.Value;
                                break;
                            case "msg-param-sender-count":
                                int.TryParse(t.Value, out var giftcount);
                                s.MsgParamSenderCount = giftcount;
                                break;
                            case "msg-param-promo-gift-total":
                                int.TryParse(t.Value, out var gifttotal);
                                s.MsgParamPromoGiftTotal = gifttotal;
                                break;
                            case "msg-param-promo-name":
                                s.MsgParamPromoName = t.Value;
                                break;
                            case "msg-param-sender-login":
                                s.MsgParamSenderLogin = t.Value;
                                break;
                            case "msg-param-sender-name":
                                s.MsgParamSenderName = t.Value;
                                break;
                            case "msg-param-mass-gift-count":
                                int.TryParse(t.Value, out var masscount);
                                s.MsgParamMassGiftCount = masscount;
                                break;
                            case "msg-param-origin-id":
                                s.MsgParamOriginId = t.Value;
                                break;
                            case "msg-param-cumulative-months":
                                int.TryParse(t.Value, out var cumulativeMonths);
                                s.MsgParamCumulativeMonths = cumulativeMonths;
                                break;
                            case "msg-param-should-share-streak":
                                int.TryParse(t.Value, out var sharestreak);
                                s.MsgParamShouldShareStreak = sharestreak;
                                break;
                            case "msg-param-streak-months":
                                int.TryParse(t.Value, out var streakmonths);
                                s.MsgParamStreakMonths = streakmonths;
                                break;
                            case "badge-info":
                                s.BadgeInfo = t.Value;
                                break;
                            case "msg-param-displayName":
                                s.MsgParamDisplayName = t.Value;
                                break;
                            case "msg-param-login":
                                s.MsgParamLogin = t.Value;
                                break;
                            case "msg-param-profileImageURL":
                                s.MsgParamProfileImageURL = t.Value;
                                break;
                            case "msg-param-viewerCount":
                                int.TryParse(t.Value, out var viewerCount);
                                s.MsgParamViewerCount = viewerCount;
                                break;
                            default:
                                if (s.ExtraTags == null) s.ExtraTags = new Dictionary<string, string>();
                                s.ExtraTags[t.Key] = t.Value;
                                OnUnsupportedTag?.Invoke(this, $"{data.Command}:{t.Key}:{t.Value}");
                                break;
                        }


                    OnUserSubscribe?.Invoke(this, s);
                }
                    break;
            }
        }

        private static Badge[] ParseBadges(string value)
        {
            return value.Split(',').Select(b => b.Split('/')).Where(s => s.Length == 2).Select(s =>
            {
                int.TryParse(s[1], out var result);
                return new Badge(s[0], result);
            }).ToArray();
        }

        public static Dictionary<string, ChatEmotePos[]> ParseEmotes(string value)
        {
            var result = new Dictionary<string, ChatEmotePos[]>();
            foreach (var e in value.Split('/'))
            {
                var emotePoses = new List<ChatEmotePos>();
                var eparts = e.Split(':');
                var emoteId = eparts[0];
                if (eparts.Length != 2) break;

                foreach (var pos in eparts[1].Split(','))
                {
                    var posParts = pos.Split('-');
                    if (posParts.Length != 2) break;
                    if (int.TryParse(posParts[0], out var start) && int.TryParse(posParts[1], out var end))
                        emotePoses.Add(new ChatEmotePos(start, end));
                }

                result[emoteId] = emotePoses.ToArray();
            }

            return result;
        }

        public class RoomState : EventArgs
        {
            public string Room { get; internal set; }
            public string BroadcasterLanguage { get; internal set; }
            public bool EmoteOnly { get; internal set; }
            public bool R9K { get; internal set; }
            public bool Slow { get; internal set; }
            public bool SubsOnly { get; internal set; }
            public string Rituals { get; internal set; }
            public string RoomId { get; internal set; }
            public string FollowersOnly { get; internal set; }
            public Dictionary<string, string> ExtraTags { get; internal set; }
        }


        public struct Badge
        {
            public string Name { get; }
            public int Version { get; }

            public Badge(string name, int version)
            {
                Name = name;
                Version = version;
            }
        }

        public class GlobalUserState : EventArgs
        {
            public Badge[] Badges { get; internal set; }
            public string Color { get; internal set; }
            public string DisplayName { get; internal set; }
            public string[] EmoteSets { get; internal set; }
            public string UserId { get; internal set; }
            public string UserType { get; internal set; }
            public string BadgeInfo { get; internal set; }
            public Dictionary<string, string> ExtraTags { get; internal set; }
        }

        public struct ChatEmotePos
        {
            public int Start { get; }
            public int End { get; }

            public ChatEmotePos(int start, int end)
            {
                Start = start;
                End = end;
            }
        }

        public class ChatMessage : EventArgs, IRepliable
        {
            public string UserName { get; internal set; }
            public string Message { get; internal set; }
            public bool Action { get; internal set; }
            public Badge[] Badges { get; internal set; }
            public int? Bits { get; internal set; }
            public string Color { get; internal set; }
            public string DisplayName { get; internal set; }
            public Dictionary<string, ChatEmotePos[]> Emotes { get; internal set; }
            public string Flags { get; internal set; }
            public string Id { get; internal set; }
            public bool Mod { get; internal set; }
            public string RoomId { get; internal set; }
            public bool Subscriber { get; internal set; }
            public string SentTimestamp { get; internal set; }
            public bool Turbo { get; internal set; }
            public string UserId { get; internal set; }
            public string UserType { get; internal set; }
            public bool EmoteOnly { get; internal set; }
            public string BadgeInfo { get; internal set; }
            public string Channel { get; internal set; }
            public Dictionary<string, string> ExtraTags { get; internal set; }
        }

        public class UserBan : EventArgs, IRepliable
        {
            public string UserName { get; internal set; }
            public string Reason { get; internal set; }
            public long Duration { get; internal set; }
            public string RoomId { get; internal set; }
            public string UserId { get; internal set; }
            public string Timestamp { get; internal set; }
            public string Channel { get; internal set; }
        }


        public class SubscribeData : EventArgs, IRepliable
        {
            public Badge[] Badges { get; internal set; }
            public bool Mod { get; internal set; }
            public bool Subscriber { get; internal set; }
            public bool Turbo { get; internal set; }
            public Dictionary<string, ChatEmotePos[]> Emotes { get; internal set; }
            public string Color { get; internal set; }
            public string DisplayName { get; internal set; }
            public string Flags { get; internal set; }
            public string Id { get; internal set; }
            public string Login { get; internal set; }
            public string Message { get; internal set; }
            public string MsgId { get; internal set; }
            public string MsgParamMonths { get; internal set; }
            public string MsgParamPromoName { get; internal set; }
            public string MsgParamRecipientDisplayName { get; internal set; }
            public string MsgParamRecipientId { get; internal set; }
            public string MsgParamRecipientUserName { get; internal set; }
            public string MsgParamSenderLogin { get; internal set; }
            public string MsgParamSenderName { get; internal set; }
            public string MsgParamSubPlan { get; internal set; }
            public string MsgParamSubPlanName { get; internal set; }
            public string MsgParamOriginId { get; internal set; }
            public int MsgParamCumulativeMonths { get; internal set; }
            public int MsgParamShouldShareStreak { get; internal set; }
            public int MsgParamStreakMonths { get; internal set; }
            public int? MsgParamMassGiftCount { get; internal set; }
            public int? MsgParamPromoGiftTotal { get; internal set; }
            public int? MsgParamSenderCount { get; internal set; }
            public string RoomId { get; internal set; }
            public string SystemMsg { get; internal set; }
            public string Timestamp { get; internal set; }
            public string UserId { get; internal set; }
            public string UserType { get; internal set; }
            public string BadgeInfo { get; internal set; }
            public string MsgParamDisplayName { get; internal set; }
            public string MsgParamLogin { get; internal set; }
            public string MsgParamProfileImageURL { get; internal set; }
            public int MsgParamViewerCount { get; internal set; }
            public string Channel { get; internal set; }
            public Dictionary<string, string> ExtraTags { get; internal set; }
        }
    }
}