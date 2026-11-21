# BETter

Webowy system zakładów bukmacherskich czasu rzeczywistego realizowany w architekturze **Modularnego Monolitu** z podziałem na warstwy **Inside / Outside (Ports & Adapters)**.

## Struktura repozytorium

```text
BETter/
├── Backend/                        # Solucja .NET 10 (BETter.sln)
│   ├── BETter.Domain/              # Inside: czysta logika bukmacherska, porty (zero zależności)
│   ├── BETter.Wallet/              # Inside: logika portfela, subkont i Ledgera (-> Domain)
│   ├── BETter.Infrastructure/      # Outside: EF Core, PostgreSQL, adaptery repozytoriów (-> Domain, Wallet)
│   └── BETter.Api/                 # Outside: ASP.NET Core Minimal API, SignalR, DI (-> Domain, Wallet, Infrastructure)
└── Frontend/                       # Outside: SPA w React + TypeScript + Vite + Tailwind
```

## Wymagania środowiskowe

- **.NET 10 SDK** (`net10.0`)
- **Node.js** (LTS) + `npm`
- **Docker** (PostgreSQL dla warstwy `BETter.Infrastructure`)

## Szybki start

### Backend (.NET 10)
```powershell
dotnet build Backend/BETter.sln
dotnet run --project Backend/BETter.Api
```
Domyślny adres lokalny API: `http://localhost:5000` (health check: `GET /api/health`).

### Frontend (Vite + React)
```powershell
cd Frontend
npm install
npm run dev
```
Domyślny adres lokalny UI: `http://localhost:5173` (proxy `/api` -> `http://localhost:5000`).

## Zasady pracy z repozytorium (Git Workflow)

- Gałąź `main` jest chroniona — **nie pushujemy bezpośrednio na `main`**.
- Pracujemy na dedykowanych gałęziach (`feat/...`, `fix/...`, `chore/...`) i otwieramy **Pull Request** do `main` (wymagany Code Review).
