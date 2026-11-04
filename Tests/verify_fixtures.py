"""Offline check: verifies actual DynamoDB fixtures and expected demo totals."""
import json
from decimal import Decimal
from pathlib import Path

root = Path(__file__).resolve().parents[1] / 'Data'
dishes = json.loads((root / 'dishes.json').read_text(encoding='utf-8'))['Dishes']
locations = json.loads((root / 'locations.json').read_text(encoding='utf-8'))['Locations']
assert len(dishes) == 8, f'Expected 8 catalog dishes, got {len(dishes)}'
assert len(locations) == 3, f'Expected 3 locations, got {len(locations)}'

catalog = {
    d['PutRequest']['Item']['id']['S']: Decimal(d['PutRequest']['Item']['price']['N'])
    for d in dishes
}
salmon = Decimal(catalog['15673599-3a6f-44fc-8b76-e64f2f4f2ce3'])
risotto = Decimal(catalog['95f0b087-4128-428f-bd3c-cc3353471e75'])
salad = Decimal(catalog['3a6058ab-493f-4dc7-b2df-3a492be1268d'])
andrii = salmon * 2 + salad
olena = salmon + risotto
marta = salad * 2
assert andrii == Decimal('47.97')
assert olena == Decimal('41.49')
assert marta == Decimal('19.98')
assert andrii + olena + marta == Decimal('109.44')
assert andrii + olena == Decimal('89.46')
assert salmon * 3 + salad + risotto == Decimal('89.46')
print('OK: 3 locations, 8 dishes, sample sums, submitted-only total and dish grouping.')
