# Green & Tasty · EPAM Backend Homework (.NET 8)

Самостійний навчальний ASP.NET Core Web API для **всіх трьох рівнів** із `SPECIFICATION.md` (оригінальне `homework-backend.md`). Реалізує чотири endpoint-и для Owner.

**Це тільки Backend.** Angular-фронтенд із шаблону EPAM, Terraform, AWS deploy і production infrastructure не входять до домашнього завдання, і їх навмисно НЕ включено. Цей репозиторій запускається локально без AWS, DynamoDB, npm та node_modules. GitHub Actions перевіряє збірку, API і публікацію .NET проєкту, а не деплой у хмару.

## Як запустити

Потрібен [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

```bash
dotnet restore GreenAndTasty.Backend.sln
dotnet run --launch-profile GreenAndTasty.Backend
```

Відкрий **http://localhost:5072/swagger/**. Це самодостатній API Tester (без CDN). Також доступні **/swagger/index.html**, **/openapi.json**, **/health**. Якщо запускаєш без профілю і консоль показує `http://localhost:5000`, заміни порт на 5000.

Введи `demo-owner-token`, залиш демонстраційні ID та запускай кнопками. Зміни (POST/DELETE) зберігаються в `App_Data/state.json`. Щоб скинути демо-дані, зупини сервер і видали цей файл.

## Endpoint-и

| Рівень | Method | URL | Зміст |
|:--:|---|---|---|
| 1 | GET | `/guest/groups/{groupReservationId}/sessions` | Учасники, страви та підсумки |
| 2 | GET | `/reservations/{reservationId}/guest-session-id` | Owner guestSessionId |
| 2 | POST | `/group-reservations/{groupReservationId}/submit` | Об'єднання submitted Guest-сесій по dishId, зміна статусів |
| 3 | DELETE | `/guest/groups/{groupReservationId}/sessions/{guestSessionId}` | Видалення Guest сесії, оновлений snapshot |

POST body: `{"reservationId":"reservation-123"}`; `X-Demo-Token: demo-owner-token` для всіх endpoint-ів.

| Demo ID | Значення |
|---|---|
| group | `group-123` |
| reservation | `reservation-123` |
| owner session | `owner-session-123` |
| guest for DELETE | `session3` |

Початковий total = **109.44**, після DELETE session3 = **89.46**. POST збирає тільки submitted Guest sessions: salmon ×3, salad ×1, risotto ×1. Демо-групи `group-456`, `group-789`, `group-999`, `group-321` перевіряють помилки: відсутні submitted, немає страв, немає order і Submitted Owner без Submitted Guest.

## Автоматичні перевірки

```bash
dotnet build GreenAndTasty.Backend.sln -c Release
python Tests/verify_fixtures.py
python Tests/integration_test.py
# або в Windows: py Tests/integration_test.py
```

`integration_test.py` сам запускає .NET-процес на вільному локальному порту та використовує **окремий тимчасовий каталог стану**, щоб перевірки можна було повторювати без ручного видалення `state.json`. Покриває 4 endpoint-и, 200/400/401/403/404, business rules, immutable повторний POST, збереження/перезапуск, /swagger і OpenAPI.

У `.github/workflows/backend-ci.yml` на **push**, **pull_request** та **workflow_dispatch** автоматично запускаються:

1. `dotnet restore`, `dotnet build` із `TreatWarningsAsErrors=true`;
2. перевірка оригінальних JSON і Python тестів;
3. справжні end-to-end HTTP тести запущеного API;
4. `dotnet publish` + перевірка потрібних файлів.

CI **не потребує AWS credentials, Terraform, Angular чи секретів GitHub**. Назва status check: `Backend CI / build-and-test`.

## Початкові дані й обмеження

- `Data/dishes.json` (8 страв) і `Data/locations.json` (3 локації) з вкладень EPAM; формат DynamoDB PutRequest.
- Reservations, orders і sessions у `SeedData.cs` є **навмисно створеними демонстраційними прикладами**. Їх не було в JSON.
- Owner і Guest токени (`demo-owner-token`, `demo-guest-token`) є **імітацією**, не справжнім механізмом production-автентифікації.
- `App_Data/state.json` локальне сховище одного процесу. Для тестів змінна середовища `APP_DATA_DIR` задає ізольований каталог.
- Це завершення **домашнього Backend API**, а не продакшен-ресторан і не автоматичне розгортання сайту в AWS.

## Якщо GitHub показує Terraform/apply, Deploy/deploy або PR/frontend

Ці workflows **від іншого, більшого репозиторію**. Їх немає в цьому чистому пакеті. AWS крок `aws-actions/configure-aws-credentials` закономірно не може працювати без легітимної AWS конфігурації; ми її не маскуємо. Angular lint стосується фронтенду, який не входить у Backend homework.

**Публікуй цей архів у новий порожній репозиторій** `green-and-tasty-backend-homework`. Якщо зберігаєш попередній `EPAM-HW`, перед комітом треба **видалити зайві файли і workflows**, а не просто додати нові поверх старих. Покроково: [UPLOAD_TO_GITHUB.md](UPLOAD_TO_GITHUB.md).

Цей проєкт підготовлено для перевірки GitHub Actions; **фактичну .NET компіляцію в середовищі створення архіву виконати не вдалося через відсутність dotnet SDK**, тому остаточний зелений результат підтвердить CI після завантаження. Офіційна умова в `SPECIFICATION.md`.
