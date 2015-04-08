#include "ipthelper.h"
/* Code to take an iptables-style command line and do it. */

/*
* Author: Paul.Russell@rustcorp.com.au and mneuling@radlogic.com.au
*
* (C) 2000-2002 by the netfilter coreteam <coreteam@netfilter.org>:
* 		    Paul 'Rusty' Russell <rusty@rustcorp.com.au>
* 		    Marc Boucher <marc+nf@mbsi.ca>
* 		    James Morris <jmorris@intercode.com.au>
* 		    Harald Welte <laforge@gnumonks.org>
* 		    Jozsef Kadlecsik <kadlec@blackhole.kfki.hu>
*
*	This program is free software; you can redistribute it and/or modify
*	it under the terms of the GNU General Public License as published by
*	the Free Software Foundation; either version 2 of the License, or
*	(at your option) any later version.
*
*	This program is distributed in the hope that it will be useful,
*	but WITHOUT ANY WARRANTY; without even the implied warranty of
*	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*	GNU General Public License for more details.
*
*	You should have received a copy of the GNU General Public License
*	along with this program; if not, write to the Free Software
*	Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
*/

#include <getopt.h>
#include <string.h>
#include <netdb.h>
#include <errno.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <ctype.h>
#include <stdarg.h>
#include <limits.h>
#include <unistd.h>
#include <iptables.h>
#include <xtables.h>
#include <fcntl.h>
#include <assert.h>
#include "ipthelper.h"
#include "iptables.h"

#ifdef _WIN32
#include <windows.h>
#else
#include <wordexp.h>
#endif

char **split_commandline(const char *cmdline, int *argc)
{
	int i;
	char **argv = NULL;
	assert(argc);

	if (!cmdline)
	{
		return NULL;
	}

	// Posix.
#ifndef _WIN32
	{
		wordexp_t p;

		// Note! This expands shell variables.
		if (wordexp(cmdline, &p, 0))
		{
			return NULL;
		}

		*argc = p.we_wordc;

		if (!(argv = calloc(*argc, sizeof(char *))))
		{
			goto fail;
		}

		for (i = 0; i < p.we_wordc; i++)
		{
			if (!(argv[i] = strdup(p.we_wordv[i])))
			{
				goto fail;
			}
		}

		wordfree(&p);

		return argv;
	fail:
		wordfree(&p);
	}
#else // WIN32
	{
		wchar_t **wargs = NULL;
		size_t needed = 0;
		wchar_t *cmdlinew = NULL;
		size_t len = strlen(cmdline) + 1;

		if (!(cmdlinew = calloc(len, sizeof(wchar_t))))
			goto fail;

		if (!MultiByteToWideChar(CP_ACP, 0, cmdline, -1, cmdlinew, len))
			goto fail;

		if (!(wargs = CommandLineToArgvW(cmdlinew, argc)))
			goto fail;

		if (!(argv = calloc(*argc, sizeof(char *))))
			goto fail;

		// Convert from wchar_t * to ANSI char *
		for (i = 0; i < *argc; i++)
		{
			// Get the size needed for the target buffer.
			// CP_ACP = Ansi Codepage.
			needed = WideCharToMultiByte(CP_ACP, 0, wargs[i], -1,
				NULL, 0, NULL, NULL);

			if (!(argv[i] = malloc(needed)))
				goto fail;

			// Do the conversion.
			needed = WideCharToMultiByte(CP_ACP, 0, wargs[i], -1,
				argv[i], needed, NULL, NULL);
		}

		if (wargs) LocalFree(wargs);
		if (cmdlinew) free(cmdlinew);
		return argv;

	fail:
		if (wargs) LocalFree(wargs);
		if (cmdlinew) free(cmdlinew);
	}
#endif // WIN32

	if (argv)
	{
		for (i = 0; i < *argc; i++)
		{
			if (argv[i])
			{
				free(argv[i]);
			}
		}

		free(argv);
	}

	return NULL;
}

//#include "xshared.h"

#ifndef TRUE
#define TRUE 1
#endif
#ifndef FALSE
#define FALSE 0
#endif

#define FMT_NOCOUNTS	0x0002
#define FMT_KILOMEGAGIGA 0x0004
#define FMT_OPTIONS	0x0008
#define FMT_NOTABLE	0x0010
#define FMT_NOTARGET	0x0020
#define FMT_VIA		0x0040
#define FMT_NONEWLINE	0x0080
#define FMT_LINENUMBERS 0x0100

