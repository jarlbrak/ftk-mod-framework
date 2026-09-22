#!/usr/bin/env python3
"""Exercise the shipped Mono file-handle lifetime without launching Unity or the game."""
import argparse
import fcntl
import json
import pathlib
import select
import subprocess
import sys
import tempfile


def trial(host, runtime, managed, assembly, directory, expected_locked):
    directory.mkdir()
    process = subprocess.Popen(
        [str(host), str(runtime), str(managed), str(assembly), str(directory)],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
    )
    try:
        if not select.select([process.stdout], [], [], 20)[0]:
            raise RuntimeError("Standalone Mono host did not reach the GC checkpoint within 20 seconds")
        checkpoint = process.stdout.readline().strip()
        if checkpoint != "LOCKED_AFTER_GC":
            raise RuntimeError("Unexpected Mono output: " + checkpoint)
        with (directory / "runtime.lock").open("r+") as lock:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                locked = False
            except BlockingIOError:
                locked = True
        if locked != expected_locked:
            raise RuntimeError("Unexpected post-GC lock state: " + str(locked))
        process.stdin.write("\n")
        process.stdin.flush()
        process.wait(timeout=10)
        if process.returncode != 0:
            raise RuntimeError("Standalone Mono host failed: " + str(process.returncode))
        with (directory / "runtime.lock").open("r+") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        return {"lockedAfterTenGcRounds": locked, "releasedAfterExit": True}
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", type=pathlib.Path, required=True, help="Read-only path to shipped x86_64 libmono.0.dylib")
    parser.add_argument("--managed", type=pathlib.Path, required=True, help="Read-only managed directory containing the shipped mscorlib.dll")
    args = parser.parse_args()
    if sys.platform != "darwin":
        parser.error("This embedding harness requires macOS; the net35 fixture itself is portable")
    runtime, managed = args.runtime.resolve(strict=True), args.managed.resolve(strict=True)
    if not (managed / "mscorlib.dll").is_file():
        parser.error("Managed directory has no mscorlib.dll")
    here = pathlib.Path(__file__).resolve().parent
    source = here.parents[2] / "Core/Marketplace/MarketplaceRuntimeLease.cs"
    with tempfile.TemporaryDirectory(prefix="ftkmf-legacy-lease-") as temporary:
        work = pathlib.Path(temporary)
        host = work / "mono-host"
        subprocess.run(["clang", "-arch", "x86_64", str(here / "host.c"), "-o", str(host)], check=True)
        fixed = source.read_text()
        needle = "IntPtr handle = candidate.Handle;"
        if fixed.count(needle) != 1:
            raise RuntimeError("Core acquisition changed; update the deliberate negative-control mutation")
        negative = work / "NegativeLease.cs"
        negative.write_text(fixed.replace(needle, "IntPtr handle = candidate.SafeFileHandle.DangerousGetHandle();"))
        results = {}
        for name, lease_source, expected in [("negativeControl", negative, False), ("fixedCore", source, True)]:
            output = work / name
            subprocess.run([
                "dotnet", "build", str(here / "LegacyMono.csproj"), "-c", "Release", "--nologo",
                "-p:LeaseSource=" + str(lease_source),
                "-p:BaseIntermediateOutputPath=" + str(work / (name + "-obj")) + "/",
                "-o", str(output),
            ], check=True)
            results[name] = trial(host, runtime, managed, output / "LegacyMono.exe", work / (name + "-state"), expected)
        print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
