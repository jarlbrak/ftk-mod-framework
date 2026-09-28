# Blacksmith local candidate packaging

The 32-item redesign requires development framework capabilities. Its local
manifest version is not a public compatibility declaration. Prepare it with:

```sh
python3 marketplace/packages/build_classgear_blacksmith_artifacts.py \
  --redesign-manifest art-experiments/blacksmith-forge-rodin/approved-redesign/delivery/manifest.json \
  --framework-dll /path/to/tested/FTKModFramework.dll \
  --runtime-helper-dll /path/to/tested/FtkRuntimeModelTest.dll \
  --game-assembly /path/to/isolated/Assembly-CSharp.dll \
  --output scratch/blacksmith-candidate
```

The output is a deterministic archive and a content-addressed `.candidate.json`
receipt with `releaseEligible: false`. It pins the framework, runtime test helper,
game assembly, marketplace helper, archive, complete runtime file inventory,
source provenance and banner. No game or framework binaries are bundled.

A normal marketplace descriptor exists only in a temporary directory while the
helper validates archive structure. It is then discarded. No catalog descriptor,
package URL or framework compatibility range is exported. Passing this check
does not establish public compatibility or native gameplay acceptance. The
candidate receipt itself is deliberately rejected as a marketplace descriptor.

The local staging script accepts `--candidate`, `--marketplace-helper`, and an
optional `--check-only`. Before creating a backup or changing the destination,
it verifies all four binary hashes, provenance, every source file, and archive
inventory and bytes. `--check-only` performs these reads without deployment.
Actual staging additionally requires the isolated game process to be stopped.
Use a new output directory without historical `.descriptor.json` files.

This guards the supported local staging route. Manual installation of the ZIP
is still possible and is not approved by the candidate receipt. Publishing needs
the actual compatible released framework version and the outstanding native
release evidence. The builder's legacy 30-item path, without
`--redesign-manifest`, retains its existing descriptor behavior.