#define FMT_PRINT_RULE (FMT_NOCOUNTS | FMT_OPTIONS | FMT_VIA \
			| FMT_NUMERIC | FMT_NOTABLE)
#define FMT(tab,notab) ((format) & FMT_NOTABLE ? (notab) : (tab))

/* Table of legal combinations of commands and options.  If any of the
* given commands make an option legal, that option is legal (applies to
* CMD_LIST and CMD_ZERO only).
* Key:
*  +  compulsory
*  x  illegal
*     optional
*/

#define opts iptables_globals.opts
#define prog_name iptables_globals.program_name
#define prog_vers iptables_globals.program_version

char buffer[10240];
char* ptr = buffer;

/* Primitive headers... */
/* defined in netinet/in.h */
#if 0
#ifndef IPPROTO_ESP
#define IPPROTO_ESP 50
#endif
#ifndef IPPROTO_AH
#define IPPROTO_AH 51
#endif
#endif

enum {
	IPT_DOTTED_ADDR = 0,
	IPT_DOTTED_MASK
};

/*
*	All functions starting with "parse" should succeed, otherwise
*	the program fails.
*	Most routines return pointers to static data that may change
*	between calls to the same or other routines with a few exceptions:
*	"host_to_addr", "parse_hostnetwork", and "parse_hostnetworkmask"
*	return global static data.
*/

/* Christophe Burki wants `-p 6' to imply `-m tcp'.  */
/* Can't be zero. */
static int
parse_rulenumber(const char *rule)
{
	unsigned int rulenum;

	if (!xtables_strtoui(rule, NULL, &rulenum, 1, INT_MAX))
		xtables_error(PARAMETER_PROBLEM,
		"Invalid rule number `%s'", rule);

	return rulenum;
}

static const char *
parse_target(const char *targetname)
{
	const char *ptr;

	if (strlen(targetname) < 1)
		xtables_error(PARAMETER_PROBLEM,
		"Invalid target name (too short)");

	if (strlen(targetname) >= XT_EXTENSION_MAXNAMELEN)
		xtables_error(PARAMETER_PROBLEM,
		"Invalid target name `%s' (%u chars max)",
		targetname, XT_EXTENSION_MAXNAMELEN - 1);

	for (ptr = targetname; *ptr; ptr++)
		if (isspace(*ptr))
			xtables_error(PARAMETER_PROBLEM,
			"Invalid target name `%s'", targetname);
	return targetname;
}

static void
print_num(uint64_t number, unsigned int format)
{
	if (format & FMT_KILOMEGAGIGA) {
		if (number > 99999) {
			number = (number + 500) / 1000;
			if (number > 9999) {
				number = (number + 500) / 1000;
				if (number > 9999) {
					number = (number + 500) / 1000;
					if (number > 9999) {
						number = (number + 500) / 1000;
						ptr += sprintf(ptr,FMT("%4lluT ", "%lluT "), (unsigned long long)number);
					}
					else ptr += sprintf(ptr,FMT("%4lluG ", "%lluG "), (unsigned long long)number);
				}
				else ptr += sprintf(ptr,FMT("%4lluM ", "%lluM "), (unsigned long long)number);
			}
			else
				ptr += sprintf(ptr,FMT("%4lluK ", "%lluK "), (unsigned long long)number);
		}
		else
			ptr += sprintf(ptr,FMT("%5llu ", "%llu "), (unsigned long long)number);
	}
	else
		ptr += sprintf(ptr,FMT("%8llu ", "%llu "), (unsigned long long)number);
}

static int
print_match(const struct xt_entry_match *m,
const struct ipt_ip *ip,
int numeric)
{
	const struct xtables_match *match =
		xtables_find_match(m->u.user.name, XTF_TRY_LOAD, NULL);

	if (match) {
		if (match->print)
			match->print(ip, m, numeric);
		else
			ptr += sprintf(ptr,"%s ", match->name);
	}
	else {
		if (m->u.user.name[0])
			ptr += sprintf(ptr,"UNKNOWN match `%s' ", m->u.user.name);
	}
	/* Don't stop iterating. */
	return 0;
}


