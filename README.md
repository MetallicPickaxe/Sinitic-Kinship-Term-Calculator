# Sinitic Kinship Term Calculator

Click `father · father · elder-brother` and it tells you the person is your **伯祖父** — and that
southern speakers say **伯公**, northern speakers say **大爺爺**, and how to phrase it in a legal
document.

A WinUI 3 desktop application on .NET 10. Single-file, portable.

---

## Standing on prior work

This project would not exist without **[mumuy/relationship](https://github.com/mumuy/relationship)**
(MIT). It is, as far as we know, the most complete openly available treatment of Sinitic kinship
terminology: tens of thousands of relation chains, carefully collected regional variants, and a
working calculator that a great many people have found genuinely useful. Credit where it is due —
that corpus is the foundation everything here was built on.

**What mumuy contributes:** an extensive, curated corpus of kinship terms, resolved through a
precomputed lookup table (`mode-map.json`, ~20 MB). Give it a relation chain, it returns the term.

**What this project adds on top:**

| | mumuy | this project |
|---|---|---|
| How a term is produced | looked up in a precomputed table | **derived at runtime** from morphological rules |
| Deep chains (6+ hops) | table entry, or nothing | **composed** — 87% of 90,042 chains generated, not looked up |
| Regional variants | mixed into the answer set | **separated into swappable layers**, each labelled with its origin |
| Register | one answer | **four simultaneous readings** (standard / colloquial / documentary / raw) |
| Extending it | edit the source data | **drop a YAML file next to the executable** |

The relationship is incremental, not competitive. We read mumuy's output, inferred the
word-formation laws behind it, and rebuilt those laws as a generative engine — so the program
*computes* the term rather than remembering it. See [ATTRIBUTION.md](ATTRIBUTION.md) for exactly
what came from where, and for an honest account of what we did **not** manage to cover.

## Four simultaneous readings

One relationship, four ways to say it. **The selected answer is always standard Chinese**; the rest
are candidates.

| Layer | Example for `father · father · elder-brother` | Purpose |
|---|---|---|
| **Standard** | 伯祖父 | The answer. Nationally current, usable in writing |
| **Colloquial / regional** | 伯公 *(southern)* · 大爺爺 *(northern)* | Candidates, each tagged with its source layer |
| **Documentary** | 父的父的兄 | Legal-document phrasing, never contracted. Available at any depth |
| **Raw** | 我的父的父的兄 | Your input read back **unsimplified**, so you can confirm the machine understood you |

The fourth layer earns its place whenever simplification happens: enter
`mother · elder-brother · mother` and the engine resolves **外祖母**, but the raw layer still shows
**我的母的兄的母** — one glance confirms nothing was misread.

## Regional variants are pluggable data, not hard-coded strings

The rule layer contains **only the word-formation machinery**. Every term that must be *looked up*
lives in YAML:

```
Resource/Data/Lexicon/
  lexicon-standard.yaml     standard terms no rule can derive (公公 / 岳父 / 親家公)
  register-colloquial.yaml  nationwide colloquial (爺爺 / 伯伯 / 阿姨)
  dialect-north.yaml        northern (姥姥 / 大爺爺 / 丈母娘)
  dialect-south.yaml        southern (伯公 / 姑婆 / 外婆)
```

These four ship **next to the executable** in a `Lexicon` folder. Edit them, or add your own:

```yaml
meta:
  id: dialect-mine
  name: My Region
variants:
  伯祖父: [大爹爹]
```

A file that reuses a built-in `id` replaces that layer; a new `id` stacks alongside it. The same
layers are also embedded in the assembly, so deleting the folder degrades gracefully rather than
breaking the application.

### The layer format (extension API)

A layer is a small YAML file with a `meta` block and one or both of:

- **`variants`** — the common case. Alternate spellings keyed by the *standard* form; the standard
  term stays the primary answer, your spellings become tagged candidates.
- **`entries`** — a standard term the engine cannot derive (e.g. `公公`), keyed by relation chain
  and ego gender. Usually only the base layer needs this.

```yaml
meta:
  id: dialect-mine        # reuse a built-in id to override it; a fresh id stacks alongside
  name: My Region         # shown on the candidate chip: 大爹爹 · My Region
  layer: dialect          # base | register | dialect
variants:
  伯祖父: [大爹爹]         # key = standard form the engine produced; value = your local spellings
  外祖母: [阿嫲]
```

A ready-to-edit `Lexicon/sample-region.yaml.txt` ships next to the executable — copy it, rename it
to `.yaml`, and it loads on the next start-up.

This design has a history worth admitting: an early version of this project hard-coded one region's
colloquial forms (`伯公`) as the *primary* answer, displacing standard `伯祖父` — and you could not
change it without recompiling. Finding that led to the current rule: **anything looked up belongs in
data; only what can be computed stays in code.**

## Why this is not — and cannot be — "complete"

Past a shallow core, Sinitic kinship naming is an **open system**, and that has a precise
consequence worth stating plainly instead of papering over with a large number.

New compound terms form by composition without bound; regional and historical usage disagree; and
no authority enumerates them all. Classical sources (*Erya*, *Chengweilu*) are canonical but
shallow; online calculators largely derive from one another; legal degree-of-kinship tables measure
something else. Every dictionary and table — mumuy's included — is a finite snapshot of an unbounded,
still-evolving convention. We looked for a second authority to cross-check against and found none;
details in [ATTRIBUTION.md](ATTRIBUTION.md).

Formally, the relation *chain → correct term* has **no total, authoritative oracle**. You can
enumerate the chains, but there is no complete ground-truth function to check the answers against —
the classic **test-oracle problem**, here compounded by an **open-world** domain, where an absent
term is not evidence that no term exists. Exhaustive correctness is therefore **unverifiable in
principle**, not merely unverified in practice. (It is not *undecidable* in the Turing sense — the
engine always terminates and returns a string; it is that no authority exists to call that string
right or wrong.) Chasing "100%" is chasing a horizon.

So we make a **bounded** claim, not a complete one:

1. **Correct by construction** — deep terms are *computed* from kinship morphology, so the engine
   answers where any finite table is silent.
2. **Internally consistent** — machine-checked metamorphic invariants (determinism,
   path-independence, generation and terminal-gender arithmetic) are asserted over large random
   samples; where they do not yet hold, the rate is *measured and published* below, not hidden.
3. **Concordant with attested sources** — on the overlap where mumuy or a standard dictionary
   records a term, we agree, or we diverge deliberately and traceably.

The honest positioning: **built on mumuy's corpus, one level beyond it.** Table lookup answers a
finite set and stops; we compute past its edge, mark the boundary plainly (a descriptive
`father-of-father-of-…` reading where composition runs out), and treat the un-attested tail as
*served and disclosed* — never as *certified*.

## What is, and isn't, verified

| Surface | Status | What it actually proves |
|---|---|---|
| Unit tests | 156 passed / 0 failed / 1 skipped | behaviour pinned against regressions |
| Verification tests | 59 / 59 | rule-layer invariants hold |
| Hand-adjudicated set (438 rows) | **0 mismatches** | a human-curated safety net, judged by the freshly built engine (the loop hard-fails on a stale binary); rows where the reference's regional form is served as a tagged candidate are graded *acceptable*, marked 候選命中 — not silently counted as full matches |
| Deep comparison vs mumuy (90,042 rows) | ~96% reconciled | agreement *where mumuy has an answer*; ~4% deliberate or open divergence |
| Terminal-gender consistency (random chains) | **0 / 7,500** | an oracle-free metamorphic invariant that started life as a 1.13% defect gauge; the structure-collapsing shortcuts behind it were retired across two audit rounds and the run (fixed seed, deterministic) now asserts exactly zero |

`Utility\Scripts\Run-ValidationLoop.ps1` enforces these as **hard gates** (any failure exits
nonzero): build/restore/test exit codes; suite totals at or above the recorded floor with zero
failures; binaries resolved at deterministic paths and **provenance-sealed** — each assembly's
embedded source revision must equal the current git HEAD, so a stale or back-dated binary is
rejected outright; the 90k TSV is deleted before the run and checked after for freshness, its
exact 90,042 row count, and a legal judgment vocabulary; the 438 face must contain exactly 438
judged rows. Metrics are reported split: a *primary-answer mismatch* whose reference term is
still served among our candidates (marked 候選命中) is disclosed separately from a genuine
*served miss*; the gates bind the served-miss counts (438 ≤ 0, 90k ratcheted).

None of these, alone or together, certifies the un-attested tail — the 438 set is what a human
curated, the 90k set measures agreement only where mumuy speaks, and the metamorphic gauges measure
internal consistency where *nothing external can*. By the argument above, nothing can certify that
tail. That is the design boundary, stated on purpose rather than hidden behind a percentage.

## Building

```powershell
# Produce the portable single-file executable in Distribution\ (works on a clean clone)
Script\Publish-SingleFile.ps1

# Build and run the full verification suite with hard gates
Utility\Scripts\Run-ValidationLoop.ps1
```

Requirements: Visual Studio (with MSBuild), the .NET 10 SDK (`global.json` pins the SDK band),
and **PowerShell 7+ (`pwsh`)** for the scripts — Windows PowerShell 5.1 cannot parse them.
MSBuild is resolved via `vswhere -latest -prerelease` (a deliberate newest-toolchain stance);
both scripts print the resolved MSBuild version and git HEAD so every run records its toolchain.
`Distribution\SHA256SUMS.txt` covers the executable **and** every shipped Lexicon file.

### Reproducing the validation faces (optional, dev-only)

The application itself needs nothing beyond this repository. The **validation loop's oracle
data** (~47 MB) is not tracked, and an honest caveat applies: the upstream
[mumuy/relationship](https://github.com/mumuy/relationship) project ships its data as JS
source modules (`src/module/mode.js`, `cache.js`, …), **not** as the JSON files this repo
consumes — ours are one-time local extractions of that data, and the upstream commit they were
taken from was not recorded at extraction time. What IS pinned is the exact bytes every figure
in this README was measured against:

```
FE4B66691BC3BD437E2C88D4D4C738F6DEAAF60844A610E235B7D0644F0B35D1  Utility/MumuyAlgorithm/Data/mode-map.json
1E105A7DBF6DF3E8B0E3C7087D5F34F91273325F5B99E5C150E62C740590A9E4  Utility/MumuyAlgorithm/Data/cache.json
67B2AECE10AB3E79AC33EA65F1CA64AFA474DF800D9B500A5C1386701337EFCF  Utility/MumuyAlgorithm/Data/kinship_terms.yaml
```

`kinship_terms.yaml` is derived locally (`Utility\Scripts\import_mumuy_terms.py` /
`export_kinship_yaml.py`). Without these files the two comparison faces (438 / 90k) and the
oracle-backed tests cannot run; everything else builds and passes.

## Licence

MIT — see [LICENSE](LICENSE). Kinship data originates from
[mumuy/relationship](https://github.com/mumuy/relationship), likewise MIT; see
[ATTRIBUTION.md](ATTRIBUTION.md).
