#!/usr/bin/env python3
"""Prepare synthetic A3 browser trials locally. Never opens a browser or publishes."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import urlencode, quote, parse_qs, urlsplit

import yaml

ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = 'jarlbrak/ftk-mod-framework'
FIELD_IDS = ('summary', 'repro', 'expected-actual', 'log', 'affected',
             'frequency', 'environment', 'diagnostics')
URL_CAP = 6000


def form():
    value = yaml.safe_load((ROOT / '.github/ISSUE_TEMPLATE/bug_report.yml').read_text())
    present = [item['id'] for item in value['body'] if 'id' in item]
    assert present[:5] == list(FIELD_IDS[:5])
    value['name'] = 'Synthetic reporting proof'
    value['description'] = 'Disposable FTK reporting feasibility trials only'
    if 'frequency' not in present:
        value['body'].append({'type': 'dropdown', 'id': 'frequency',
                              'attributes': {'label': 'Frequency', 'options':
                                             ['Not sure', 'Once', 'Sometimes', 'Every time']},
                              'validations': {'required': False}})
    if 'environment' not in present:
        value['body'].append({'type': 'textarea', 'id': 'environment',
                              'attributes': {'label': 'Environment and observed mod state'},
                              'validations': {'required': False}})
    diagnostics = next((item for item in value['body'] if item.get('id') == 'diagnostics'), None)
    if diagnostics is None:
        diagnostics = {'type': 'textarea', 'id': 'diagnostics', 'attributes':
                       {'label': 'Optional reviewed diagnostics'}, 'validations': {'required': False}}
        value['body'].append(diagnostics)
    diagnostics['attributes']['description'] = (
        'Synthetic files only. Selecting a file uploads immediately; public repository files may be public before submission.')
    # Preserve repository-supplied label/assignee policy for a faithful trial.
    # No privileged parameter is added to a generated URL.
    return value


def plan(repository, title, fields):
    if not re.fullmatch(r'[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Expected a GitHub owner/repository name')
    if repository.casefold() == PRODUCTION.casefold():
        raise ValueError('Production tracker is not a proof destination')
    if set(fields) - set(FIELD_IDS):
        raise ValueError('Unknown form field')
    base = f'https://github.com/{repository}/issues/new'
    query = [('template', 'bug_report.yml'), ('title', title)]
    query.extend((name, fields[name]) for name in FIELD_IDS if name in fields and name != 'log')
    full = base + '?' + urlencode(query, quote_via=quote, safe='')
    fallback = base + '?template=bug_report.yml'
    return dict(mode='prefilled' if len(full.encode('ascii')) <= URL_CAP else 'template-only',
                url=full if len(full.encode('ascii')) <= URL_CAP else fallback,
                candidateUrlBytes=len(full.encode('ascii')), title=title, fields=fields)


def cases(repository):
    ordinary = {
        'summary': 'Synthetic: café 雪 🎲 & 100% #focus',
        'repro': '1. Open Options.\n2. Cancel.\n3. Reopen.\nLiteral query text: &labels=unexpected%26x#fragment',
        'expected-actual': 'Expected: focus retained.\nActual: synthetic focus change.',
        'affected': 'Player attribution: not sure',
        'frequency': 'Synthetic: twice',
        'environment': 'Synthetic only; framework proof; platform unknown; load outcomes unknown.',
        'diagnostics': 'schemaVersion=1; reportId=synthetic-browser-report; captureId=synthetic-browser-capture; partial. Attach reviewed diagnostics.txt explicitly.',
    }
    normal = plan(repository, 'Bug: ' + ordinary['summary'], ordinary)
    long_fields = dict(ordinary, repro='Synthetic oversized reproduction: ' + '雪🎲&%\n' * 500)
    oversized = plan(repository, 'Bug: Synthetic long report', long_fields)
    text_fields = dict(ordinary, diagnostics='schemaVersion=1; reportId=synthetic-text-report; captureId=null; diagnostics not shared.')
    text_only = plan(repository, 'Bug: Synthetic text-only report', text_fields)
    title_fields = dict(ordinary, summary='🎲' * 116)
    shortened = plan(repository, 'Bug: ' + '🎲' * 115, title_fields)
    shortened['titleNote'] = 'Synthetic title shortened from 121 to 120 Unicode scalar values; full summary retained.'
    return {'normal': normal, 'oversized': oversized, 'text-only': text_only, 'title-shortening': shortened}


def check():
    schema = form()
    fields = {item['id']: item for item in schema['body'] if 'id' in item}
    assert tuple(fields) == FIELD_IDS
    assert fields['diagnostics']['type'] == 'textarea'
    assert 'render' not in fields['diagnostics']['attributes']
    assert not fields['diagnostics']['validations']['required']
    assert all(fields[k]['validations']['required'] for k in ('summary','repro','expected-actual'))
    for repository in (PRODUCTION, PRODUCTION.upper(), 'owner/repo?labels=bad', '../repo', 'https://github.com/o/r'):
        try:
            plan(repository, 'test', {})
        except ValueError:
            pass
        else:
            raise AssertionError('Unsafe destination accepted')
    results = cases('synthetic-owner/reporting-fixture')
    assert results['normal']['mode'] == 'prefilled'
    assert results['oversized']['mode'] == 'template-only'
    assert len(results['title-shortening']['title']) == 120
    assert len('Bug: ' + results['title-shortening']['fields']['summary']) == 121
    for case in results.values():
        assert len(case['url'].encode('ascii')) <= URL_CAP
        parsed = parse_qs(urlsplit(case['url']).query)
        if case['mode'] == 'prefilled':
            assert parsed == {'template': ['bug_report.yml'], 'title': [case['title']],
                              **{key: [value] for key, value in case['fields'].items()}}
        else:
            assert parsed == {'template': ['bug_report.yml']}
    baseline = plan('synthetic-owner/reporting-fixture', 'Bug: boundary', {'summary': ''})
    room = URL_CAP - baseline['candidateUrlBytes']
    assert plan('synthetic-owner/reporting-fixture', 'Bug: boundary', {'summary': 'a' * room})['mode'] == 'prefilled'
    assert plan('synthetic-owner/reporting-fixture', 'Bug: boundary', {'summary': 'a' * (room+1)})['mode'] == 'template-only'
    print('PASS: form IDs, optional attachment field, destination rejection, Unicode/query roundtrip, 6000/6001-byte boundary')


def prepare(repository, destination):
    trials = cases(repository)  # Validate destination before creating files.
    if destination.exists():
        raise ValueError('Output must be a new directory')
    destination.mkdir(parents=True)
    schema_dir = destination / '.github/ISSUE_TEMPLATE'
    schema_dir.mkdir(parents=True)
    (schema_dir / 'bug_report.yml').write_text(yaml.safe_dump(form(), sort_keys=False, allow_unicode=True), encoding='utf-8')
    (schema_dir / 'config.yml').write_text('blank_issues_enabled: false\n', encoding='utf-8')
    diagnostics = dict(schemaVersion=1, reportId='synthetic-browser-report',
                       captureId='synthetic-browser-capture', status='partial',
                       reason='source_error', source='synthetic fixture, no game collection',
                       mods=[{'guid':'synthetic.mod','outcome':'unknown'}], logs=None)
    (destination / 'diagnostics.txt').write_text(json.dumps(diagnostics, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    for name, trial in trials.items():
        folder = destination / name
        folder.mkdir()
        (folder / 'fields.json').write_text(json.dumps(trial, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        # Recovery is verbatim field text, not a production Markdown formatter.
        text = '\n\n'.join(key + '\n' + value for key,value in trial['fields'].items())
        (folder / 'report.txt').write_text(trial['title']+'\n'+trial.get('titleNote', '')+'\n\n'+text+'\n', encoding='utf-8')
        (folder / 'url.txt').write_text(trial['url']+'\n', encoding='utf-8')
    hashes = {str(p.relative_to(destination)): hashlib.sha256(p.read_bytes()).hexdigest()
              for p in sorted(destination.rglob('*')) if p.is_file()}
    (destination / 'hashes.json').write_text(json.dumps(hashes, sort_keys=True, indent=2)+'\n', encoding='utf-8')
    print('Prepared synthetic trials locally; nothing opened, uploaded or published.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repository', help='Explicitly designated test owner/repository, never production')
    parser.add_argument('--output', type=Path, help='New local directory')
    args = parser.parse_args()
    if bool(args.repository) != bool(args.output):
        parser.error('--repository and --output must be supplied together')
    check()
    if args.repository:
        prepare(args.repository, args.output)


if __name__ == '__main__':
    main()