#define IP_PARTS_NATIVE(n)			\
(unsigned int)((n)>>24)&0xFF,			\
(unsigned int)((n)>>16)&0xFF,			\
(unsigned int)((n)>>8)&0xFF,			\
(unsigned int)((n)&0xFF)

#define IP_PARTS(n) IP_PARTS_NATIVE(ntohl(n))

/* This assumes that mask is contiguous, and byte-bounded. */
static void
print_iface(char letter, const char *iface, const unsigned char *mask,
int invert)
{
	unsigned int i;

	if (mask[0] == 0)
		return;

	ptr += sprintf(ptr,"%s -%c ", invert ? " !" : "", letter);

	for (i = 0; i < IFNAMSIZ; i++) {
		if (mask[i] != 0) {
			if (iface[i] != '\0')
				ptr += sprintf(ptr,"%c", iface[i]);
		}
		else {
			/* we can access iface[i-1] here, because
			* a few lines above we make sure that mask[0] != 0 */
			if (iface[i - 1] != '\0')
				ptr += sprintf(ptr,"+");
			break;
		}
	}
}

static int fd;
static fpos_t pos;

void switchStdout(const char *newStream)
{
	fflush(stdout);
	fgetpos(stdout, &pos);
	fd = dup(fileno(stdout));
	freopen(newStream, "w", stdout);
}

void revertStdout()
{
	fflush(stdout);
	dup2(fd, fileno(stdout));
	close(fd);
	clearerr(stdout);
	fsetpos(stdout, &pos);
}

static int print_match_save(const struct xt_entry_match *e,
	const struct ipt_ip *ip)
{
	char buf[BUFSIZ];
	const struct xtables_match *match =
		xtables_find_match(e->u.user.name, XTF_TRY_LOAD, NULL);

	if (match) {
#ifdef OLD_IPTABLES
		ptr += sprintf(ptr," -m %s", e->u.user.name);
#else
		ptr += sprintf(ptr, " -m %s", match->alias ? match->alias(e) : e->u.user.name);
#endif

		/* some matches don't provide a save function */
		if (match->save){
			memset(buf, 0, sizeof(buf));
			switchStdout("/dev/null");
			setvbuf(stdout, buf, _IOLBF, BUFSIZ);
			match->save(ip, e);
			fflush(stdout);
			setbuf(stdout, NULL);
			revertStdout();
			ptr += sprintf(ptr, "%s", buf);
		}
	}
	else {
		if (e->u.match_size) {
			fprintf(stderr,
				"Can't find library for match `%s'\n",
				e->u.user.name);
			exit(1);
		}
	}
	return 0;
}

/* print a given ip including mask if neccessary */
static void print_ip(const char *prefix, uint32_t ip,
	uint32_t mask, int invert)
{
	uint32_t bits, hmask = ntohl(mask);
	int i;

	if (!mask && !ip && !invert)
		return;

	ptr += sprintf(ptr,"%s %s %u.%u.%u.%u",
		invert ? " !" : "",
		prefix,
		IP_PARTS(ip));

	if (mask == 0xFFFFFFFFU) {
		ptr += sprintf(ptr,"/32");
		return;
	}

	i = 32;
	bits = 0xFFFFFFFEU;
	while (--i >= 0 && hmask != bits)
		bits <<= 1;
	if (i >= 0)
		ptr += sprintf(ptr,"/%u", i);
	else
		ptr += sprintf(ptr,"/%u.%u.%u.%u", IP_PARTS(mask));
}

static void print_proto(uint16_t proto, int invert)
{
	if (proto) {
		unsigned int i;
		const char *invertstr = invert ? " !" : "";

		const struct protoent *pent = getprotobynumber(proto);
		if (pent) {
			ptr += sprintf(ptr,"%s -p %s", invertstr, pent->p_name);
			return;
		}

		for (i = 0; xtables_chain_protos[i].name != NULL; ++i)
			if (xtables_chain_protos[i].num == proto) {
				ptr += sprintf(ptr,"%s -p %s",
					invertstr, xtables_chain_protos[i].name);
				return;
			}

		ptr += sprintf(ptr,"%s -p %u", invertstr, proto);
	}
}

