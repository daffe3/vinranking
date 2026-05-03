# VinRanking

AI-betygsatt vinranking från Systembolagets tillfälliga sortiment.

## Tech stack
- **Frontend:** React 18 + TypeScript + Vite → Vercel
- **Backend:** .NET 8 Minimal API → Render
- **Databas:** PostgreSQL (produktion) / SQLite (lokalt)
- **AI:** OpenRouter (Llama/Mistral)

## Lokal utveckling

```bash
cp .env.example .env
# Fyll i dina värden i .env

docker compose up --build
# Öppna http://localhost:3001
```

## Deploy till produktion


