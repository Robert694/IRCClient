using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IRCClient
{
    //https://ircv3.net/specs/core/message-tags-3.2.html
    public class IrcMessage
    {
        private static readonly Regex HostmaskRegex = new Regex("[!@]", RegexOptions.Compiled);

        public string Command { get; set; } = string.Empty;

        public bool IsPrefixHostmask => !string.IsNullOrWhiteSpace(Prefix)
                                        && Prefix.Contains("@")
                                        && Prefix.Contains("!");

        public bool IsPrefixServer => !string.IsNullOrWhiteSpace(Prefix)
                                      && !IsPrefixHostmask
                                      && Prefix.Contains(".");

        public List<string> Params { get; } = new List<string>();
        public string Prefix { get; set; } = string.Empty;
        public IDictionary<string, string> Tags { get; } = new Dictionary<string, string>();


        public IrcMessage SetCommand(string command)
        {
            Command = command;
            return this;
        }

        public IrcMessage SetPrefix(string prefix)
        {
            Prefix = prefix;
            return this;
        }

        public IrcMessage AddParam(IEnumerable<string> param)
        {
            Params.AddRange(param);
            return this;
        }

        public IrcMessage AddParam(params string[] param)
        {
            Params.AddRange(param);
            return this;
        }

        public IrcMessage AddMsgParam(string param)
        {
            Params.Add($":{param}");
            return this;
        }


        public IrcMessage AddTag(string key, string value)
        {
            Tags[key] = value;
            return this;
        }

        public IrcMessage AddTag(params (string Key, string Value)[] tag)
        {
            foreach (var v in tag)
            {
                Tags[v.Key] = v.Value;
            }
            return this;
        }

        public static IrcMessage Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new FormatException("Invalid IRC message: message is empty");
            }

            var message = new IrcMessage();
            int nextspace;
            var position = 0;

            // The first thing we check for is IRCv3.2 message tags.
            // http://ircv3.atheme.org/specification/message-tags-3.2
            if (line[0] == '@')
            {
                nextspace = line.IndexOf(' ');
                if (nextspace == -1)
                {
                    throw new FormatException("Invalid IRC message: malformed message parsing tags");
                }

                var rawTags = line.Substring(1, nextspace - 1).Split(';');
                foreach (var pair in rawTags.Select(tag => tag.Split('=')))
                {
                    message.Tags[pair[0]] = pair.Length > 1 ? UnEscapeTagValue(pair[1]) : null;
                }
                position = nextspace + 1;
            }

            position = SkipSpaces(line, position);

            // Extract the message's prefix if present. Prefixes are prepended
            // with a colon.
            if (line[position] == ':')
            {
                nextspace = line.IndexOf(' ', position);
                if (nextspace == -1)
                {
                    throw new FormatException("Invalid IRC message: malformed message parsing prefix");
                }

                message.Prefix = line.Substring(position + 1, nextspace - position - 1);
                position = nextspace + 1;
                position = SkipSpaces(line, position);
            }

            // If there's no more whitespace left, extract everything from the
            // current position to the end of the string as the command.
            nextspace = line.IndexOf(' ', position);
            if (nextspace == -1)
            {
                if (line.Length > position)
                {
                    message.Command = line.Substring(position);
                }

                return message;
            }

            // Else, the command is the current position up to the next space. After
            // that, we expect some parameters.
            message.Command = line.Substring(position, nextspace - position);
            position = nextspace + 1;
            position = SkipSpaces(line, position);

            while (position < line.Length)
            {
                nextspace = line.IndexOf(' ', position);

                // If the character is a colon, we've got a trailing parameter.
                // At this point, there are no extra params, so we push everything
                // from after the colon to the end of the string, to the params array
                // and break out of the loop.
                if (line[position] == ':')
                {
                    message.Params.Add(line.Substring(position + 1));
                    break;
                }

                // If we still have some whitespace...
                if (nextspace != -1)
                {
                    // Push whatever's between the current position and the next
                    // space to the params array.
                    message.Params.Add(line.Substring(position, nextspace - position));
                    position = nextspace + 1;
                    // Skip any trailing whitespace and continue looping.
                    position = SkipSpaces(line, position);
                    continue;
                }

                // If we don't have any more whitespace and the param isn't trailing,
                // push everything remaining to the params array.
                if (nextspace != -1)
                {
                    continue;
                }

                message.Params.Add(line.Substring(position));
                break;
            }

            return message;
        }

        public static string UnEscapeTagValue(string value)
        {
            value = value.Replace(@"\:", ";");
            value = value.Replace(@"\r", "\r");
            value = value.Replace(@"\n", "\n");
            value = value.Replace(@"\s", " ");
            value = value.Replace(@"\\", @"\");
            return value;
        }

        public static string EscapeTagValue(string value)
        {
            value = value.Replace(";", @"\:");
            value = value.Replace("\r", @"\r");
            value = value.Replace("\n", @"\n");
            value = value.Replace(" ", @"\s");
            value = value.Replace(@"\", @"\\");
            return value;
        }

        public static bool TryParse(string line, out IrcMessage message)
        {
            message = null;
            try
            {
                message = Parse(line);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Hostmask GetHostmaskFromPrefix()
        {
            if (!IsPrefixHostmask)
            {
                return null;
            }

            var parts = HostmaskRegex.Split(Prefix);
            return new Hostmask
            {
                Nickname = parts[0],
                Username = parts[1],
                Hostname = parts[2]
            };
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Command))
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (Tags.Count > 0)
            {
                var tags = string.Join(";", Tags.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key)).Select(kvp =>
                    string.IsNullOrWhiteSpace(kvp.Value)
                        ? kvp.Key.Trim()
                        : $"{kvp.Key.Trim()}={EscapeTagValue(kvp.Value)}"));
                parts.Add($"@{tags}");
            }

            if (!string.IsNullOrWhiteSpace(Prefix))
            {
                parts.Add($":{Prefix.Trim()}");
            }

            parts.Add(Command.Trim());

            if (Params.Count > 0)
            {
                var processedParams = Params.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
                // We have at least one parameter that isn't blank or empty
                if (processedParams.Count > 0)
                {
                    var lastHasSpaces = processedParams.Last().IndexOf(' ') != -1;
                    parts.AddRange(
                        processedParams.Take(processedParams.Count - (lastHasSpaces ? 1 : 0)).SelectMany(p => p.IndexOf(' ') == -1
                            ? new[] {p}
                            : p.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)))
                    );

                    if (lastHasSpaces)
                    {
                        parts.Add($":{processedParams.Last()}");
                    }
                }
            }

            return string.Join(" ", parts);
        }

        private static int SkipSpaces(string text, int position)
        {
            while (position < text.Length && text[position] == ' ')
            {
                position++;
            }

            return position;
        }

        public class Hostmask
        {
            public string Hostname { get; set; }
            public string Nickname { get; set; }
            public string Username { get; set; }
        }
    }
}