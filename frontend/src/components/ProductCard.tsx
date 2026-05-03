import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { Product } from '../types';
import { StarRating } from './StarRating';
import { toggleFavorite } from '../hooks/useApi';

interface Props {
  product: Product;
  onFavoriteChange?: (id: number, fav: boolean) => void;
}

export function ProductCard({ product, onFavoriteChange }: Props) {
  const navigate = useNavigate();
  const [fav, setFav] = useState(product.isFavorite);
  const [toggling, setToggling] = useState(false);
  const [imgError, setImgError] = useState(false);

  const handleFav = async (e: React.MouseEvent) => {
    e.stopPropagation();
    if (toggling) return;
    setToggling(true);
    try {
      const newFav = await toggleFavorite(product.id);
      setFav(newFav);
      onFavoriteChange?.(product.id, newFav);
    } finally {
      setToggling(false);
    }
  };

  const pricePerLiter = product.volume
    ? ((product.price / product.volume) * 1000).toFixed(0)
    : null;

  const showImage = product.imageUrl && !imgError;

  return (
    <article className="product-card" onClick={() => navigate(`/product/${product.id}`)}>
      {product.isNewRelease && <span className="badge badge--new">Nyhet</span>}

      <button
        className={`fav-btn ${fav ? 'fav-btn--active' : ''}`}
        onClick={handleFav}
        aria-label={fav ? 'Ta bort favorit' : 'Spara som favorit'}
      >
        {fav ? '♥' : '♡'}
      </button>

      <div className="product-card__image-wrap">
        {showImage ? (
          <img
            src={product.imageUrl!}
            alt={product.name}
            className="product-card__image"
            onError={() => setImgError(true)}
            loading="lazy"
          />
        ) : (
          <div className="product-card__no-image">
            <span className="product-card__no-image-label">Bild saknas</span>
          </div>
        )}
      </div>

      <div className="product-card__header">
        <h3 className="product-card__name">{product.name}</h3>
        {product.subName && <p className="product-card__subname">{product.subName}</p>}
        <p className="product-card__meta">
          {product.country && <span>{product.country}</span>}
          {product.producer && <span>{product.producer}</span>}
        </p>
      </div>

      <div className="product-card__ratings">
        <div className="rating-row">
          <span className="rating-label">AI</span>
          <StarRating value={product.aiRating} />
        </div>
        {product.vivinoRating && (
          <div className="rating-row">
            <span className="rating-label">Vivino</span>
            <span className="vivino-inline">{product.vivinoRating.toFixed(1)} / 5</span>
          </div>
        )}
        <div className="rating-row">
          <span className="rating-label">Prisvärt</span>
          <StarRating value={product.valueRating} color="#7eb87e" />
        </div>
      </div>

      {product.flavorProfile && (
        <div className="product-card__flavors">
          {product.flavorProfile.split(',').slice(0, 3).map(f => (
            <span key={f.trim()} className="flavor-tag">{f.trim()}</span>
          ))}
        </div>
      )}

      <div className="product-card__footer">
        <span className="price">{product.price.toFixed(0)} kr</span>
        {pricePerLiter && <span className="price-per-liter">{pricePerLiter} kr/l</span>}
        <span className="detail-hint">Visa detaljer</span>
      </div>
    </article>
  );
}
