# Attribution

The kinship knowledge in this project began with
**[mumuy/relationship](https://github.com/mumuy/relationship)** (MIT License). This document states
publicly what we took, what we built on top of it, and what we did not manage to do.

---

## 1. How the data was used

**Not copied — re-derived.**

mumuy's deep terminology is, at its core, a precomputed lookup table (`mode-map.json`, ~20 MB): give
it a relation chain, it returns the term. There is no runtime word-formation algorithm. We did the
opposite:

```
mumuy's output  ──(treated as observations)──▶  infer word-formation laws  ──▶  generative engine
   lookup                                                                        computed
```

By analogy: **like training a model on someone's dataset.** We read its output, inferred the laws
behind it (class-stacking prefixes, generation stems, the 外 marker slot, the 姻/眷 recursion), and
wrote those laws as a rule engine that *forms* terms rather than remembering them.

Measured result: across 90,042 relation chains, the engine **generates** a valid term for **87%**
on its own. Lookup is a thin shim for the remainder, not the mechanism.

## 2. What still originates from mumuy

To keep this auditable, anything overlapping mumuy's wording is **isolated into labelled, optional
data layers** — never mixed into the rule layer:

| File | Contents |
|---|---|
| `Resource/Data/Lexicon/dialect-south.yaml` | Southern colloquial layer. Its forms overlap heavily with mumuy (which is southern-leaning); the file header records this. |
| `Resource/Data/Reference/MumuyAbsorptionLedger.tsv` | Per-chain adjudication ledger: what we accepted, what we declined, and why. |
| `Utility/MumuyAlgorithm/` | An in-repo port of mumuy's algorithm, used **only as a comparison oracle during verification**. It takes no part in product output. |

**The rule layer (`KinshipCalculator.Core`) contains no vocabulary taken from mumuy.** All
looked-up terms were moved out to YAML; only the word-formation machinery remains in code.

## 3. What we did not cover — and why

Of the 90,042-row deep comparison, roughly **3,600 rows (4.0%)** still differ from mumuy's wording.
**We deliberately did not flatten them:**

| Reason | Explanation | Example |
|---|---|---|
| **Open system, no single authority** | Deep Sinitic kinship naming varies by region and register. mumuy is one southern-leaning tradition, not a gold standard. | `太姻翁` (literary/southern) vs our `女兒姻祖父` (standard) |
| **Different granularity, both defensible** | mumuy sometimes uses a coarser cover term; we compute a finer one. Sometimes the reverse. | mumuy `姻家兄` (generic) vs our `女兒姻堂伯祖父` (two generations more precise) |
| **The source is itself unsettled** | One slot, several mutually inconsistent spellings. | `太姻翁 \| 女姻祖父 \| 息姻祖父` |
| **String information floor** | The same surface form denotes two different people; spelling alone cannot disambiguate. | the `外甥` morpheme ambiguity |
| **Collective nouns** | The source gives a group noun where a per-person calculator has no counterpart. | `妻儿` / `孙辈` |

**Our position:** *differing from mumuy* is not the same as *being wrong*. Where our output is
sound by word-formation law and internally consistent, it is our primary answer; mumuy's wording is
preserved as a labelled candidate in the `dialect-south` layer. Forcing agreement would degrade
precise answers into vaguer ones, and bend standard Chinese toward one region's usage.

## 4. Licence

- **mumuy/relationship** — MIT License. Verified against the repository, July 2026. Used under its
  terms, with full credit here and in `LICENSE`.
- This project's engine, rules, tests and documentation are independent work, also MIT.

> MIT means neither party owes the other anything. We used it, we say so, and we publish our own
> method in turn — so anyone wanting to build something similar, or simply to learn, can see where
> every piece of knowledge came from.

## 5. Notes for anyone building something similar

Four things this project learned the hard way:

1. **Never hard-code regional forms into the rule layer.** We did, early on: one region's colloquial
   (`伯公`) became the primary answer and displaced standard `伯祖父`, and it could not be changed
   without recompiling.
2. **Separate what is computed from what is looked up.** Word-formation machinery in code; every
   looked-up term in data. Draw that line correctly and dialect, register and regional variants all
   become pluggable layers for free.
3. **The deep tail has no correct answer.** Past a certain depth this is an open system. Rather than
   chasing agreement with any one source, emit multiple candidates and label their provenance.
4. **Keep a small hand-adjudicated sample as a safety net.** Ours is 438 rows. Any change that
   breaks it surfaces immediately — far more valuable than a flattering number on the deep set.
