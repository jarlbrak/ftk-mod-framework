# Shipped Mono lease lifetime regression

This macOS test embeds the supplied Mono library in a tiny standalone process. It never
starts Unity or the game and never writes to the supplied installation. All compiled
outputs, generated negative-control source, and lock files use a temporary directory.

Prerequisites: Python 3, .NET SDK with net35 reference-package restore access, Xcode command
line tools (`clang`), and the shipped x86_64 `libmono.0.dylib` plus its matching managed
assembly directory. Apple Silicon also requires Rosetta support for the x86_64 host.
Do not substitute modern Mono: its handle semantics may differ.

```sh
python3 FTKModFramework/Tests/MarketplaceLease/LegacyMono/verify.py \
  --runtime /path/to/isolated-copy/Contents/Frameworks/Mono/MonoEmbedRuntime/osx/libmono.0.dylib \
  --managed /path/to/isolated-copy/Contents/Resources/Data/Managed
```

The fixture acquires the actual Core lease and forces ten garbage-collection/finalizer
rounds. An independent process attempts `flock(LOCK_EX | LOCK_NB)`. The positive case must
retain the lock until process exit. A generated negative control changes only the handle
acquisition expression to the original temporary owning `SafeFileHandle` getter; on the
shipped Mono it must lose the lock after collection. Failure of either expectation fails
the test. No runtime source is modified and no game binaries or decompiled source are
copied into the repository.

The managed fixture is net35. The embedding host targets macOS x86_64; Windows lock
interoperability remains covered separately by the main MarketplaceLease suite and
requires its own platform verification. This test establishes descriptor lifetime,
not live-game lifecycle correctness or a cause for unrelated native crashes.
