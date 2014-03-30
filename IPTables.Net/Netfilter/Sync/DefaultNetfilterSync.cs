﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IPTables.Net.Netfilter.Sync
{
    class DefaultNetfilterSync<T> : INetfilterSync<T> where T : INetfilterRule
    {
        private Func<T, bool> _shouldDelete = null;

        public Func<T, bool> ShouldDelete
        {
            get { return _shouldDelete; }
            set
            {
                if (value == null)
                {
                    _shouldDelete = a => true;
                }
                else
                {
                    _shouldDelete = value;
                }
            }
        }

        private Func<T, T, bool> _ruleComparerForUpdate;

        public Func<T, T, bool> RuleComparerForUpdate
        {
            get { return _ruleComparerForUpdate; }
            set
            {
                if (value == null)
                {
                    _ruleComparerForUpdate = (a, b) => false;
                }
                else
                {
                    _ruleComparerForUpdate = value;
                }
            }
        }

        public DefaultNetfilterSync(Func<T, T, bool> ruleComparerForUpdate, Func<T, bool> shouldDelete = null)
        {
            ShouldDelete = shouldDelete;
        } 

        public void SyncChainRules(IEnumerable<T> with, IEnumerable<T> currentRules)
        {
            //Copy the rules
            currentRules = new List<T>(currentRules.ToArray());

            int i = 0, len = with.Count();
            foreach (T cR in currentRules)
            {
                //Delete any extra rules
                if (i == len)
                {
                    cR.Delete();
                    continue;
                }

                //Get the rule for comparison
                T withRule = with.ElementAt(i);

                if (cR.Equals(withRule))
                {
                    //No need to make any changes
                    i++;
                }
                else if (_ruleComparerForUpdate(cR, withRule))
                {
                    //Replace this rule
                    cR.Replace(withRule);
                    i++;
                }
                else
                {
                    if (_shouldDelete(cR))
                    {
                        cR.Delete();
                    }
                }
            }

            //Get rules to be added
            foreach (T rR in with.Skip(i))
            {
                rR.Add();
            }
        }

        public void SyncChains()
        {
            
        }

        public void SyncChainRules(IEnumerable<INetfilterRule> with, IEnumerable<INetfilterRule> currentRules)
        {
            var withCast = with.Cast<T>();
            var currentRulesCast = currentRules.Cast<T>();

            SyncChainRules(withCast, currentRulesCast);
        }
    }
}
