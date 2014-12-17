﻿using System;
using System.Collections.Generic;
using System.Data;
using IPTables.Net.Iptables.DataTypes;
using IPTables.Net.Iptables.Modules.Comment;
using IPTables.Net.Iptables.Modules.Core;
using IPTables.Net.Iptables.Modules.Multiport;
using IPTables.Net.Iptables.Modules.Tcp;
using IPTables.Net.Iptables.Modules.Udp;

namespace IPTables.Net.Iptables.RuleGenerator
{
    public class MultiportAggregator<TKey> : IRuleGenerator
    {
        private String _chain;
        private String _table;
        private Dictionary<TKey, List<IpTablesRule>> _rules = new Dictionary<TKey, List<IpTablesRule>>();
        private Func<IpTablesRule, TKey> _extractKey;
        private Func<IpTablesRule, PortOrRange> _extractPort;
        private Action<IpTablesRule, List<PortOrRange>> _setPort;
        private string _commentPrefix;
        private Action<IpTablesRule, TKey> _setJump;
        private string _baseRule;

        public MultiportAggregator(String chain, String table, Func<IpTablesRule, TKey> extractKey, 
            Func<IpTablesRule, PortOrRange> extractPort, Action<IpTablesRule, List<PortOrRange>> setPort, 
            Action<IpTablesRule, TKey> setJump, String commentPrefix,
            String baseRule = null)
        {
            _chain = chain;
            _table = table;
            _extractKey = extractKey;
            _extractPort = extractPort;
            _setPort = setPort;
            _setJump = setJump;
            _commentPrefix = commentPrefix;
            if (baseRule == null)
            {
                baseRule = "-A "+chain+" -t "+table;
            }
            _baseRule = baseRule;
        }

        private List<PortOrRange> GetRanged(IEnumerable<PortOrRange> ranges)
        {
            List<PortOrRange> ret = new List<PortOrRange>();
            PortOrRange start = new PortOrRange(0);
            int previous = -1;
            foreach (PortOrRange current in ranges)
            {
                if (current.LowerPort == (previous + 1))
                {
                    if (start.LowerPort == 0)
                    {
                        start = new PortOrRange((uint)previous, current.UpperPort);
                    }
                }
                else
                {
                    if (start.UpperPort != 0)
                    {
                        ret.Add(new PortOrRange(start.LowerPort, (uint)previous));
                        start = new PortOrRange(0);
                    }
                    else if (previous != -1)
                    {
                        ret.Add(new PortOrRange((uint)previous));
                    }
                }
                previous = (int)current.UpperPort;
            }
            if (start.UpperPort != 0)
            {
                ret.Add(new PortOrRange(start.LowerPort, (uint)previous));
                // ReSharper disable RedundantAssignment
                start = new PortOrRange(0);
                // ReSharper restore RedundantAssignment
            }
            else if (previous != -1)
            {
                ret.Add(new PortOrRange((uint)previous));
            }
            return ret;
        }

        public static void DestinationPortSetter(IpTablesRule rule, List<PortOrRange> ranges)
        {
            var protocol = rule.GetModule<CoreModule>("core").Protocol;
            if (ranges.Count == 1 && !protocol.Null && !protocol.Not)
            {
                if (protocol.Value == "tcp")
                {
                    var tcp = rule.GetModuleOrLoad<TcpModule>("tcp");
                    tcp.DestinationPort = new ValueOrNot<PortOrRange>(ranges[0]);
                }
                else
                {
                    var tcp = rule.GetModuleOrLoad<UdpModule>("udp");
                    tcp.DestinationPort = new ValueOrNot<PortOrRange>(ranges[0]);
                }
            }
            else
            {
                var multiport = rule.GetModuleOrLoad<MultiportModule>("multiport");
                multiport.DestinationPorts = new ValueOrNot<IEnumerable<PortOrRange>>(ranges);
            }
        }

        public static void SourcePortSetter(IpTablesRule rule, List<PortOrRange> ranges)
        {
            var protocol = rule.GetModule<CoreModule>("core").Protocol;
            if (ranges.Count == 1 && !protocol.Null && !protocol.Not)
            {
                if (protocol.Value == "tcp")
                {
                    var tcp = rule.GetModuleOrLoad<TcpModule>("tcp");
                    tcp.SourcePort = new ValueOrNot<PortOrRange>(ranges[0]);
                }
                else
                {
                    var tcp = rule.GetModuleOrLoad<UdpModule>("udp");
                    tcp.SourcePort = new ValueOrNot<PortOrRange>(ranges[0]);
                }
            }
            else
            {
                var multiport = rule.GetModuleOrLoad<MultiportModule>("multiport");
                multiport.SourcePorts = new ValueOrNot<IEnumerable<PortOrRange>>(ranges);
            }
        }

