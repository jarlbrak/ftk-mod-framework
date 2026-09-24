#!/usr/bin/env python3
"""Run the real reporting store on shipped Mono in owned processes, without Unity."""
import argparse
import fcntl
import pathlib
import select
import subprocess
import sys
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', type=pathlib.Path, required=True)
    parser.add_argument('--managed', type=pathlib.Path, required=True)
    args = parser.parse_args()
    if sys.platform != 'darwin':
        parser.error('This embedding host requires macOS')
    runtime = args.runtime.resolve(strict=True)
    managed = args.managed.resolve(strict=True)
    if not (managed / 'mscorlib.dll').is_file():
        parser.error('Missing shipped mscorlib.dll')
    here = pathlib.Path(__file__).resolve().parent
    with tempfile.TemporaryDirectory(prefix='ftkmf-reporting-mono-') as temp:
        work = pathlib.Path(temp).resolve()
        host, output, state = work / 'host', work / 'bin', work / 'state'
        source = here.parents[1] / 'MarketplaceLease/LegacyMono/host.c'
        subprocess.run(['clang', '-arch', 'x86_64', str(source), '-o', str(host)], check=True, timeout=60)
        subprocess.run(['dotnet', 'build', str(here / 'LegacyMono.csproj'), '-c', 'Release', '--nologo',
                        '-p:BaseIntermediateOutputPath=' + str(work / 'obj') + '/', '-o', str(output)],
                       check=True, timeout=120)
        def command(mode):
            return [str(host), str(runtime), str(managed), str(output / 'LegacyMono.exe'), mode + '|' + str(state)]
        def run(mode, expected):
            result = subprocess.run(command(mode), capture_output=True, text=True, timeout=20)
            if result.returncode or result.stdout.strip() != expected:
                raise RuntimeError('Mono ' + mode + ' failed: ' + str(result.returncode) + ' ' + result.stdout + result.stderr)
        process = subprocess.Popen(command('hold'), stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, text=True)
        try:
            if not select.select([process.stdout], [], [], 20)[0] or process.stdout.readline().strip() != 'READY_AFTER_GC':
                raise RuntimeError('Mono did not reach the GC checkpoint')
            with (state / 'reporting.lock').open('r+') as lock:
                try:
                    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                except BlockingIOError:
                    pass
                else:
                    raise RuntimeError('Lease lost after GC')
            run('blocked', 'BLOCKED')
        finally:
            if process.poll() is None:
                process.kill()
            process.wait(timeout=10)
        run('recover', 'OK')
        run('normal', 'OK')
        run('normal', 'OK')
        print('PASS: shipped Mono checkpoint IO, GC lease retention, competing owner, forced exit recovery, provenance, dismissal and normal shutdown')


if __name__ == '__main__':
    main()
