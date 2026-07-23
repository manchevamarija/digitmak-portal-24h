# DigitMak Portal V1 — техничко предавање

## Преглед на проектот

DigitMak Portal е веб-платформа наменета за:

- јавно прикажување услуги и информации;
- повеќејазична содржина;
- контакт барања;
- регистрација и најава;
- управување со организации;
- покани и лични претплати;
- help-desk тикети;
- разговор во реално време;
- закажување состаноци;
- известувања;
- административна обработка;
- извештаи и audit log.

Backend-от е изработен со .NET Web API, Entity Framework Core и PostgreSQL.

Frontend-от е изработен со React, TypeScript и Vite.

## Структура на проектот

```text
backend/      Backend API и тестови
frontend/     React frontend
deploy/       Production и deployment скрипти
docs/         Техничка документација
```

Главниот solution фајл е:

```text
DigitMakPortal.slnx
```

## Локално стартување на Windows

### Backend

Стартувај:

```text
START-BACKEND.cmd
```

Backend API ќе биде достапен на:

```text
http://localhost:5241
```

### Frontend

Стартувај:

```text
START-FRONTEND.cmd
```

Frontend апликацијата ќе биде достапна на:

```text
http://localhost:5173
```

За истовремено стартување на backend и frontend може да се користи:

```text
START-FULL-SYSTEM.cmd
```

## Локални demo профили

Овие профили автоматски се креираат во Development околината.

### Администратор

- Email: `admin@digitmak.mk`
- Лозинка: `DigitMak!2026Admin`

### Клиент

- Email: `client@digitmak.mk`
- Лозинка: `DigitMak!2026Client`

Demo клиентот има одобрена организација и активна претплата за локално тестирање на клиентските функционалности.

Овие податоци се наменети само за локален развој и демонстрација.

Во production околина мора да се постават нови администраторски податоци.

## Production конфигурација

Production вредностите не треба да се запишуваат директно во кодот или да се зачувуваат во Git.

Фајлот `.env.production` може да се генерира со:

```powershell
powershell -File deploy/setup-production.ps1
```

Скриптата бара или генерира вредности за:

- PostgreSQL база, корисник и лозинка;
- JWT signing key;
- Brevo SMTP податоци;
- production administrator;
- јавен домен;
- Let's Encrypt email;
- upload storage;
- ClamAV конфигурација.

Фајлот `.env.production` треба да постои само на серверот и не треба да се испраќа преку Git, email или chat.

## Потребни production вредности

| Вредност | Опис |
|---|---|
| `POSTGRES_DB` | Име на PostgreSQL базата |
| `POSTGRES_USER` | PostgreSQL корисник |
| `POSTGRES_PASSWORD` | Силна лозинка за базата |
| `JWT_SIGNING_KEY` | Таен клуч со најмалку 64 знаци |
| `BREVO_SMTP_HOST` | Brevo SMTP host |
| `BREVO_SMTP_PORT` | Brevo SMTP port |
| `BREVO_SMTP_USERNAME` | Brevo SMTP корисник |
| `BREVO_SMTP_PASSWORD` | Brevo SMTP лозинка или API key |
| `BREVO_FROM_EMAIL` | Email адреса од која се испраќаат пораките |
| `ADMIN_BOOTSTRAP_EMAIL` | Почетен production администратор |
| `ADMIN_BOOTSTRAP_PASSWORD` | Силна администраторска лозинка |
| `PORTAL_DOMAIN` | Production домен |
| `APP_PUBLIC_URL` | Целосна HTTPS адреса на порталот |
| `LETSENCRYPT_EMAIL` | Email за TLS сертификат |
| `UPLOADS_ROOT` | Патека за зачувување прикачени фајлови |

## Проверка на production конфигурацијата

На Windows:

```powershell
powershell -File deploy/validate-production.ps1
```

На Linux:

```bash
sh deploy/validate-production.sh
```

Проверката потврдува дека:

- сите задолжителни вредности постојат;
- нема останати `CHANGE_ME` вредности;
- JWT клучот е доволно долг;
- администраторската лозинка е доволно силна;
- јавната адреса користи HTTPS;
- доменот е правилно конфигуриран.

## Production стартување

Откако ќе биде создаден `.env.production`, системот може да се стартува со:

```bash
docker compose -f docker-compose.production.yml --env-file .env.production up -d --build
```

Production Docker околината содржи:

- PostgreSQL;
- backend API;
- frontend;
- Nginx;
- ClamAV;
- Certbot.

## DNS и TLS

Пред активирање на TLS:

1. Постави DNS `A` запис за production доменот кон IP адресата на серверот.
2. Провери дека доменот правилно покажува кон VM.
3. Постави ги `PORTAL_DOMAIN` и `LETSENCRYPT_EMAIL`.
4. Изврши:

```bash
sh deploy/init-tls.sh
```

TLS сертификатот се издава преку Let's Encrypt.

## Проверка по deployment

По стартувањето провери:

- дали frontend-от се отвора;
- дали `/health` endpoint-от враќа успешен одговор;
- регистрација и најава;
- admin најава;
- организациски workflow;
- контакт барање;
- испраќање email;
- тикети и разговор;
- прикачување фајлови;
- состаноци;
- нотификации;
- извештаи;
- backup и restore.

## Надворешни работи

Следните работи не се дел од source code и мора да бидат обезбедени од сопственикот на системот или систем-администраторот:

- Linux VM или друг production сервер;
- DNS пристап;
- production домен;
- Brevo SMTP сметка;
- production лозинки и тајни;
- TLS активирање;
- backup локација;
- финално правно одобрени текстови;
- политика за приватност и услови за користење;
- финална user-acceptance проверка.

## Документација

Дополнителната документација се наоѓа во `docs`:

- `architecture.md` — архитектура и структура на системот;
- `operations.md` — production, deployment, backup и restore;
- `EXTERNAL-INTEGRATIONS.md` — надворешни интеграции;
- `reference/` — техничката спецификација на проектот.

## Безбедност

Во Git не треба да се зачувуваат:

- production лозинки;
- JWT signing key;
- SMTP credentials;
- `.env.production`;
- API keys;
- приватни сертификати;
- backup фајлови;
- реални кориснички податоци.
