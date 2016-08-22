﻿//#define DEBUG_NATIVE_IPTCP
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Common.Logging;
using IPTables.Net.Exceptions;

namespace IPTables.Net.Iptables.NativeLibrary
{
    public class IptcInterface : IDisposable
    {
        private IntPtr _handle;
        public const String LibraryV4 = "libip4tc.so";
        public const String LibraryV6 = "libip6tc.so";
        public const String Helper = "libipthelper.so";
        public const int StringLabelLength = 32;

        public const String IPTC_LABEL_ACCEPT = "ACCEPT";
        public const String IPTC_LABEL_DROP = "DROP";
        public const String IPTC_LABEL_QUEUE = "QUEUE";
        public const String IPTC_LABEL_RETURN = "RETURN";

        /* Does this chain exist? */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_is_chain(String chain, IntPtr handle);

        /* Take a snapshot of the rules.  Returns NULL on error. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern IntPtr v4_iptc_init(String tablename);

        /* Cleanup after iptc_init(). */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern void v4_iptc_free(IntPtr h);

        /* Iterator functions to run through the chains.  Returns NULL at end. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern IntPtr v4_iptc_first_chain(IntPtr handle);
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern IntPtr v4_iptc_next_chain(IntPtr handle);

        /* Get first rule in the given chain: NULL for empty chain. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern IntPtr v4_iptc_first_rule(String chain,
                            IntPtr handle);

        /* Returns NULL when rules run out. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern IntPtr v4_iptc_next_rule(IntPtr prev,
                               IntPtr handle);

        /* Returns a pointer to the target name of this entry. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern String v4_iptc_get_target(IntPtr e,
                        IntPtr handle);

        /* Is this a built-in chain? */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_builtin(String chain, IntPtr handle);

        /* Get the policy of a given built-in chain */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern String v4_iptc_get_policy(String chain,
                        IntPtr counter,
                        IntPtr handle);

        /* These functions return TRUE for OK or 0 and set errno.  If errno ==
           0, it means there was a version error (ie. upgrade libiptc). */
        /* Rule numbers start at 1 for the first rule. */

        /* Insert the entry `e' in chain `chain' into position `rulenum'. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_insert_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)] String chain,
                      IntPtr e,
                      uint rulenum,
                      IntPtr handle);

        /* Atomically replace rule `rulenum' in `chain' with `e'. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_replace_entry([MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                       IntPtr e,
                       uint rulenum,
                       IntPtr handle);

        /* Append entry `e' to chain `chain'.  Equivalent to insert with
           rulenum = length of chain. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_append_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr e,
                      IntPtr handle);

        /* Delete the first rule in `chain' which matches `e', subject to
           matchmask (array of length == origfw) */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_delete_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr origfw,
                      String matchmask,
                      IntPtr handle);

        /* Delete the rule in position `rulenum' in `chain'. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_delete_num_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      uint rulenum,
                      IntPtr handle);

        /* Flushes the entries in the given chain (ie. empties chain). */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_flush_entries(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                       IntPtr handle);

