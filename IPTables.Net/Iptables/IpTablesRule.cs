﻿using System;
using System.Collections.Generic;
using System.Linq;
using IPTables.Net.Common;
using IPTables.Net.Iptables.Modules;

namespace IPTables.Net.Iptables
{
    public class IpTablesRule : IEquatable<IpTablesRule>
    {
        private readonly OrderedDictionary<String, IIpTablesModuleGod> _modules = new OrderedDictionary<String, IIpTablesModuleGod>();
        protected internal readonly NetfilterSystem _system;
        public long Bytes = 0;
        public IpTablesChain Chain;
        public long Packets = 0;

        public IpTablesRule(NetfilterSystem system, IpTablesChain chain)
        {
            _system = system;
            Chain = chain;
        }

        public String Table
        {
            get { return Chain.Table; }
        }

        public String ChainName
        {
            get { return Chain.Name; }
        }

        public int Position
        {
            get { return Chain.Rules.IndexOf(this) + 1; }
        }

        internal NetfilterSystem System
        {
            get { return _system; }
        }

        internal OrderedDictionary<String, IIpTablesModuleGod> ModulesInternal
        {
            get { return _modules; }
        }

        public IEnumerable<IIpTablesModule> Modules
        {
            get { return _modules.Values.Select(a => a as IIpTablesModule); }
        }

        public bool Equals(IpTablesRule rule)
        {
            return _modules.DictionaryEqual(rule.ModulesInternal);
        }

        public override bool Equals(object obj)
        {
            if (obj is IpTablesRule)
            {
                return Equals(obj as IpTablesRule);
            }
            return base.Equals(obj);
        }


        public String GetCommand()
        {
            String command = "";
            if (Table != "filter")
            {
                command += "-t " + Table;
            }

            foreach (var e in _modules)
            {
                if (command.Length != 0)
                {
                    command += " ";
                }
                if (e.Value.NeedsLoading)
                {
                    command += "-m " + e.Key + " ";
                }
                command += e.Value.GetRuleString();
            }
            return command;
        }

        public String GetFullCommand(String opt = "-A")
        {
            String command = opt + " " + Chain.Name + " ";
            if (opt == "-R")
            {
                if (Position == -1)
                {
                    throw new Exception(
                        "This rule does not have a specific position and hence can not be located for replace");
                }
                command += Position + " ";
            }
            else if (opt == "-I")
            {
                //Posotion not specified, insert at top
                if (Position != -1)
                {
                    command += Position + " ";
                }
            }
            command += GetCommand();
            return command;
        }

        public void Add()
        {
            _system.Adapter.AddRule(this);
        }

        public void Delete(bool usingPosition = true)
        {
            if (usingPosition)
            {
                _system.Adapter.DeleteRule(Table, ChainName, Position);
            }
            else
            {
                _system.Adapter.DeleteRule(this);
            }
            Chain.Rules.Remove(this);
        }

        internal IIpTablesModuleGod GetModuleForParseInternal(string name, Type moduleType)
        {
            if (_modules.ContainsKey(name))
            {
                return _modules[name];
            }

            var module = (IIpTablesModuleGod) Activator.CreateInstance(moduleType);
            _modules.Add(name, module);
            return module;
        }

        public IIpTablesModule GetModuleForParse(string name, Type moduleType)
        {
            return GetModuleForParseInternal(name, moduleType);
        }

        public static string[] SplitArguments(string commandLine)
        {
            char[] parmChars = commandLine.ToCharArray();
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            for (int index = 0; index < parmChars.Length; index++)
            {
                if (parmChars[index] == '"' && !inSingleQuote)
                {
                    inDoubleQuote = !inDoubleQuote;
                    parmChars[index] = '\n';
                }
                if (parmChars[index] == '\'' && !inDoubleQuote)
                {
                    inSingleQuote = !inSingleQuote;
                    parmChars[index] = '\n';
                }
                if (!inSingleQuote && !inDoubleQuote && parmChars[index] == ' ')
                    parmChars[index] = '\n';
            }
            return (new string(parmChars)).Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
        }

        public static IpTablesRule Parse(String rule, NetfilterSystem system, IpTablesChainSet chains,
            String defaultTable = "filter")
        {
            string[] arguments = SplitArguments(rule);
            int count = arguments.Length;
            var ipRule = new IpTablesRule(system, null);
            var parser = new RuleParser(arguments, ipRule, chains, defaultTable);

            bool not = false;
            for (int i = 0; i < count; i++)
            {
                if (arguments[i] == "!")
                {
                    not = true;
                    continue;
                }
                i += parser.FeedToSkip(i, not);
                not = false;
            }

            ipRule.Chain = parser.GetChain(system);

            return ipRule;
        }

        public T GetModule<T>(string moduleName) where T : class, IIpTablesModule
        {
            if (!_modules.ContainsKey(moduleName)) return null;
            return _modules[moduleName] as T;
        }

        public T GetModuleOrLoad<T>(string moduleName) where T : class, IIpTablesModule
        {
            return GetModuleForParse(moduleName, typeof (T)) as T;
        }

        public void Replace(IpTablesRule withRule)
        {
            int idx = Chain.Rules.IndexOf(this);
            _system.Adapter.ReplaceRule(withRule);
            Chain.Rules[idx] = withRule;
        }
    }
}