# Shipped Mono reporting-store check

This macOS harness embeds the supplied shipped x86_64 Mono library in standalone owned
processes. It never launches Unity, writes to the supplied installation or interacts with
an existing game. All test stores and build outputs use a fresh temporary directory.
It reuses the marketplace lease embedding host and compiles the actual reporting store
and lease as net35 source.

Prerequisites are Python 3, .NET SDK, Xcode command-line tools, matching shipped Mono and
managed assemblies, and Rosetta on Apple Silicon.

```sh
python3 FTKModFramework/Tests/ReportingSession/LegacyMono/verify.py \
  --runtime /path/to/copied/game/Contents/Frameworks/Mono/MonoEmbedRuntime/osx/libmono.0.dylib \
  --managed /path/to/copied/game/Contents/Resources/Data/Managed
```

The test verifies checkpoint IO, exclusive ownership after ten GC/finalizer rounds,
an independent `flock` contender, a competing managed owner, forced termination of its
own host, preserved prior metadata, durable dismissal and normal shutdown observation.
It passed on the configured macOS shipped Mono copy. This does not validate Unity quit
callbacks, native UI, actual crash causes, Windows locks or power-loss durability.
