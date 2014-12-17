﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IPTables.Net.Iptables.Adapter.Client;
using IPTables.Net.Netfilter;

namespace IPTables.Net.Iptables.Adapter
{
    public class IPTablesBinaryAdapter : IPTablesAdapterBase
    {
        public override IIPTablesAdapterClient GetClient(IpTablesSystem system)
        {
            return new Client.IPTablesBinaryAdapterClient(system);
        }
    }
}