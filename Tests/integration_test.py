"""Repeatable end-to-end tests: launch an isolated .NET process, exercise all endpoints,
verify auth/errors/aggregation/persistence, then stop and clean up. Stdlib only.

Run from repository root after: dotnet build -c Release
"""

import contextlib
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / 'bin' / 'Release' / 'net8.0' / 'GreenAndTasty.Backend.dll'
SALMON = '15673599-3a6f-44fc-8b76-e64f2f4f2ce3'
RISOTTO = '95f0b087-4128-428f-bd3c-cc3353471e75'
SALAD = '3a6058ab-493f-4dc7-b2df-3a492be1268d'


def unused_port():
    with socket.socket() as server:
        server.bind(('127.0.0.1', 0))
        return server.getsockname()[1]


def request(base, method, path, token='demo-owner-token', payload=None):
    headers = {}
    if token is not None:
        headers['X-Demo-Token'] = token
    body = None
    if payload is not None:
        headers['Content-Type'] = 'application/json'
        body = json.dumps(payload).encode('utf-8')
    req = urllib.request.Request(base + path, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=6) as response:
            status, data, mime = response.status, response.read(), response.headers.get('Content-Type', '')
    except urllib.error.HTTPError as error:
        status, data, mime = error.code, error.read(), error.headers.get('Content-Type', '')
    if 'application/json' in mime and data:
        return status, json.loads(data)
    return status, data.decode('utf-8', errors='replace')


def check(condition, message):
    if not condition:
        raise AssertionError(message)


def expected_status(base, method, path, status, token='demo-owner-token', payload=None):
    result = request(base, method, path, token, payload)
    check(result[0] == status, f'{method} {path}: expected {status}, got {result}')
    return result[1]


