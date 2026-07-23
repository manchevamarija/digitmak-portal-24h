# DigitMak Portal V1 — предавање

## Што е завршено во V19

Проектот ја содржи V1 функционалноста од техничката спецификација: јавен повеќејазичен портал, DMA контакт-формулар со шесте експлицитни интерни категории, регистрација и најава, организации и членства, покани и претплати, тикети со разговор, прилози и системски настани, состаноци, role-scoped staff обработка, администрација со целосен DMA detail, содржини, поставки, детални извештаи, audit log, нотификации и production основа. V19 ги содржи точните `EmailVerifiedAt` и `Status` полиња од PDF, PostgreSQL миграции, role-aware секција „Тикети“, директно отворање избран клиентски тикет, одделни UTF-8 CSV и форматирани Excel извештаи и последните responsive/UI поправки.

Backend-от е организиран како modular monolith со `Domain`, `Application`, `Infrastructure` и функционални `Modules`. Frontend-от е поделен на `app`, `pages`, `features`, `components` и `shared`.

## Локално стартување на Windows

1. Отвори ја распакуваната главна папка.
2. Двоен клик на `START-BACKEND.cmd`.
3. Двоен клик на `START-FRONTEND.cmd`.
4. Во прелистувач отвори `http://localhost:5173`.

Локален development администратор:

- `admin@digitmak.mk`
- `DigitMak!2026Admin`

Овие податоци не смеат да останат во реална production околина.

## Production вредности

Сите потребни имиња се во `.env.production.example`. Направи копија со име `.env.production` само на серверот и замени ги сите `CHANGE_ME` вредности. Овој фајл е исклучен од Git и не смее да се испраќа по е-пошта или chat.

| Вредност | Од каде се добива |
|---|---|
| Brevo SMTP корисник/лозинка | Сопственикот на Brevo сметката |
| PostgreSQL лозинка | Ја генерира систем-администраторот |
| JWT signing key | Нов случаен таен клуч, најмалку 64 знаци |
| Admin e-mail/лозинка | Овластен DigitMak администратор |
| VM IP и SSH пристап | Hosting или систем-администратор |
| Production domain/DNS | Лицето што управува со избраниот домен; се внесува во `PORTAL_DOMAIN` |
| TLS сертификат | Let's Encrypt откако DNS ќе покажува кон VM |

## Надворешни работи што не можат да бидат вградени во ZIP

- реален DNS запис и Linux VM пристап;
- production SMTP, PostgreSQL, JWT и admin тајни;
- TLS издавање пред DNS да биде активен;
- финални текстови, преводи, политика за приватност и правно одобрување;
- финална user-acceptance проверка со DigitMak тимот.

## Проверка пред deployment

1. Копирај `.env.production.example` во `.env.production` на VM.
2. Замени ги сите `CHANGE_ME` вредности.
3. На Linux VM изврши `sh deploy/validate-production.sh` (Windows проверка: `powershell -File deploy/validate-production.ps1`).
4. Постави DNS `A` запис `portal` кон VM IP.
5. Изврши `deploy/init-tls.sh`.
6. Стартувај `docker compose -f docker-compose.production.yml --env-file .env.production up -d --build`.
7. Инсталирај ги `deploy/systemd` units за автоматско стартување, дневен backup и TLS renewal.
8. Провери `/health`, најава, e-mail, upload, ticket chat, состанок и backup/restore.

Тековната строга споредба со PDF-от е во `docs/V1-PDF-COMPLIANCE-2026-07-22.md`, а последните промени се во `docs/V19-FINAL-COMPLETION-2026-07-22.md`. Формалното одобрување на изгледот се евидентира во `docs/BRANDING-APPROVAL.md`, а границата за надворешните интеграции е во `docs/EXTERNAL-INTEGRATIONS.md`.
