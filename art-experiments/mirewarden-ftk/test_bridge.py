"""Small client for the isolated, single-player FTK model test (port 8788)."""
import argparse
import json
from pathlib import Path
from urllib.request import Request, urlopen

BASE='http://127.0.0.1:8788'

def request(route,data=None):
    payload=None if data is None else json.dumps(data).encode()
    req=Request(BASE+route,data=payload,headers={'Content-Type':'application/json'})
    with urlopen(req,timeout=15) as response:
        raw=response.read()
    return raw if route=='/screenshot' else json.loads(raw)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',nargs='?',default='state')
    parser.add_argument('args',nargs='?',default='{}')
    parser.add_argument('--output',type=Path)
    a=parser.parse_args()
    if a.action=='screenshot':
        if not a.output:parser.error('screenshot requires --output')
        raw=request('/screenshot');a.output.write_bytes(raw)
        print(str(a.output));raise SystemExit
    result=request('/'+a.action) if a.action in ['health','state'] else request('/action',{'action':a.action,'args':json.loads(a.args)})
    if a.output:a.output.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))
