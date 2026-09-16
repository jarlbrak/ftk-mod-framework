# Empty Basey Fish resource

The [source findings](findings.json) resolve renderer 121680 to Resources
`enbaseyfish`, GameObject 39967 and CEL 140194. Its `enFishA` renderer retains
34 bone references, a `grey50` material and the `fishUnarmedController`, but its
mesh pointer is null. There is no native inverse-bind baseline to export or
replace. The current strict renderer API rejects the missing source mesh.

Fresh native enemy-table inspection found no reference to this CEL among 363
nonnull rows, and none of 96 native skinsets points to it. This resolves its
resource ownership; it does not prove historical obsolescence or global non-use.
Do not borrow fishA01's binds merely because the names and topology match.
Creating a populated resource would require a separately reviewed construction
path and authored binding baseline, rather than a strict mesh replacement.

The renderer remains in the raw 34-bone topology inventory but is excluded from
the 384 valid bind profiles. Other valid renderers in that topology retain
their own evidence and pending checks. Only metadata is archived here; native
assets, decompiled source and extracted geometry remain in ignored scratch.
