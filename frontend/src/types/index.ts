export interface Product {
  id: number;
  systembolagetId: string;
  name: string;
  subName?: string;
  category?: string;
  subCategory?: string;
  price: number;
  volume?: number;
  country?: string;
  producer?: string;
  description?: string;
  taste?: string;
  alcoholPercentage?: number;
  url?: string;
  imageUrl?: string;
  isNewRelease: boolean;
  fetchedAt: string;
  // AI
  aiRating?: number;
  valueRating?: number;
  aiSummary?: string;
  flavorProfile?: string;
  aiAnalyzedAt?: string;
  // Vivino
  vivinoRating?: number;
  vivinoReviewCount?: number;
  vivinoUrl?: string;
  vivinoFetchedAt?: string;
  // Användare
  isFavorite: boolean;
}

export interface ProductsResponse {
  total: number;
  page: number;
  pageSize: number;
  items: Product[];
}

export interface Stats {
  total: number;
  analyzed: number;
  newReleases: number;
  favorites: number;
  withVivino: number;
  avgRating: number;
}

export interface Filters {
  category?: string;
  subCategory?: string;
  minPrice?: number;
  maxPrice?: number;
  minRating?: number;
  exactRating?: number;
  newOnly?: boolean;
  favoritesOnly?: boolean;
  sort: string;
}
