import { useEffect, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Gift,
  Lock,
  PlusCircle,
  ShieldAlert,
  Trash2,
  Trophy,
  Wallet,
  Wifi,
  WifiOff,
} from 'lucide-react';

// Encje zgodne z Żelaznym Kontraktem Nazewniczym (BETter.Domain & BETter.Wallet)
interface SelectionItem {
  id: string;
  name: string;
  odds: number;
  isSuspended?: boolean;
}

interface Market {
  id: string;
  name: string;
  isSuspended?: boolean;
  selections: SelectionItem[];
}

interface SportEvent {
  id: string;
  league: string;
  startTime: string;
  homeTeam: string;
  awayTeam: string;
  isLive?: boolean;
  markets: Market[];
}

interface BetSlipItem {
  sportEventId: string;
  marketId: string;
  selectionId: string;
  matchName: string;
  marketName: string;
  selectionName: string;
  odds: number;
}

type SlipType = 'SOLO' | 'AKO';

interface WalletState {
  availableBalance: number;
  frozenBalance: number;
  bonusBalance: number;
}

const TAX_MULTIPLIER = 0.88; // Ustawowy podatek 12% (PU-07 / CouponCalculator)
const MAX_AKO_SELECTIONS = 30;

const FALLBACK_EVENTS: SportEvent[] = [
  {
    id: 'se-101',
    league: 'PKO BP Ekstraklasa',
    startTime: 'Dziś, 18:00',
    homeTeam: 'Górnik Zabrze',
    awayTeam: 'Legia Warszawa',
    markets: [
      {
        id: 'mkt-101-1x2',
        name: '1X2',
        selections: [
          { id: 'sel-101-1', name: '1', odds: 2.65 },
          { id: 'sel-101-x', name: 'X', odds: 3.3 },
          { id: 'sel-101-2', name: '2', odds: 2.55 },
        ],
      },
    ],
  },
  {
    id: 'se-102',
    league: 'PKO BP Ekstraklasa',
    startTime: 'Dziś, 20:30',
    homeTeam: 'Lech Poznań',
    awayTeam: 'Raków Częstochowa',
    markets: [
      {
        id: 'mkt-102-1x2',
        name: '1X2',
        selections: [
          { id: 'sel-102-1', name: '1', odds: 2.2 },
          { id: 'sel-102-x', name: 'X', odds: 3.25 },
          { id: 'sel-102-2', name: '2', odds: 3.15 },
        ],
      },
    ],
  },
  {
    id: 'se-103',
    league: 'Premier League',
    startTime: 'Jutro, 16:00',
    homeTeam: 'Arsenal',
    awayTeam: 'Liverpool',
    markets: [
      {
        id: 'mkt-103-1x2',
        name: '1X2',
        selections: [
          { id: 'sel-103-1', name: '1', odds: 2.4 },
          { id: 'sel-103-x', name: 'X', odds: 3.5 },
          { id: 'sel-103-2', name: '2', odds: 2.8 },
        ],
      },
    ],
  },
  {
    id: 'se-104',
    league: 'Premier League',
    startTime: 'LIVE 64’',
    homeTeam: 'Manchester City',
    awayTeam: 'Aston Villa',
    isLive: true,
    markets: [
      {
        id: 'mkt-104-1x2',
        name: '1X2 (Analiza VAR)',
        isSuspended: true,
        selections: [
          { id: 'sel-104-1', name: '1', odds: 1.55 },
          { id: 'sel-104-x', name: 'X', odds: 4.3 },
          { id: 'sel-104-2', name: '2', odds: 5.4 },
        ],
      },
    ],
  },
  {
    id: 'se-105',
    league: 'UEFA Champions League',
    startTime: 'Środa, 21:00',
    homeTeam: 'Real Madryt',
    awayTeam: 'Bayern Monachium',
    markets: [
      {
        id: 'mkt-105-1x2',
        name: '1X2',
        selections: [
          { id: 'sel-105-1', name: '1', odds: 2.1 },
          { id: 'sel-105-x', name: 'X', odds: 3.65 },
          { id: 'sel-105-2', name: '2', odds: 3.25 },
        ],
      },
    ],
  },
];