        private IpTablesRule OutputRulesForGroup(IpTablesRuleSet ruleSet, IpTablesSystem system, List<IpTablesRule> rules, string chainName)
        {
            if (rules.Count == 0)
            {
                return null;
            }

            int count = 0, ruleCount = 0;
            List<PortOrRange> ranges = new List<PortOrRange>();
            IpTablesRule rule1 = null;
            var firstCore = rules[0].GetModule<CoreModule>("core");
            int ruleIdx = 1;

            Action buildRule = () =>
            {
                if (ranges.Count == 0)
                {
                    throw new Exception("this should not happen");
                }

                rule1 = IpTablesRule.Parse(_baseRule, system, ruleSet.ChainSet);
                var ruleCore = rule1.GetModuleOrLoad<CoreModule>("core");
                ruleCore.Protocol = firstCore.Protocol;
                if (firstCore.TargetMode == TargetMode.Goto && !String.IsNullOrEmpty(firstCore.Goto))
                {
                    ruleCore.Goto = firstCore.Goto;
                }
                else if (firstCore.TargetMode == TargetMode.Jump && !String.IsNullOrEmpty(firstCore.Jump))
                {
                    ruleCore.Jump = firstCore.Jump;
                }
                var ruleComment = rule1.GetModuleOrLoad<CommentModule>("comment");
                ruleComment.CommentText = _commentPrefix + "|" + chainName + "|" + ruleIdx;
                if (ruleCount == 0)
                {
                    rule1.Chain = ruleSet.ChainSet.GetChainOrDefault(_chain, _table);
                }
                else
                {
                    rule1.Chain = ruleSet.ChainSet.GetChainOrDefault(chainName, _table);
                }
                _setPort(rule1, new List<PortOrRange>(ranges));
                ruleSet.AddRule(rule1);
                ruleIdx++;
            };

            List<PortOrRange> exceptions = new List<PortOrRange>();
            foreach (var rule in rules)
            {
                exceptions.Add(_extractPort(rule));
            }

            exceptions.Sort((a, b) =>
            {
                if (a.IsRange() && b.IsRange() || !a.IsRange() && !b.IsRange())
                {
                    if (a.LowerPort < b.LowerPort)
                    {
                        return -1;
                    }
                    return 1;
                }
                if (a.IsRange()) return -1;
                return 1;
            });

            exceptions = GetRanged(exceptions);

            for (var i=0;i<exceptions.Count;i++)
            {
                var e = exceptions[i];
                if (e.IsRange())
                {
                    count += 2;
                }
                else
                {
                    count++;
                }


                if (i + 1 < exceptions.Count)
                {
                    if (count == 14 && exceptions[i+1].IsRange())
                    {
                        ruleCount++;
                        continue;
                    }
                }

                if (count == 15)
                {
                    ruleCount++;
                }
            }
            count = 0;

            foreach (var e in exceptions)
            {
                if (count == 14 && e.IsRange() || count == 15)
                {
                    buildRule();
                    count = 0;
                    ranges.Clear();
                }
                ranges.Add(e);

                if (e.IsRange())
                {
                    count += 2;
                }
                else
                {
                    count++;
                }
            }

            buildRule();

            if (ruleCount != 0)
            {
                return null;
            }

            return rule1;
        }

        public void AddRule(IpTablesRule rule)
        {
            var key = _extractKey(rule);
            if (!_rules.ContainsKey(key))
            {
                _rules.Add(key, new List<IpTablesRule>());
            }
            _rules[key].Add(rule);
        }

        public void Output(IpTablesSystem system, IpTablesRuleSet ruleSet)
        {
            foreach (var p in _rules)
            {
                String chainName = _chain + "_" + p.Key;
                if (ruleSet.ChainSet.HasChain(chainName, _table))
                {
                    throw new Exception(String.Format("Duplicate feature split: {0}", chainName));
                }

                //Jump to chain
                var chain = ruleSet.ChainSet.GetChainOrAdd(chainName, _table, system);

                //Nested output
                var singleRule = OutputRulesForGroup(ruleSet, system, p.Value, chainName);
                if (singleRule == null)
                {
                    if (chain.Rules.Count != 0)
                    {
                        IpTablesRule jumpRule = IpTablesRule.Parse(_baseRule, system, ruleSet.ChainSet);
                        _setJump(jumpRule, p.Key);
                        jumpRule.GetModuleOrLoad<CoreModule>("core").Jump = chainName;
                        jumpRule.GetModuleOrLoad<CommentModule>("comment").CommentText = _commentPrefix + "|MA|" +
                                                                                         chainName;
                        ruleSet.AddRule(jumpRule);
                    }
                }
                else
                {
                    _setJump(singleRule, p.Key);
                }
                if(chain.Rules.Count == 0)
                {
                    ruleSet.ChainSet.Chains.Remove(chain);
                }
            }
        }
    }
}