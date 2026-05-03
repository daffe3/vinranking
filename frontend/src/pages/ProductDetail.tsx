import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useProduct, toggleFavorite } from '../hooks/useApi';
import { StarRating } from '../components/StarRating';
import './ProductDetail.css';

export function ProductDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { product, loading, error } = useProduct(Number(id));
  const [fav, setFav] = useState<boolean | null>(null);
  const [imgError, setImgError] = useState(false);

  if (loading) return (
    <div className="detail-loading">
      <div className="spinner" />
      <p>Hämtar vin...</p>
    </div>
  );

  if (error || !product) return (
    <div className="detail-error">
      <p>Kunde inte hitta vinet.</p>
      <button onClick={() => navigate(-1)}>Tillbaka</button>
    </div>
  );

  const isFav = fav !== null ? fav : product.isFavorite;
  const pricePerLiter = product.volume
    ? ((product.price / product.volume) * 1000).toFixed(0)
    : null;

  const handleFav = async () => {
    const next = await toggleFavorite(product.id);
    setFav(next);
  };

  const combinedScore =
    product.aiRating && product.vivinoRating
      ? ((product.aiRating / 5) * 0.5 + (product.vivinoRating / 5) * 0.5) * 5
      : null;

  const showImage = product.imageUrl && !imgError;

  return (
    <div className="detail-page">
      <div className="detail-bg" />
      <div className="detail-container">
        <button className="back-btn" onClick={() => navigate(-1)}>
          Tillbaka till listan
        </button>

        <div className="detail-layout">
          <aside className="detail-sidebar">
            <div className="detail-image-wrap">
              {showImage ? (
                <img
                  src={product.imageUrl!}
                  alt={product.name}
                  className="detail-image"
                  onError={() => setImgError(true)}
                />
              ) : (
                <div className="detail-image-placeholder">
                  <span>Bild saknas</span>
                </div>
              )}
            </div>

            <div className="detail-quick-facts">
              <div className="quick-fact">
                <span className="qf-label">Pris</span>
                <span className="qf-value price">{product.price.toFixed(0)} kr</span>
              </div>
              {pricePerLiter && (
                <div className="quick-fact">
                  <span className="qf-label">Per liter</span>
                  <span className="qf-value">{pricePerLiter} kr</span>
                </div>
              )}
              {product.volume && (
                <div className="quick-fact">
                  <span className="qf-label">Volym</span>
                  <span className="qf-value">{product.volume / 100} cl</span>
                </div>
              )}
              {product.alcoholPercentage && (
                <div className="quick-fact">
                  <span className="qf-label">Alkohol</span>
                  <span className="qf-value">{product.alcoholPercentage}%</span>
                </div>
              )}
              {product.country && (
                <div className="quick-fact">
                  <span className="qf-label">Land</span>
                  <span className="qf-value">{product.country}</span>
                </div>
              )}
              {product.producer && (
                <div className="quick-fact">
                  <span className="qf-label">Producent</span>
                  <span className="qf-value">{product.producer}</span>
                </div>
              )}
              {product.subCategory && (
                <div className="quick-fact">
                  <span className="qf-label">Typ</span>
                  <span className="qf-value">{product.subCategory}</span>
                </div>
              )}
            </div>

            <div className="detail-actions">
              <button
                className={`fav-btn-large ${isFav ? 'fav-btn-large--active' : ''}`}
                onClick={handleFav}
              >
                {isFav ? 'Ta bort favorit' : 'Spara som favorit'}
              </button>
              {product.url && (
                <a href={product.url} target="_blank" rel="noopener noreferrer" className="sb-btn">
                  Köp på Systembolaget
                </a>
              )}
              {product.vivinoUrl && (
                <a href={product.vivinoUrl} target="_blank" rel="noopener noreferrer" className="ct-btn">
                  Se på CellarTracker
                </a>
              )}
            </div>
          </aside>

          <main className="detail-main">
            <div className="detail-header">
              {product.isNewRelease && <span className="badge badge--new">Nyhet</span>}
              {product.category && <span className="detail-category">{product.category}</span>}
              <h1 className="detail-title">{product.name}</h1>
              {product.subName && <p className="detail-subtitle">{product.subName}</p>}
            </div>

            <section className="detail-ratings">
              <h2 className="section-title">Betyg</h2>
              <div className="ratings-grid">
                <div className="rating-card">
                  <span className="rating-card__label">AI-betyg</span>
                  <StarRating value={product.aiRating} size="md" />
                  <span className="rating-card__num">{product.aiRating ?? '–'} / 5</span>
                </div>
                <div className="rating-card">
                  <span className="rating-card__label">Prisvärdhet</span>
                  <StarRating value={product.valueRating} size="md" color="#7eb87e" />
                  <span className="rating-card__num">{product.valueRating ?? '–'} / 5</span>
                </div>
                <a
                  href={`https://www.vivino.com/search/wines?q=${encodeURIComponent((product.name || '') + ' ' + (product.subName || ''))}`}
                  target="_blank"
                  rel="noreferrer"
                  className="rating-card rating-card--vivino rating-card--link"
                  title="Sök på Vivino"
                >
                  <span className="rating-card__label">Vivino</span>
                  {product.vivinoRating ? (
                    <>
                      <span className="vivino-score">{product.vivinoRating.toFixed(1)}</span>
                      {product.vivinoReviewCount && (
                        <span className="vivino-count">{product.vivinoReviewCount.toLocaleString('sv')} omdömen</span>
                      )}
                    </>
                  ) : (
                    <span className="vivino-search-text">Sök på Vivino →</span>
                  )}
                </a>
                {combinedScore && (
                  <div className="rating-card rating-card--combined">
                    <span className="rating-card__label">Kombinerat</span>
                    <span className="combined-score">{combinedScore.toFixed(1)}</span>
                    <span className="rating-card__num">AI + Vivino</span>
                  </div>
                )}
              </div>
            </section>

            {product.flavorProfile && (
              <section className="detail-section">
                <h2 className="section-title">Smakprofil</h2>
                <div className="flavor-tags">
                  {product.flavorProfile.split(',').map(f => (
                    <span key={f.trim()} className="flavor-tag-large">{f.trim()}</span>
                  ))}
                </div>
              </section>
            )}

            {product.aiSummary && (
              <section className="detail-section">
                <h2 className="section-title">
                  <span className="ai-badge">AI</span> Sammanfattning
                </h2>
                <p className="detail-summary">{product.aiSummary}</p>
              </section>
            )}

            {product.description && (
              <section className="detail-section">
                <h2 className="section-title">Systembolagets beskrivning</h2>
                <p className="detail-desc">{product.description}</p>
              </section>
            )}

            {product.taste && product.taste.includes('|||') === false && (
              <section className="detail-section">
                <h2 className="section-title">Smak</h2>
                <p className="detail-desc">{product.taste}</p>
              </section>
            )}
            {product.taste && product.taste.includes('|||') && (
              <section className="detail-section">
                <h2 className="section-title">Smak</h2>
                <p className="detail-desc">{product.taste.split('|||')[0]}</p>
              </section>
            )}

            <p className="detail-meta-footer">
              Hämtad {new Date(product.fetchedAt).toLocaleDateString('sv-SE')}
              {product.aiAnalyzedAt && ` · AI-analyserad ${new Date(product.aiAnalyzedAt).toLocaleDateString('sv-SE')}`}
              {product.vivinoFetchedAt && ` · Vivino ${new Date(product.vivinoFetchedAt).toLocaleDateString('sv-SE')}`}
            </p>
          </main>
        </div>
      </div>
    </div>
  );
}