/* We want this to be readable, so only print out neccessary fields.
* Because that's the kind of world I want to live in.  */
extern EXPORT const char* output_rule4(const struct ipt_entry *e, void *h, const char *chain, int counters)
{
	const struct xt_entry_target *t;
	const char *target_name;
	char buf[BUFSIZ];

	/* print counters for iptables-save */
	if (counters > 0)
		ptr += sprintf(ptr,"[%llu:%llu] ", (unsigned long long)e->counters.pcnt, (unsigned long long)e->counters.bcnt);

	/* print chain name */
	ptr += sprintf(ptr,"-A %s", chain);

	/* Print IP part. */
	print_ip("-s", e->ip.src.s_addr, e->ip.smsk.s_addr,
		e->ip.invflags & IPT_INV_SRCIP);

	print_ip("-d", e->ip.dst.s_addr, e->ip.dmsk.s_addr,
		e->ip.invflags & IPT_INV_DSTIP);

	print_iface('i', e->ip.iniface, e->ip.iniface_mask,
		e->ip.invflags & IPT_INV_VIA_IN);

	print_iface('o', e->ip.outiface, e->ip.outiface_mask,
		e->ip.invflags & IPT_INV_VIA_OUT);

	print_proto(e->ip.proto, e->ip.invflags & XT_INV_PROTO);

	if (e->ip.flags & IPT_F_FRAG)
		ptr += sprintf(ptr,"%s -f",
		e->ip.invflags & IPT_INV_FRAG ? " !" : "");

	/* Print matchinfo part */
	if (e->target_offset) {
		IPT_MATCH_ITERATE(e, print_match_save, &e->ip);
	}

	/* print counters for iptables -R */
	if (counters < 0)
		ptr += sprintf(ptr," -c %llu %llu", (unsigned long long)e->counters.pcnt, (unsigned long long)e->counters.bcnt);

	/* Print target name */
	target_name = iptc_get_target(e, h);
#ifdef OLD_IPTABLES
	if (target_name && (*target_name != '\0'))
#ifdef IPT_F_GOTO
		ptr += sprintf(ptr," -%c %s", e->ip.flags & IPT_F_GOTO ? 'g' : 'j', target_name);
#else
		ptr += sprintf(ptr," -j %s", target_name);
#endif
#endif

	/* Print targinfo part */
	t = ipt_get_target((struct ipt_entry *)e);
	if (t->u.user.name[0]) {
		const struct xtables_target *target =
			xtables_find_target(t->u.user.name, XTF_TRY_LOAD);

		if (!target) {
			fprintf(stderr, "Can't find library for target `%s'\n",
				t->u.user.name);
			return NULL;
		}
		
#ifndef OLD_IPTABLES
		ptr += sprintf(ptr, " -j %s", target->alias ? target->alias(t) : target_name);
#endif

		if (target){
			if (target->save){
				memset(buf, 0, sizeof(buf));
				switchStdout("/dev/null");
				setvbuf(stdout, buf, _IOLBF, BUFSIZ);
				target->save(&e->ip, t);
				fflush(stdout);
				setbuf(stdout, NULL);
				revertStdout();
				ptr += sprintf(ptr, "%s", buf);
			}
			else {
				/* If the target size is greater than xt_entry_target
				* there is something to be saved, we just don't know
				* how to print it */
				if (t->u.target_size !=
					sizeof(struct xt_entry_target)) {
					fprintf(stderr, "Target `%s' is missing "
						"save function\n",
						t->u.user.name);
					return NULL;
				}
			}
		}
	}

#ifndef OLD_IPTABLES
	else if (target_name && (*target_name != '\0')){
#ifdef IPT_F_GOTO
		ptr += sprintf(ptr, " -%c %s", e->ip.flags & IPT_F_GOTO ? 'g' : 'j', target_name);
#else
		ptr += sprintf(ptr, " -j %s", target_name);
#endif
	}
#endif

	*ptr = '\0';
	ptr = buffer;

	return buffer;
}

EXPORT int execute_command(const char* rule, void *h){
	int newargc;
	char* table = "filter";
	char** newargv = split_commandline(rule, &newargc);
	int ret = do_command4(newargc, newargv,
		&table, &h);
	free(newargv);
	return ret;
}

EXPORT int init_helper(void){
	int c = xtables_init_all(&iptables_globals, NFPROTO_IPV4);
	return c;
}