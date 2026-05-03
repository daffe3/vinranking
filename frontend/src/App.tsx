import { useState, useCallback } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import type { Filters } from './types';
import { useProducts, useStats, triggerRefresh } from './hooks/useApi';
import { ProductCard } from './components/ProductCard';
import { FilterBar } from './components/FilterBar';
import { ProductDetail } from './pages/ProductDetail';
import './App.css';

const DEFAULT_FILTERS: Filters = { sort: 'rating' };

function ProductList() {
  const [filters, setFilters] = useState<Filters>(DEFAULT_FILTERS);
  const [page, setPage] = useState(1);
  const [refreshing, setRefreshing] = useState(false);

  const { data, loading, error } = useProducts(filters, page);
  const stats = useStats();

  const handleFilterChange = useCallback((f: Filters) => {
    setFilters(f);
    setPage(1);
  }, []);

  const handleRefresh = async () => {
    if (refreshing) return;
    setRefreshing(true);
    try { await triggerRefresh(); window.location.reload(); }
    finally { setRefreshing(false); }
  };

  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 0;

  return (
    <div className="app">
      <header className="app-header">
        <div className="header-inner">
          <div className="logo">
            <div>
              <h1 className="logo-title">VinRanking</h1>
              <p className="logo-sub">AI-betygsatt · Systembolaget</p>
            </div>
          </div>
          {stats && (
            <div className="stats-row">
              <div className="stat">
                <span className="stat-num">{stats.total.toLocaleString('sv')}</span>
                <span className="stat-label">viner</span>
              </div>
              <div className="stat">
                <span className="stat-num">{stats.analyzed.toLocaleString('sv')}</span>
                <span className="stat-label">AI-analyserade</span>
              </div>
              <div className="stat">
                <span className="stat-num">{stats.newReleases?.toLocaleString('sv') ?? 0}</span>
                <span className="stat-label">nyheter</span>
              </div>
              <div className="stat">
                <span className="stat-num">{stats.withVivino?.toLocaleString('sv') ?? 0}</span>
                <span className="stat-label">med ext. betyg</span>
              </div>
            </div>
          )}
          <button className="refresh-btn" onClick={handleRefresh} disabled={refreshing}>
            {refreshing ? 'Uppdaterar...' : 'Uppdatera data'}
          </button>
        </div>
      </header>

      <div className="app-body">
        <FilterBar filters={filters} onChange={handleFilterChange} />
        <main className="main-content">
          {loading && (
            <div className="loading">
              <div className="spinner" />
              <p>Hämtar viner...</p>
            </div>
          )}
          {error && (
            <div className="error-box">
              <p>{error}</p>
              <p className="error-hint">Är backend igång? Kör <code>dotnet run</code> i backend-mappen.</p>
            </div>
          )}
          {!loading && !error && data && (
            <>
              <div className="results-header">
                <p className="results-count">{data.total.toLocaleString('sv')} viner hittade</p>
              </div>
              {data.items.length === 0 ? (
                <div className="empty"><p>Inga viner matchar dina filter.</p></div>
              ) : (
                <div className="product-grid">
                  {data.items.map(product => (
                    <ProductCard key={product.id} product={product} />
                  ))}
                </div>
              )}
              {totalPages > 1 && (
                <div className="pagination">
                  <button disabled={page === 1} onClick={() => setPage(p => p - 1)}>Föregående</button>
                  <span>{page} / {totalPages}</span>
                  <button disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>Nästa</button>
                </div>
              )}
            </>
          )}
        </main>
      </div>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<ProductList />} />
        <Route path="/product/:id" element={<ProductDetail />} />
      </Routes>
    </BrowserRouter>
  );
}