export function App() {
  const [wallet, setWallet] = useState<WalletState>({
    availableBalance: 1000.0,
    frozenBalance: 0.0,
    bonusBalance: 50.0,
  });

  const [events, setEvents] = useState<SportEvent[]>([]);
  const [selectedLeague, setSelectedLeague] = useState<string>('Wszystkie');
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isBackendConnected, setIsBackendConnected] = useState<boolean>(false);

  const [slipItems, setSlipItems] = useState<BetSlipItem[]>([]);
  const [grossStakeInput, setGrossStakeInput] = useState<string>('50');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [feedback, setFeedback] = useState<{
    type: 'success' | 'error';
    text: string;
  } | null>(null);

  useEffect(() => {
    const loadEvents = async () => {
      try {
        const response = await fetch('/api/events');
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const data: SportEvent[] = await response.json();
        setEvents(data);
        setIsBackendConnected(true);
      } catch {
        setEvents(FALLBACK_EVENTS);
        setIsBackendConnected(false);
      } finally {
        setIsLoading(false);
      }
    };

    loadEvents();
  }, []);

  const leagues = ['Wszystkie', ...Array.from(new Set(events.map((e) => e.league)))];
  const filteredEvents =
    selectedLeague === 'Wszystkie'
      ? events
      : events.filter((e) => e.league === selectedLeague);

  const handleToggleSelection = (
    sportEvent: SportEvent,
    market: Market,
    selection: SelectionItem
  ) => {
    setFeedback(null);

    if (market.isSuspended || selection.isSuspended) {
      return;
    }

    const existingFromSameEvent = slipItems.find(
      (item) => item.sportEventId === sportEvent.id
    );

    if (existingFromSameEvent && existingFromSameEvent.selectionId === selection.id) {
      setSlipItems((prev) =>
        prev.filter((item) => item.sportEventId !== sportEvent.id)
      );
      return;
    }

    if (existingFromSameEvent) {
      setFeedback({
        type: 'error',
        text: 'Blokada anty-korelacyjna (PU-08): nie można łączyć selekcji z tego samego meczu.',
      });
      return;
    }

    if (slipItems.length >= MAX_AKO_SELECTIONS) {
      setFeedback({
        type: 'error',
        text: `Osiągnięto maksymalny limit ${MAX_AKO_SELECTIONS} zdarzeń na kuponie AKO.`,
      });
      return;
    }

    setSlipItems((prev) => [
      ...prev,
      {
        sportEventId: sportEvent.id,
        marketId: market.id,
        selectionId: selection.id,
        matchName: `${sportEvent.homeTeam} – ${sportEvent.awayTeam}`,
        marketName: market.name,
        selectionName: selection.name,
        odds: selection.odds,
      },
    ]);
  };

  const handleRemoveItem = (sportEventId: string) => {
    setFeedback(null);
    setSlipItems((prev) => prev.filter((item) => item.sportEventId !== sportEventId));
  };

  const slipType: SlipType = slipItems.length > 1 ? 'AKO' : 'SOLO';
  const grossStake = parseFloat(grossStakeInput) || 0;
  const netStake = Number((grossStake * TAX_MULTIPLIER).toFixed(2));
  const totalOdds =
    slipItems.length > 0
      ? Number(slipItems.reduce((acc, item) => acc * item.odds, 1).toFixed(2))
      : 0;
  const potentialWin =
    slipItems.length > 0 ? Number((netStake * totalOdds).toFixed(2)) : 0;

  const handleQuickDeposit = async (amount: number) => {
    try {
      const response = await fetch('/api/wallet/deposit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ amount }),
      });

      if (response.ok) {
        const updated = await response.json();
        if (typeof updated.availableBalance === 'number') {
          setWallet((prev) => ({ ...prev, ...updated }));
          return;
        }
      }
    } catch {
      // Fallback dla trybu offline
    }

    setWallet((prev) => ({
      ...prev,
      availableBalance: Number((prev.availableBalance + amount).toFixed(2)),
    }));
  };

  const handlePlaceBet = async () => {
    setFeedback(null);

    if (slipItems.length === 0) {
      setFeedback({ type: 'error', text: 'Dodaj przynajmniej jedną selekcję do kuponu.' });
      return;
    }
    if (grossStake <= 0) {
      setFeedback({ type: 'error', text: 'Stawka brutto musi być większa od 0 PLN.' });
      return;
    }
    if (grossStake > wallet.availableBalance) {
      setFeedback({
        type: 'error',
        text: 'Niewystarczające środki na Saldzie dostępnym (PU-09 / 5a).',
      });
      return;
    }

    setIsSubmitting(true);

    const requestPayload = {
      stake: grossStake,
      slipType: slipType === 'SOLO' ? 'Solo' : 'Ako',
      items: slipItems.map((item) => ({
        sportEventId: item.sportEventId,
        marketId: item.marketId,
        selectionId: item.selectionId,
        odds: item.odds,
      })),
    };

    try {
      const response = await fetch('/api/slips/place', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(requestPayload),
      });

      if (response.ok) {
        const data = await response.json();
        setWallet((prev) => ({
          availableBalance:
            typeof data.availableBalance === 'number'
              ? data.availableBalance
              : Number((prev.availableBalance - grossStake).toFixed(2)),
          frozenBalance:
            typeof data.frozenBalance === 'number'
              ? data.frozenBalance
              : Number((prev.frozenBalance + grossStake).toFixed(2)),
          bonusBalance:
            typeof data.bonusBalance === 'number'
              ? data.bonusBalance
              : prev.bonusBalance,
        }));
      } else {
        setWallet((prev) => ({
          ...prev,
          availableBalance: Number((prev.availableBalance - grossStake).toFixed(2)),
          frozenBalance: Number((prev.frozenBalance + grossStake).toFixed(2)),
        }));
      }

      setSlipItems([]);
      setFeedback({
        type: 'success',
        text: `Kupon ${slipType} przyjęty (status: OPEN). Zamrożono ${grossStake.toFixed(2)} PLN.`,
      });
    } catch {
      setWallet((prev) => ({
        ...prev,
        availableBalance: Number((prev.availableBalance - grossStake).toFixed(2)),
        frozenBalance: Number((prev.frozenBalance + grossStake).toFixed(2)),
      }));
      setSlipItems([]);
      setFeedback({
        type: 'success',
        text: `Kupon ${slipType} przyjęty (tryb mock). Zamrożono ${grossStake.toFixed(2)} PLN.`,
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-abyss text-white flex flex-col selection:bg-royal selection:text-white">
      {/* Górny pasek: Logo + Status API + 3 subkonta Portfela */}
      <header className="sticky top-0 z-10 bg-card/95 backdrop-blur-md border-b border-dusk-plum px-6 py-3 flex flex-wrap items-center justify-between gap-4 shadow-lg shadow-black/60">
        <div className="flex items-center gap-3">
          <div className="bg-royal text-white font-black px-3 py-1 rounded-lg text-lg tracking-tight border border-royal-border/80 shadow-md shadow-black/70">
            BETter
          </div>
          <span className="text-xs uppercase tracking-wider text-mist hidden md:inline">
            Prototyp #1 • Walking Skeleton
          </span>
          <span
            className={`inline-flex items-center gap-1.5 text-xs px-2.5 py-0.5 rounded-full border ${
              isBackendConnected
                ? 'bg-royal text-white border-royal-border'
                : 'bg-surface text-mist border-dusk-plum'
            }`}
          >
            {isBackendConnected ? (
              <Wifi className="w-3.5 h-3.5 text-purple-200" />
            ) : (
              <WifiOff className="w-3.5 h-3.5 text-mist" />
            )}
            {isBackendConnected ? 'BETter.Api Online' : 'Mock Oferta'}
          </span>
        </div>

        {/* 3 subkonta z encji Wallet */}
        <div className="flex flex-wrap items-center gap-3">
          <div className="bg-surface border border-dusk-plum rounded-lg px-3.5 py-1.5 flex items-center gap-2.5">
            <Wallet className="w-4 h-4 text-purple-300" />
            <div>
              <div className="text-[10px] uppercase tracking-wider text-mist">
                Saldo dostępne
              </div>
              <div className="text-sm font-bold text-white tabular-nums">
                {wallet.availableBalance.toFixed(2)} PLN
              </div>
            </div>
            <button
              onClick={() => handleQuickDeposit(100)}
              title="Szybki depozyt +100 PLN (POST /api/wallet/deposit)"
              className="ml-1 text-mist hover:text-white transition cursor-pointer"
            >
              <PlusCircle className="w-4 h-4" />
            </button>
          </div>

          <div className="bg-surface border border-dusk-plum rounded-lg px-3.5 py-1.5 flex items-center gap-2.5">
            <Lock className="w-4 h-4 text-amber-400" />
            <div>
              <div className="text-[10px] uppercase tracking-wider text-mist">
                Saldo zamrożone (Hold)
              </div>
              <div className="text-sm font-bold text-amber-400 tabular-nums">
                {wallet.frozenBalance.toFixed(2)} PLN
              </div>
            </div>
          </div>

          <div className="bg-surface border border-dusk-plum rounded-lg px-3.5 py-1.5 flex items-center gap-2.5">
            <Gift className="w-4 h-4 text-sky-400" />
            <div>
              <div className="text-[10px] uppercase tracking-wider text-mist">
                Saldo bonusowe
              </div>
              <div className="text-sm font-bold text-sky-300 tabular-nums">
                {wallet.bonusBalance.toFixed(2)} PLN
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Układ 3-kolumnowy */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-4 grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">
        {/* LEWA KOLUMNA: Ligi */}
        <aside className="lg:col-span-3 bg-card border border-dusk-plum rounded-xl p-3 shadow-lg shadow-black/50">
          <div className="flex items-center gap-2 px-2 py-1.5 mb-2 text-xs font-semibold uppercase tracking-wider text-mist">
            <Trophy className="w-4 h-4 text-purple-300" />
            Rozgrywki / Ligi
          </div>
          <nav className="space-y-1.5">
            {leagues.map((league) => {
              const count =
                league === 'Wszystkie'
                  ? events.length
                  : events.filter((e) => e.league === league).length;
              const active = selectedLeague === league;

              return (
                <button
                  key={league}
                  onClick={() => setSelectedLeague(league)}
                  className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm font-medium transition cursor-pointer ${
                    active
                      ? 'bg-royal text-white border border-royal-border shadow-md shadow-black/60 font-semibold'
                      : 'text-mist hover:text-white hover:bg-indigo-slate border border-transparent'
                  }`}
                >
                  <span className="truncate">{league}</span>
                  <span
                    className={`text-xs px-2 py-0.5 rounded-full ml-2 tabular-nums ${
                      active
                        ? 'bg-abyss text-purple-200 border border-royal-border/50'
                        : 'bg-surface text-mist border border-dusk-plum/60'
                    }`}
                  >
                    {count}
                  </span>
                </button>
              );
            })}
          </nav>
        </aside>

        {/* ŚRODKOWA KOLUMNA: Lista SportEvents z rynkami i przyciskami kursów */}
        <section className="lg:col-span-6 space-y-3">
          <div className="flex items-center justify-between px-1">
            <h1 className="text-base font-semibold text-white">
              Oferta wydarzeń ({filteredEvents.length})
            </h1>
            <span className="text-xs text-mist">GET /api/events</span>
          </div>

          {isLoading ? (
            <div className="bg-card border border-dusk-plum rounded-xl p-8 text-center text-mist text-sm">
              Pobieranie oferty z GET /api/events...
            </div>
          ) : (
            filteredEvents.map((sportEvent) => {
              const primaryMarket = sportEvent.markets[0];
              const activeSlipItem = slipItems.find(
                (item) => item.sportEventId === sportEvent.id
              );

              return (
                <div
                  key={sportEvent.id}
                  className="bg-card border border-dusk-plum rounded-xl p-4 hover:border-royal-border/50 transition shadow-md shadow-black/40"
                >
                  <div className="flex items-center justify-between text-xs text-mist mb-2">
                    <span className="font-medium text-purple-300/90">
                      {sportEvent.league}
                    </span>
                    <span
                      className={
                        sportEvent.isLive
                          ? 'text-rose-400 font-semibold flex items-center gap-1.5'
                          : 'text-mist'
                      }
                    >
                      {sportEvent.isLive && (
                        <span className="w-2 h-2 rounded-full bg-rose-500 animate-pulse" />
                      )}
                      {sportEvent.startTime}
                    </span>
                  </div>

                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    <div className="space-y-1">
                      <div className="font-semibold text-white">
                        {sportEvent.homeTeam}
                      </div>
                      <div className="font-semibold text-white">
                        {sportEvent.awayTeam}
                      </div>
                      {primaryMarket?.isSuspended && (
                        <div className="inline-flex items-center gap-1 text-[11px] text-amber-400 pt-1">
                          <ShieldAlert className="w-3.5 h-3.5" />
                          Rynek zawieszony (Suspended Market)
                        </div>
                      )}
                    </div>

                    {primaryMarket && (
                      <div className="grid grid-cols-3 gap-2 sm:w-64 shrink-0">
                        {primaryMarket.selections.map((sel) => {
                          const isSelected = activeSlipItem?.selectionId === sel.id;
                          const isCorrelationLocked = Boolean(
                            activeSlipItem && !isSelected
                          );
                          const isMarketLocked = Boolean(
                            primaryMarket.isSuspended || sel.isSuspended
                          );
                          const disabled = isCorrelationLocked || isMarketLocked;

                          return (
                            <button
                              key={sel.id}
                              disabled={disabled}
                              onClick={() =>
                                handleToggleSelection(sportEvent, primaryMarket, sel)
                              }
                              title={
                                isMarketLocked
                                  ? 'Rynek tymczasowo zablokowany'
                                  : isCorrelationLocked
                                    ? 'Blokada korelacji (PU-08): wybrano już typ z tego meczu'
                                    : `${sel.name} @ ${sel.odds.toFixed(2)}`
                              }
                              className={`py-2 px-2.5 rounded-lg border text-center transition flex flex-col items-center justify-center ${
                                isSelected
                                  ? 'bg-royal text-white border-royal-border ring-1 ring-royal-border/60 font-bold shadow-lg shadow-black/70 cursor-pointer'
                                  : disabled
                                    ? 'bg-abyss/80 text-mist/25 border-dusk-plum/30 cursor-not-allowed'
                                    : 'bg-surface text-white border-dusk-plum hover:bg-indigo-slate hover:border-royal-border/70 cursor-pointer'
                              }`}
                            >
                              <span
                                className={`text-[11px] ${
                                  isSelected ? 'text-purple-200 font-semibold' : 'text-mist'
                                }`}
                              >
                                {sel.name}
                              </span>
                              <span className="text-sm font-bold tabular-nums">
                                {isMarketLocked ? (
                                  <Lock className="w-3.5 h-3.5 my-0.5 text-mist/40" />
                                ) : (
                                  sel.odds.toFixed(2)
                                )}
                              </span>
                            </button>
                          );
                        })}
                      </div>
                    )}
                  </div>
                </div>
              );
            })
          )}
        </section>

        {/* PRAWA KOLUMNA: Panel kuponu (BetSlip) */}
        <aside className="lg:col-span-3 bg-card border border-dusk-plum rounded-xl p-4 sticky top-20 shadow-lg shadow-black/50">
          <div className="flex items-center justify-between border-b border-dusk-plum pb-3 mb-3">
            <div className="flex items-center gap-2">
              <h2 className="font-semibold text-white">Kupon</h2>
              <span className="bg-royal text-white text-xs font-bold px-2 py-0.5 rounded-full border border-royal-border">
                {slipItems.length}
              </span>
              {slipItems.length > 0 && (
                <span className="text-[11px] font-bold px-2 py-0.5 rounded bg-surface text-purple-200 border border-dusk-plum">
                  {slipType}
                </span>
              )}
            </div>

            {slipItems.length > 0 && (
              <button
                onClick={() => {
                  setSlipItems([]);
                  setFeedback(null);
                }}
                className="text-xs text-mist hover:text-white transition cursor-pointer"
              >
                Wyczyść
              </button>
            )}
          </div>

          {slipItems.length === 0 ? (
            <div className="py-8 text-center text-xs text-mist border border-dashed border-dusk-plum rounded-lg mb-4 px-3 bg-surface/60">
              Wybierz kurs z oferty, aby utworzyć kupon SOLO lub AKO.
            </div>
          ) : (
            <div className="space-y-2.5 mb-4 max-h-72 overflow-y-auto pr-1">
              {slipItems.map((item) => (
                <div
                  key={item.sportEventId}
                  className="bg-surface border border-dusk-plum rounded-lg p-2.5 text-xs relative"
                >
                  <button
                    onClick={() => handleRemoveItem(item.sportEventId)}
                    aria-label="Usuń selekcję"
                    className="absolute top-2.5 right-2.5 text-mist hover:text-white transition cursor-pointer"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                  <div className="text-white font-medium pr-5 mb-0.5">
                    {item.matchName}
                  </div>
                  <div className="text-[11px] text-mist mb-1">
                    Rynek: {item.marketName}
                  </div>
                  <div className="flex items-center justify-between text-mist">
                    <span>
                      Typ:{' '}
                      <strong className="text-purple-300">{item.selectionName}</strong>
                    </span>
                    <span className="font-bold text-white tabular-nums">
                      {item.odds.toFixed(2)}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Kalkulator kuponu (PU-07: podatek 12%) */}
          <div className="space-y-2.5 border-t border-dusk-plum pt-3 text-sm">
            <div className="flex justify-between text-mist text-xs">
              <span>Kurs całkowity:</span>
              <span className="font-bold text-white tabular-nums">
                {totalOdds.toFixed(2)}
              </span>
            </div>

            <div>
              <label className="block text-xs text-mist mb-1">
                Stawka brutto (PLN)
              </label>
              <input
                type="number"
                min="1"
                step="1"
                value={grossStakeInput}
                onChange={(e) => setGrossStakeInput(e.target.value)}
                className="w-full bg-surface border border-dusk-plum rounded-lg px-3 py-2 text-right font-bold text-white focus:outline-none focus:border-royal-border tabular-nums"
              />
            </div>

            <div className="flex justify-between text-mist text-xs">
              <span>Stawka netto (po podatku 12%):</span>
              <span className="font-medium text-white tabular-nums">
                {netStake.toFixed(2)} PLN
              </span>
            </div>

            <div className="flex justify-between items-center bg-surface border border-dusk-plum rounded-lg px-3 py-2">
              <span className="text-xs text-mist">Przewidywana wygrana:</span>
              <span className="font-bold text-white tabular-nums">
                {potentialWin.toFixed(2)} PLN
              </span>
            </div>

            {feedback && (
              <div
                className={`p-2.5 rounded-lg text-xs flex items-start gap-2 border ${
                  feedback.type === 'success'
                    ? 'bg-royal/70 text-white border-royal-border'
                    : 'bg-rose-950/80 text-rose-200 border-rose-500/50'
                }`}
              >
                {feedback.type === 'success' ? (
                  <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5 text-purple-200" />
                ) : (
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5 text-rose-300" />
                )}
                <span>{feedback.text}</span>
              </div>
            )}

            <button
              onClick={handlePlaceBet}
              disabled={isSubmitting || slipItems.length === 0}
              className="w-full py-2.5 px-4 rounded-lg font-bold text-sm bg-royal hover:bg-royal-hover disabled:bg-surface disabled:text-mist/30 disabled:border-dusk-plum/40 text-white border border-royal-border transition cursor-pointer disabled:cursor-not-allowed shadow-md shadow-black/60"
            >
              {isSubmitting ? 'Przetwarzanie Hold...' : 'Postaw zakład'}
            </button>
          </div>
        </aside>
      </main>
    </div>
  );
}

export default App;