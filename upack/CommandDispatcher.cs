﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Inedo.ProGet.UPack
{
    public sealed class CommandDispatcher
    {
        public static CommandDispatcher Default => new CommandDispatcher(typeof(Pack), typeof(Push), typeof(Unpack), typeof(Install));

        private readonly IEnumerable<Type> commands;

        public CommandDispatcher(params Type[] commands)
        {
            this.commands = commands;
        }

        public void Main(string[] args)
        {
            bool first = true;
            bool onlyPositional = false;
            bool hadError = false;

            var positional = new List<string>();
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var arg in args)
            {
                if (first)
                {
                    first = false;
                    continue;
                }

                if (onlyPositional || !arg.StartsWith("--"))
                {
                    positional.Add(arg);
                }
                else if (arg == "--")
                {
                    onlyPositional = true;
                    continue;
                }
                else
                {
                    var parts = arg.Substring("--".Length).Split(new[] { '=' }, 2);
                    if (extra.ContainsKey(parts[0]))
                    {
                        hadError = true;
                    }

                    extra[parts[0]] = parts.Length == 1 ? null : parts[1];
                }
            }

            Command cmd = null;
            if (positional.Count == 0)
            {
                hadError = true;
            }
            else
            {
                foreach (var command in commands)
                {
                    cmd = (Command)command.GetConstructor(null).Invoke(null);
                    if (cmd.DisplayName.Equals(positional[0], StringComparison.OrdinalIgnoreCase))
                    {
                        if (hadError)
                        {
                            break;
                        }

                        positional.RemoveAt(0);

                        foreach (var arg in cmd.PositionalArguments)
                        {
                            if (arg.Index < positional.Count)
                            {
                                if (!arg.TrySetValue(cmd, positional[arg.Index]))
                                {
                                    hadError = true;
                                }
                            }
                            else if (!arg.Optional)
                            {
                                hadError = true;
                            }
                        }

                        if (positional.Count > cmd.PositionalArguments.Count())
                        {
                            hadError = true;
                        }

                        foreach (var arg in cmd.ExtraArguments)
                        {
                            if (extra.ContainsKey(arg.DisplayName))
                            {
                                if (!arg.TrySetValue(cmd, extra[arg.DisplayName]))
                                {
                                    hadError = true;
                                }
                                extra.Remove(arg.DisplayName);
                            }
                            else if (!arg.Optional)
                            {
                                hadError = true;
                            }
                        }

                        if (extra.Count != 0)
                        {
                            hadError = true;
                        }

                        break;
                    }
                }
            }

            if (hadError)
            {
                if (cmd != null)
                {
                    ShowHelp(cmd);
                }
                else
                {
                    ShowGenericHelp();
                }
                Environment.ExitCode = 1;
            }
            else
            {
                Environment.ExitCode = cmd.RunAsync().GetAwaiter().GetResult();
            }
        }

        public void ShowGenericHelp()
        {
            Console.WriteLine("Usage: upack <<command>>");
            Console.WriteLine();

            foreach (var command in commands)
            {
                Console.WriteLine($"{command.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? command.Name} - {command.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty}");
            }
        }

        public void ShowHelp(Command cmd)
        {
            Console.WriteLine(cmd.GetHelp());
        }
    }
}