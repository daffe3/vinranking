import { Filters } from '../types';

interface FilterBarProps {
  filters: Filters;
  onChange: (filters: Filters) => void;
}

interface CategoryOption {
  label: string;
  category?: string;
  subCategory?: string;
}

interface PriceOption {
  label: string;
  min?: number;
  max?: number;
}

const CATEGORIES: CategoryOption[] = [
  { label: 'Alla' },
  { label: 'Rött vin',     category: 'Rött vin' },
  { label: 'Vitt vin',     category: 'Vitt vin' },
  { label: 'Mousserande',  category: 'Mousserande vin' },
];

const PRICE_OPTIONS: PriceOption[] = [
  { label: 'Alla priser' },
  { label: 'Under 100 kr', max: 100 },
  { label: '100–150 kr',   min: 100, max: 150 },
  { label: '150–200 kr',   min: 150, max: 200 },
  { label: '200–300 kr',   min: 200, max: 300 },
  { label: '300–500 kr',   min: 300, max: 500 },
];

const SORT_OPTIONS = [
  { label: 'Bäst AI-betyg',  value: 'rating' },
  { label: 'Bäst Vivino',    value: 'vivino' },
  { label: 'Mest prisvärt',  value: 'value' },
  { label: 'Lägst pris',     value: 'price' },
  { label: 'Högst pris',     value: 'price_desc' },
];

export function FilterBar({ filters, onChange }: FilterBarProps) {
  const set = (partial: Partial<Filters>) =>
    onChange({ ...filters, ...partial });

  const isActiveCat = (cat: CategoryOption) => {
    if (!cat.category) return !filters.category;
    if (cat.subCategory) {
      return filters.category === cat.category && filters.subCategory === cat.subCategory;
    }
    return filters.category === cat.category && !filters.subCategory;
  };

  const activePrice = PRICE_OPTIONS.find(
    p => p.min === filters.minPrice && p.max === filters.maxPrice
  ) ?? PRICE_OPTIONS[0];

  return (
    <aside className="filter-bar">
      <section className="filter-section">
        <h3 className="filter-label">KATEGORI</h3>
        <div className="pill-group">
          {CATEGORIES.map(cat => (
            <button
              key={cat.label}
              className={`pill ${isActiveCat(cat) ? 'pill--active' : ''}`}
              onClick={() => set({
                category: cat.category,
                subCategory: cat.subCategory,
              })}
            >
              {cat.label}
            </button>
          ))}
        </div>
      </section>

      <section className="filter-section">
        <h3 className="filter-label">MAXPRIS</h3>
        <div className="pill-group">
          {PRICE_OPTIONS.map(opt => (
            <button
              key={opt.label}
              className={`pill ${activePrice.label === opt.label ? 'pill--active' : ''}`}
              onClick={() => set({ minPrice: opt.min, maxPrice: opt.max })}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </section>

      <section className="filter-section">
        <h3 className="filter-label">AI-BETYG</h3>
        <div className="pill-group">
          {([1,2,3,4,5] as const).map(r => (
            <button
              key={r}
              className={`pill ${filters.exactRating === r ? 'pill--active' : ''}`}
              onClick={() => set({ exactRating: filters.exactRating === r ? undefined : r, minRating: undefined })}
            >
              {'★'.repeat(r)}{'☆'.repeat(5-r)}
            </button>
          ))}

        </div>
      </section>

      <section className="filter-section">
        <h3 className="filter-label">SORTERA</h3>
        <div className="pill-group">
          {SORT_OPTIONS.map(opt => (
            <button
              key={opt.value}
              className={`pill ${filters.sort === opt.value ? 'pill--active' : ''}`}
              onClick={() => set({ sort: opt.value })}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </section>

      <section className="filter-section">
        <h3 className="filter-label">VISA</h3>
        <div className="pill-group">
          <button
            className={`pill ${filters.newOnly ? 'pill--active' : ''}`}
            onClick={() => set({ newOnly: !filters.newOnly })}
          >
            Nyheter
          </button>
          <button
            className={`pill ${filters.favoritesOnly ? 'pill--active' : ''}`}
            onClick={() => set({ favoritesOnly: !filters.favoritesOnly })}
          >
            Favoriter
          </button>
        </div>
      </section>
    </aside>
  );
}