@contextlib.contextmanager
def app_server(state_dir, base, log_path):
    env = dict(os.environ)
    env.update({
        'APP_DATA_DIR': str(state_dir),
        'ASPNETCORE_URLS': base,
        'ASPNETCORE_ENVIRONMENT': 'Development',
    })
    with open(log_path, 'a', encoding='utf-8') as log:
        proc = subprocess.Popen(['dotnet', str(DLL)], cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            started = False
            for _ in range(100):
                if proc.poll() is not None:
                    break
                try:
                    started = request(base, 'GET', '/health', token=None)[0] == 200
                except (OSError, TimeoutError):
                    pass
                if started:
                    break
                time.sleep(0.15)
            if not started:
                raise RuntimeError('App did not start: ' + Path(log_path).read_text(encoding='utf-8')[-6000:])
            yield
        finally:
            proc.terminate()
            try:
                proc.wait(timeout=7)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait(timeout=5)


def main():
    if not DLL.is_file():
        raise SystemExit('Missing Release build DLL. Run: dotnet build -c Release')
    port = unused_port()
    base = f'http://127.0.0.1:{port}'
    with tempfile.TemporaryDirectory(prefix='gat-ci-') as temporary:
        state_dir = Path(temporary) / 'state'
        log_path = Path(temporary) / 'server.log'
        try:
            with app_server(state_dir, base, log_path):
                assert expected_status(base, 'GET', '/health', 200, token=None)['status'] == 'ok'
                for route in ['/', '/swagger/', '/swagger/index.html', '/openapi.json']:
                    expected_status(base, 'GET', route, 200, token=None)
                api_spec = expected_status(base, 'GET', '/openapi.json', 200, token=None)
                check(len(api_spec['paths']) == 4, 'OpenAPI must describe all 4 homework routes')
                path = '/guest/groups/group-123/sessions'
                expected_status(base, 'GET', path, 401, token=None)
                expected_status(base, 'GET', path, 401, token='invalid-token')
                expected_status(base, 'GET', path, 403, token='demo-guest-token')
                participants = expected_status(base, 'GET', path, 200)
                check(participants['total'] == 109.44, 'Invalid initial group total')
                people = {x['id']: x for x in participants['participants']}
                check(set(people) == {'owner-session-123', 'session1', 'session2', 'session3'}, 'Wrong participant list')
                check(people['session1']['total'] == 47.97 and people['session2']['total'] == 41.49, 'Invalid participant totals')
                check(people['session3']['isSubmitted'] is False, 'Draft must not be submitted')
                expected_status(base, 'GET', '/guest/groups/missing/sessions', 404)

                owner_path = '/reservations/reservation-123/guest-session-id'
                check(expected_status(base, 'GET', owner_path, 200)['guestSessionId'] == 'owner-session-123', 'Owner ID mismatch')
                expected_status(base, 'GET', owner_path, 403, token='demo-guest-token')
                expected_status(base, 'GET', '/reservations/missing/guest-session-id', 404)

                submit = '/group-reservations/group-123/submit'
                expected_status(base, 'POST', submit, 401, token=None, payload={'reservationId': 'reservation-123'})
                expected_status(base, 'POST', submit, 403, token='demo-guest-token', payload={'reservationId': 'reservation-123'})
                expected_status(base, 'POST', '/group-reservations/missing/submit', 404, payload={'reservationId': 'reservation-123'})
                expected_status(base, 'POST', submit, 400, payload={'reservationId': 'wrong'})
                for group_id, reservation_id in [('456', '456'), ('789', '789'), ('999', '999'), ('321', '321')]:
                    expected_status(base, 'POST', f'/group-reservations/group-{group_id}/submit', 400,
                                    payload={'reservationId': f'reservation-{reservation_id}'})

                delete = '/guest/groups/group-123/sessions/'
                expected_status(base, 'DELETE', delete + 'session1', 401, token=None)
                expected_status(base, 'DELETE', delete + 'session1', 403, token='demo-guest-token')
                expected_status(base, 'DELETE', delete + 'owner-session-123', 400)
                expected_status(base, 'DELETE', delete + 'other-session-456', 400)
                expected_status(base, 'DELETE', delete + 'non-existent', 404)
                snapshot = expected_status(base, 'DELETE', delete + 'session3', 200)
                check(snapshot['total'] == 89.46, 'Wrong total after deletion')
                check('session3' not in {x['id'] for x in snapshot['participants']}, 'Guest was not removed')
                expected_status(base, 'DELETE', delete + 'session3', 404)
                consolidated = expected_status(base, 'POST', submit, 200, payload={'reservationId': 'reservation-123'})
                expected = {SALMON: 3, SALAD: 1, RISOTTO: 1}
                check({x['dishId']: x['quantity'] for x in consolidated} == expected, 'Incorrect consolidation')
                check(expected_status(base, 'POST', submit, 200, payload={'reservationId': 'reservation-123'}) == consolidated,
                      'Repeated submission must be idempotent')

            stored = json.loads((state_dir / 'state.json').read_text(encoding='utf-8'))
            group = next(x for x in stored['groupReservations'] if x['id'] == 'group-123')
            order = next(x for x in stored['orders'] if x['id'] == 'order-123')
            check(group['status'] == 'Submitted', 'Group status was not persisted')
            check(order['status'] == 'SubmittedByCustomer', 'Order status was not persisted')
            check({x['dishId']: x['quantity'] for x in order['items']} == expected, 'Order items were not persisted')

            # A second process must load the saved state, not re-seed it.
            with app_server(state_dir, base, log_path):
                snap = expected_status(base, 'GET', '/guest/groups/group-123/sessions', 200)
                check(snap['total'] == 89.46 and len(snap['participants']) == 3, 'Persistence after restart failed')
                check(expected_status(base, 'POST', submit, 200, payload={'reservationId': 'reservation-123'}) == consolidated,
                      'Repeated submit after restart must not duplicate items')
        except Exception:
            print('--- Last server log lines ---')
            if log_path.exists():
                print(log_path.read_text(encoding='utf-8')[-6000:])
            raise
    print('PASS: API 200/400/401/403/404, all 3 levels, sums, auth, filtering, idempotency, static UI, persistence')


if __name__ == '__main__':
    main()
