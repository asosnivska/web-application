# SLAE Solver

Вебзастосунок для розв'язання систем лінійних рівнянь методом Якобі. Обчислення виконують окремі процеси (Worker), а запити користувачів розподіляє власний балансувальник навантаження.

## Що вміє

- Реєстрація і вхід (JWT), HTTPS.
- Створення задач: згенерована система або матриця, введена вручну.
- Обмеження на розмір, точність, кількість ітерацій і час виконання (до 900 с).
- Прогрес виконання, історія задач, скасування.
- Черга з приблизним часом очікування, до 3 активних задач на користувача.
- Балансування між двома API, перевірка їхнього стану, відновлення задач після збою Worker-а.

Не реалізовано: автоматичне масштабування і панель адміністратора.

## Запуск (Windows, PowerShell)

Потрібні .NET 10 SDK, PostgreSQL і `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

1. Створити порожню базу в PostgreSQL.
2. У папці `src` задати налаштування (значення в `<...>` замінити своїми):

```powershell
dotnet user-secrets set "ConnectionStrings:SlaeDbContext" "Host=localhost;Port=5432;Database=<db>;Username=<user>;Password=<password>" --project Slae.WebApi
dotnet user-secrets set "ConnectionStrings:SlaeDbContext" "Host=localhost;Port=5432;Database=<db>;Username=<user>;Password=<password>" --project Slae.Worker
dotnet user-secrets set "Jwt:Key" "<випадковий рядок від 32 символів>" --project Slae.WebApi
```

3. Створити таблиці:

```powershell
dotnet ef database update --project Slae.Persistence --startup-project Slae.WebApi
```

4. Довірити сертифікат для HTTPS: `dotnet dev-certs https --trust`.
5. Запустити все (збирає проєкт і стартує 2 API, 2 Worker-и та балансувальник):

```powershell
.\run-all.ps1
```

Застосунок відкриється на <https://localhost:5443>. Зупинити: `.\stop-all.ps1`.

## API

- `POST /api/auth/register`, `POST /api/auth/login`
- `POST /api/tasks`, `GET /api/tasks`, `GET /api/tasks/{id}`, `POST /api/tasks/{id}/cancel` (потрібен токен)
- `GET /lb/status`: стан серверів на балансувальнику

## Технології

.NET 10, ASP.NET Core, Entity Framework Core, PostgreSQL, JWT, Task Parallel Library; фронтенд на чистих HTML, CSS та JavaScript.
