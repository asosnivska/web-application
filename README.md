# SLAE Solver

Вебзастосунок для розв'язання систем лінійних рівнянь методом Якобі. Обчислення виконують окремі процеси (Worker), а запити користувачів розподіляє власний балансувальник навантаження.

## Що вміє

- Реєстрація і вхід (JWT), ролі User і Admin, HTTPS.
- Створення задач: згенерована система або матриця, введена вручну.
- Обмеження на розмір, точність, кількість ітерацій і час виконання (до 900 с).
- Прогрес виконання, історія задач, скасування.
- Черга з приблизним часом очікування, до 3 активних задач на користувача.
- Балансування між двома API, перевірка їхнього стану, відновлення задач після збою Worker-а.
- Панель адміністратора: стан серверів API, активні задачі з воркерами, черга.

Не реалізовано: автоматичне масштабування.

## Як це працює

<img width="1422" height="517" alt="image" src="https://github.com/user-attachments/assets/f7fd32b1-28d5-4bfb-8f50-915bafc8d44d" />

- **Балансувальник** (`Slae.LoadBalancer`) віддає фронтенд, завершує HTTPS і по колу передає запити живим API.
- **API** (`Slae.WebApi`) перевіряє JWT, створює й показує задачі.
- **Worker** (`Slae.Worker`) сам бере задачі з бази, рахує паралельно і записує прогрес.
- **PostgreSQL** зберігає користувачів і задачі та працює як черга.

<img width="1047" height="590" alt="image" src="https://github.com/user-attachments/assets/63e1914c-046b-4f53-a1f1-12b8518c63d5" />


## Запуск (Windows, PowerShell)

Потрібні .NET 10 SDK, PostgreSQL і `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

1. Створити порожню базу в PostgreSQL.
2. У папці `src` задати налаштування (значення в `<...>` замінити своїми):

```powershell
dotnet user-secrets set "ConnectionStrings:SlaeDbContext" "Host=localhost;Port=5432;Database=<db>;Username=<user>;Password=<password>" --project Slae.WebApi
dotnet user-secrets set "ConnectionStrings:SlaeDbContext" "Host=localhost;Port=5432;Database=<db>;Username=<user>;Password=<password>" --project Slae.Worker
dotnet user-secrets set "Jwt:Key" "<випадковий рядок від 32 символів>" --project Slae.WebApi
```

3. Створити таблиці (разом із міграцією ролей):

```powershell
dotnet ef database update --project Slae.Persistence --startup-project Slae.WebApi
```

4. Довірити сертифікат для HTTPS: `dotnet dev-certs https --trust`.
5. Запустити п'ять процесів, кожен у своєму вікні PowerShell, у папці `src`. Профілі запуску вже задані в `launchSettings.json`, вони виставляють порти й імена серверів:

```powershell
dotnet run --project Slae.WebApi --launch-profile api-1
dotnet run --project Slae.WebApi --launch-profile api-2
dotnet run --project Slae.Worker --launch-profile worker-1
dotnet run --project Slae.Worker --launch-profile worker-2
dotnet run --project Slae.LoadBalancer --launch-profile lb
```

Балансувальник слухає <https://localhost:5443>, API працюють на портах 5001 і 5002. Усе можна також запустити з Visual Studio, вибравши потрібний профіль для кожного проєкту. Зупинити: закрити вікна або натиснути Ctrl+C у кожному.

Після змін у коді спершу зупиніть усі процеси, інакше збірка не зможе перезаписати файли.

## Адміністратор

Усі нові користувачі отримують роль User. Щоб зробити адміністратором, виконайте в базі:

```sql
UPDATE "Users" SET "Role" = 'Admin' WHERE "Username" = '<ім'я>';
```

Потім увійдіть знову, бо роль зберігається в токені. Після цього на головній сторінці з'явиться посилання «Адмін-панель» (`/admin.html`).

## API

- `POST /api/auth/register`, `POST /api/auth/login`
- `POST /api/tasks`, `GET /api/tasks`, `GET /api/tasks/{id}`, `POST /api/tasks/{id}/cancel` (потрібен токен)
- `GET /api/admin/overview`: активні задачі й черга (потрібен токен з роллю Admin)
- `GET /health`: стан API та його ім'я
- `GET /lb/status`: стан серверів на балансувальнику

## Технології

.NET 10, ASP.NET Core, Entity Framework Core, PostgreSQL, JWT, Task Parallel Library; фронтенд на чистих HTML, CSS та JavaScript.
