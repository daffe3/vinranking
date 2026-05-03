import { useState, useEffect, useCallback } from 'react';
import type { Product, ProductsResponse, Stats, Filters } from '../types';

const API = import.meta.env.VITE_API_URL
  ? `${import.meta.env.VITE_API_URL}/api`
  : '/api';

export function useProducts(filters: Filters, page: number) {
  const [data, setData] = useState<ProductsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (filters.category) params.set('category', filters.category);
      if (filters.subCategory) params.set('subCategory', filters.subCategory);
      if (filters.minPrice) params.set('minPrice', String(filters.minPrice));
      if (filters.maxPrice) params.set('maxPrice', String(filters.maxPrice));
      if (filters.exactRating) params.set('exactRating', String(filters.exactRating));
      else if (filters.minRating) params.set('minRating', String(filters.minRating));
      if (filters.newOnly) params.set('newOnly', 'true');
      if (filters.favoritesOnly) params.set('favoritesOnly', 'true');
      params.set('sort', filters.sort);
      params.set('page', String(page));
      params.set('pageSize', '24');
      const res = await window.fetch(`${API}/products?${params}`);
      if (!res.ok) throw new Error('Kunde inte hämta produkter');
      setData(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Okänt fel');
    } finally {
      setLoading(false);
    }
  }, [filters, page]);

  useEffect(() => { fetchData(); }, [fetchData]);
  return { data, loading, error, refetch: fetchData };
}

export function useProduct(id: number) {
  const [product, setProduct] = useState<Product | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    window.fetch(`${API}/products/${id}`)
      .then(r => { if (!r.ok) throw new Error('Inte hittad'); return r.json(); })
      .then(setProduct)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, [id]);

  return { product, loading, error };
}

export function useStats() {
  const [stats, setStats] = useState<Stats | null>(null);
  useEffect(() => {
    window.fetch(`${API}/stats`).then(r => r.json()).then(setStats).catch(console.error);
  }, []);
  return stats;
}

export async function toggleFavorite(id: number): Promise<boolean> {
  const res = await window.fetch(`${API}/products/${id}/favorite`, { method: 'PATCH' });
  const data = await res.json();
  return data.isFavorite;
}

export async function triggerRefresh(): Promise<void> {
  await window.fetch(`${API}/admin/refresh`, { method: 'POST' });
}