        /* Zeroes the counters in a chain. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_zero_entries(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Creates a new chain. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_create_chain(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Deletes a chain. */
        [DllImport(LibraryV4, SetLastError = true)]
        static extern int v4_iptc_delete_chain(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Renames a chain. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_rename_chain(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Sets the policy on a built-in chain. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_set_policy(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chainPolicy,
                    IntPtr counters,
                    IntPtr handle);

        /* Get the number of references to this chain */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_get_references(IntPtr references,
                [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                    IntPtr handle);

        /* read packet and byte counters for a specific rule */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern IntPtr v4_iptc_read_counter(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                               uint rulenum,
                               IntPtr handle);

        /* zero packet and byte counters for a specific rule */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_zero_counter(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      uint rulenum,
                      IntPtr handle);

        /* set packet and byte counters for a specific rule */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_set_counter(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                     uint rulenum,
                     IntPtr counters,
                     IntPtr handle);

        /* Makes the actual changes. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern int v4_iptc_commit(IntPtr handle);

        /* Translates errno numbers into more human-readable form than strerror. */
        [DllImport(LibraryV4, SetLastError = true)]
        public static extern IntPtr v4_iptc_strerror(int err);

        /* Does this chain exist? */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_is_chain(String chain, IntPtr handle);

        /* Take a snapshot of the rules.  Returns NULL on error. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern IntPtr v6_iptc_init(String tablename);

        /* Cleanup after iptc_init(). */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern void v6_iptc_free(IntPtr h);

        /* Iterator functions to run through the chains.  Returns NULL at end. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern IntPtr v6_iptc_first_chain(IntPtr handle);
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern IntPtr v6_iptc_next_chain(IntPtr handle);

        /* Get first rule in the given chain: NULL for empty chain. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern IntPtr v6_iptc_first_rule(String chain,
                            IntPtr handle);

        /* Returns NULL when rules run out. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern IntPtr v6_iptc_next_rule(IntPtr prev,
                               IntPtr handle);

        /* Returns a pointer to the target name of this entry. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern String v6_iptc_get_target(IntPtr e,
                        IntPtr handle);

        /* Is this a built-in chain? */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_builtin(String chain, IntPtr handle);

        /* Get the policy of a given built-in chain */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern String v6_iptc_get_policy(String chain,
                        IntPtr counter,
                        IntPtr handle);

        /* These functions return TRUE for OK or 0 and set errno.  If errno ==
           0, it means there was a version error (ie. upgrade libiptc). */
        /* Rule numbers start at 1 for the first rule. */

        /* Insert the entry `e' in chain `chain' into position `rulenum'. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_insert_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)] String chain,
                      IntPtr e,
                      uint rulenum,
                      IntPtr handle);

        /* Atomically replace rule `rulenum' in `chain' with `e'. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_replace_entry([MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                       IntPtr e,
                       uint rulenum,
                       IntPtr handle);

        /* Append entry `e' to chain `chain'.  Equivalent to insert with
           rulenum = length of chain. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_append_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr e,
                      IntPtr handle);

        /* Delete the first rule in `chain' which matches `e', subject to
           matchmask (array of length == origfw) */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_delete_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr origfw,
                      String matchmask,
                      IntPtr handle);

        /* Delete the rule in position `rulenum' in `chain'. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_delete_num_entry(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      uint rulenum,
                      IntPtr handle);

        /* Flushes the entries in the given chain (ie. empties chain). */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_flush_entries(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                       IntPtr handle);

        /* Zeroes the counters in a chain. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_zero_entries(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Creates a new chain. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_create_chain(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Deletes a chain. */
        [DllImport(LibraryV6, SetLastError = true)]
        static extern int v6_iptc_delete_chain(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Renames a chain. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_rename_chain(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      IntPtr handle);

        /* Sets the policy on a built-in chain. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_set_policy(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chainPolicy,
                    IntPtr counters,
                    IntPtr handle);

        /* Get the number of references to this chain */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_get_references(IntPtr references,
                [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                    IntPtr handle);

        /* read packet and byte counters for a specific rule */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern IntPtr v6_iptc_read_counter(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                               uint rulenum,
                               IntPtr handle);

        /* zero packet and byte counters for a specific rule */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_zero_counter(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                      uint rulenum,
                      IntPtr handle);

        /* set packet and byte counters for a specific rule */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_set_counter(
            [MarshalAs(UnmanagedType.LPStr, SizeConst = StringLabelLength)]
                String chain,
                     uint rulenum,
                     IntPtr counters,
                     IntPtr handle);

        /* Makes the actual changes. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern int v6_iptc_commit(IntPtr handle);

        /* Translates errno numbers into more human-readable form than strerror. */
        [DllImport(LibraryV6, SetLastError = true)]
        public static extern IntPtr v6_iptc_strerror(int err);

        [DllImport(Helper, SetLastError = true)]
        public static extern IntPtr output_rule4(IntPtr e, IntPtr h, String chain, int counters);

        [DllImport(Helper, SetLastError = true)]
        public static extern int execute_command4(String command, IntPtr h);

        [DllImport(Helper, SetLastError = true)]
        public static extern int init_helper();

        [DllImport(Helper, SetLastError = true)]
        static extern IntPtr init_handle(String table);

        [DllImport(Helper)]
        static extern IntPtr last_error();

        [DllImport(Helper)]
        static extern IntPtr ipth_bpf_compile([MarshalAs(UnmanagedType.LPStr)]String dltname, [MarshalAs(UnmanagedType.LPStr)]String code, int length);
        [DllImport(Helper)]
        static extern void ipth_free(IntPtr ptr);

        public static String BpfCompile(String dltName, String code, int programBufLen)
        {
            IntPtr ptr = ipth_bpf_compile(dltName, code, programBufLen);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }
            String str = Marshal.PtrToStringAnsi(ptr);
            ipth_free(ptr);
            return str;
        }

        public String LastError()
        {
            return Marshal.PtrToStringAnsi(last_error());
        }

        private static bool _helperInit = false;

        public static bool DllExists(out String msg)
        {
            try
            {
                Marshal.PrelinkAll(typeof (IptcInterface));
            }
            catch (DllNotFoundException ex)
            {
                msg = ex.Message;
                return false;
            }
            msg = null;
            return true;
        }

        public static bool DllExists()
        {
            String msg;
            return DllExists(out msg);
        }

        public IptcInterface(String table, int ipVersion, ILog log = null)
        {
            _ipVersion = ipVersion;
            logger = log;
            if (!_helperInit)
            {
                if (init_helper() < 0)
                {
                    throw new Exception("Failed to initialize the helper / xtables");
                }
                _helperInit = true;
            }
            OpenTable(table);
        }

        ~IptcInterface()
        {
            Dispose();
        }

        public void Dispose()
        {

            if (_handle != IntPtr.Zero)
            {
                Free();
            }
        }

        private List<String> _debugEntries = new List<string>();
        private ILog logger;
        private int _ipVersion;

        private void DebugEntry(string message)
        {
            if (logger != null)
            {
                _debugEntries.Add(message);
            }
        }

        private void RequireHandle()
        {
            if (_handle == IntPtr.Zero)
            {
                throw new IpTablesNetException("No IP Table currently open");
            }
        }

        private void Free()
        {
            RequireHandle();
            v4_iptc_free(_handle);
            _handle = IntPtr.Zero;
        }

        public void OpenTable(String table)
        {
            if (_handle != IntPtr.Zero)
            {
                throw new IpTablesNetException("A table is already open, commit or discard first");
            }
            _handle = init_handle(table);
            if (_handle == IntPtr.Zero)
            {
                throw new IpTablesNetException(String.Format("Failed to open table \"{0}\", error: {1}", table, LastError()));
            }
        }

        public List<IntPtr> GetRules(String chain)
        {
            RequireHandle();
            List<IntPtr> ret = new List<IntPtr>();
            IntPtr rule;
            if (_ipVersion == 4)
            {
                rule = v4_iptc_first_rule(chain, _handle);
            }
            else
            {
                rule = v6_iptc_first_rule(chain, _handle);
            }
            while (rule != IntPtr.Zero)
            {
                ret.Add(rule);
                if (_ipVersion == 4)
                {
                    rule = v4_iptc_next_rule(rule, _handle);
                }
                else
                {
                    rule = v6_iptc_next_rule(rule, _handle);
                }
            }
            return ret;
        }


        public List<string> GetChains()
        {
            RequireHandle();
            List<string> ret = new List<string>();
            IntPtr chain;
            if (_ipVersion == 4)
            {
                chain = v4_iptc_first_chain(_handle);
            }
            else
            {
                chain = v6_iptc_first_chain(_handle);
            }
            while (chain != IntPtr.Zero)
            {
                ret.Add(Marshal.PtrToStringAnsi(chain));
                if (_ipVersion == 4)
                {
                    chain = v4_iptc_next_chain(_handle);
                }
                else
                {
                    chain = v6_iptc_next_chain(_handle);
                }
            }
            return ret;
        }

        public int GetLastError()
        {
            return Marshal.GetLastWin32Error();
        }

        public String GetErrorString()
        {
            int lastError = GetLastError();
            IntPtr error;
            if (_ipVersion == 4)
            {
                error = v4_iptc_strerror(lastError);
            }
            else
            {
                error = v6_iptc_strerror(lastError);
            }
            return String.Format("({0}) {1}",lastError,Marshal.PtrToStringAnsi(error));
        }


        public String GetRuleString(String chain, IntPtr rule, bool counters = false)
        {
            RequireHandle();
            var ptr = output_rule4(rule, _handle, chain, counters ? 1 : 0);
            if (ptr == IntPtr.Zero)
            {
                throw new IpTablesNetException("IPTCH Error: " + LastError().Trim());
            }
            return Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>
        /// Insert a rule
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="entry"></param>
        /// <param name="at"></param>
        /// <returns></returns>
        public bool Insert(String chain, IntPtr entry, uint at)
        {
            RequireHandle();
            if (_ipVersion == 4)
            {
                return v4_iptc_insert_entry(chain, entry, at, _handle) == 1;
            }
            return v6_iptc_insert_entry(chain, entry, at, _handle) == 1;
        }

        /// <summary>
        /// Execute an IPTables command (add, remove, delete insert)
        /// </summary>
        /// <param name="command"></param>
        /// <returns>returns 1 for sucess, error code otherwise</returns>
        public int ExecuteCommand(string command)
        {
            DebugEntry(command);
            RequireHandle();
            var ptr = execute_command4(command, _handle);

            if (ptr == 0)
            {
                throw new IpTablesNetException("IPTCH Error: " + LastError() + " with command: " + command);
            }

            return ptr;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>if sucessful</returns>
        public bool Commit()
        {
            RequireHandle();

            if (logger != null)
            {
                foreach (var c in _debugEntries)
                {
                    logger.InfoFormat("IPTables Update: {0}", c);
                }
                _debugEntries.Clear();
            }

            bool status;

            if (_ipVersion == 4)
            {
                status = v4_iptc_commit(_handle) == 1;
            }
            else
            {
                status = v6_iptc_commit(_handle) == 1;
            }
            if (!status)
            {
                Free();
            }
            else
            {
                //Commit includes free
                _handle = IntPtr.Zero;
            }
            return status;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainName"></param>
        /// <returns>if chain exists</returns>
        public bool HasChain(string chainName)
        {
            RequireHandle();
            if (_ipVersion == 4)
            {
                return v4_iptc_is_chain(chainName, _handle) == 1;
            }

            return v6_iptc_is_chain(chainName, _handle) == 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainName"></param>
        /// <returns>if sucessful</returns>
        public bool AddChain(string chainName)
        {
            RequireHandle();
            if (_ipVersion == 4)
            {
                return v4_iptc_create_chain(chainName, _handle) == 1;
            }
            return v6_iptc_create_chain(chainName, _handle) == 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chainName"></param>
        /// <returns>if sucessful</returns>
        public bool DeleteChain(string chainName)
        {
            RequireHandle();
            if (_ipVersion == 4)
            {
                return v4_iptc_delete_chain(chainName, _handle) == 1;
            }
            return v6_iptc_delete_chain(chainName, _handle) == 1;
        }
    }
}
