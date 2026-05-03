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

### 1. Skapa GitHub-repo
```bash
git init
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/DITT-ANVÄNDARNAMN/vinranking.git
git push -u origin main
```

### 2. Backend på Render
1. render.com → New → Web Service
2. Koppla GitHub-repot
3. **Root directory:** `backend`
4. **Dockerfile path:** `Dockerfile.render`
5. **Miljövariabler att sätta i Render:**
   - `DATABASE_URL` → från Render PostgreSQL (se nedan)
   - `SYSTEMBOLAGET_API_KEY` → din API-nyckel
   - `AI_PROVIDER` → `openrouter`
   - `AI_API_KEY` → din OpenRouter-nyckel
   - `FRONTEND_URL` → din Vercel-URL (lägg till efter Vercel-deploy)

6. Render → New → PostgreSQL → kopiera "Internal Database URL" till `DATABASE_URL`

### 3. Frontend på Vercel
1. vercel.com → New Project → importera GitHub-repot
2. **Root directory:** `frontend`
3. **Miljövariabler:**
   - `VITE_API_URL` → din Render-URL (t.ex. `https://vinranking.onrender.com`)

### 4. Lägg till Vercel-URL i Render
Gå tillbaka till Render → Environment → lägg till `FRONTEND_URL` = din Vercel-URL

## Miljövariabler (.env lokalt)
```
SYSTEMBOLAGET_API_KEY=cfc702aed3094c86b92d6d4ff7a54c84
AI_PROVIDER=openrouter
AI_API_KEY=sk-or-v1-...
```
