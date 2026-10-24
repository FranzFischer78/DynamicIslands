# questbook_check.py - ROADMAP X8: every story line's story steps go in Raft's own quest book (the notebook); only side
# quest islands' quests go in the journal.
#
# Reads every world plan - the plan recipes here (content/recipes/plan_*.recipe, "plan rule" lines) and any .plan files
# given on the command line or found in the game's Mods/DynamicIslands/plans - and the island recipes (story items given
# and needed), then lists each story quest that would land in the journal:
#   A  a side island (no story place) brought by a main story island (WHEN quest/step/zone/signal/visit/rule of it):
#      the story goes on in the journal
#   B  a main story island brought by a side island's WHEN: the story waits on a journal quest
#   C  a main story island needs a story item (has / take / collect) that only a side island of the plan gives
#   D  a plan with islands chained by WHEN (quest:/visit:/step:...) but no main story island at all: a story line with
#      no notebook
# A main story island = story place first / after: / instead: / beside (IntroRule.MainStory).
#
#   python content/questbook_check.py [extra.plan ...]      exit code 0 = clean, 1 = findings
import glob, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
RECIPES = os.path.join(HERE, "recipes")
GAME_PLANS = r"D:\Games\SteamLibrary\steamapps\common\Raft\Mods\DynamicIslands\plans"
CHAIN_WHEN = ("quest", "step", "zone", "signal", "visit", "rule")


def read(path):
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        return f.read().splitlines()


def base_name(island):
    # "Wreckers' Cove (The Long Voyage)" -> "Wreckers' Cove" (a library copy's name)
    return re.sub(r"\s*\([^)]*\)\s*$", "", island.strip())


def parse_rule(text):
    p = [x.strip() for x in text.split("|")]
    p += [""] * (12 - len(p))
    rid, what, when, where, place = p[0], p[1], p[2], p[3], p[6]
    return {"id": rid, "what": what, "when": when, "where": where, "place": place,
            "main": place != "", "island": base_name(what[7:]) if what.startswith("island:") else ""}


def plans():
    out = []
    for path in sorted(glob.glob(os.path.join(RECIPES, "plan_*.recipe"))):
        name, rules = os.path.basename(path), []
        for line in read(path):
            m = re.match(r"^plan new (.+)$", line)
            if m: name = m.group(1).strip() + " (recipe " + os.path.basename(path) + ")"
            m = re.match(r"^plan rule (.+)$", line)
            if m: rules.append(parse_rule(m.group(1)))
        out.append((name, rules))
    files = sys.argv[1:] or sorted(glob.glob(os.path.join(GAME_PLANS, "*.plan")))
    for path in files:
        rules = [parse_rule(l.split("=", 1)[1]) for l in read(path) if re.match(r"^rule\s*=", l)]
        out.append((os.path.basename(path), rules))
    return out


def islands():
    """island name -> (story items given, story items needed), includes followed."""
    by_file, names = {}, {}
    for path in glob.glob(os.path.join(RECIPES, "*.recipe")):
        key = os.path.splitext(os.path.basename(path))[0]
        by_file[key] = read(path)
        for line in by_file[key]:
            m = re.match(r"^save (.+)$", line)
            if m: names[m.group(1).strip()] = key

    def items(key, seen):
        gives, needs = set(), set()
        if key in seen or key not in by_file: return gives, needs
        seen.add(key)
        for line in by_file[key]:
            m = re.match(r"^include (\S+)", line)
            if m:
                g, n = items(m.group(1), seen); gives |= g; needs |= n
            gives |= set(re.findall(r"story:([A-Za-z0-9_]+)\*", line))
            needs |= set(re.findall(r"(?:has|take|collect)\|story:([A-Za-z0-9_]+)\|", line))
        return gives, needs

    return {n: items(k, set()) for n, k in names.items()}


def refers(when, rule, rules):
    """The rule id or island a WHEN like quest:<ref> / step:<ref>:2 / rule:<id> points to."""
    k, _, rest = when.partition(":")
    if k not in CHAIN_WHEN or not rest: return None
    ref = rest.split(":")[0].strip()
    for r in rules:
        if r["id"].lower() == ref.lower() or (r["island"] and r["island"].lower() == base_name(ref).lower()): return r
    return None


def check(name, rules, isl):
    found = []
    for r in rules:
        src = refers(r["when"], r, rules)
        if src is None: continue
        if src["main"] and not r["main"]:
            found.append("A  side island '%s' is brought by main story '%s' (%s): give it a story place (beside)" % (r["id"], src["id"], r["when"]))
        if r["main"] and not src["main"]:
            found.append("B  main story '%s' waits on side island '%s' (%s)" % (r["id"], src["id"], r["when"]))
    for r in rules:
        if not r["main"] or r["island"] not in isl: continue
        own = isl[r["island"]][0]
        for item in sorted(isl[r["island"]][1] - own):
            main_give = any(x["main"] and x["island"] in isl and item in isl[x["island"]][0] for x in rules)
            side = [x["id"] for x in rules if not x["main"] and x["island"] in isl and item in isl[x["island"]][0]]
            if side and not main_give:
                found.append("C  main story '%s' needs story item '%s' found only on side island(s) %s" % (r["id"], item, side))
    if rules and not any(r["main"] for r in rules) and any(refers(r["when"], r, rules) for r in rules):
        found.append("D  islands chained by WHEN but no main story island: the story line is all in the journal")
    missing = sorted({r["island"] for r in rules if r["island"] and r["island"] not in isl})
    return found, missing


def main():
    isl = islands()
    bad = 0
    for name, rules in plans():
        found, missing = check(name, rules, isl)
        main_n = sum(1 for r in rules if r["main"])
        print("%-60s %2d main story, %2d side: %s" % (name, main_n, len(rules) - main_n, "PROBLEMS" if found else "ok"))
        for f in found: print("    " + f)
        if missing: print("    (no recipe for: %s - item checks skipped there)" % ", ".join(missing))
        bad += len(found)
    print("QUESTBOOK CHECK %s (%d finding(s))" % ("PASS" if bad == 0 else "FAIL", bad))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
