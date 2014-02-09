﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemInteract;
using IPTables.Net.Iptables.Modules.Core;

namespace IPTables.Net.Iptables
{
    public static class ControlFlowRuleHelper
    {
        public static IpTablesRule CreateJump(String chain, String table, ISystemFactory system)
        {
            IpTablesRule rule = new IpTablesRule(system);
            rule.GetModuleOrLoad<CoreModule>("core").Table = table;
            rule.GetModuleOrLoad<CoreModule>("core").Jump = chain;
            return rule;
        }

        public static IpTablesRule CreateGoto(String chain, String table, ISystemFactory system)
        {
            IpTablesRule rule = new IpTablesRule(system);
            rule.GetModuleOrLoad<CoreModule>("core").Table = table;
            rule.GetModuleOrLoad<CoreModule>("core").Goto = chain;
            return rule;
        }

        public static IpTablesRule CreateJump(IpTablesChain chain)
        {
            return CreateJump(chain.Name, chain.Table, chain.System.System);
        }

        public static IpTablesRule CreateGoto(IpTablesChain chain)
        {
            return CreateGoto(chain.Name, chain.Table, chain.System.System);
        }
    }
}
