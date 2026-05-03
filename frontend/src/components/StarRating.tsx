interface StarRatingProps {
  value?: number;
  max?: number;
  size?: 'sm' | 'md';
  color?: string;
}

export function StarRating({ value, max = 5, size = 'sm', color = '#c9a84c' }: StarRatingProps) {
  if (!value) return <span className="no-rating">Ej betygsatt</span>;
  return (
    <span className={`stars stars--${size}`} aria-label={`${value} av ${max}`}>
      {Array.from({ length: max }, (_, i) => (
        <span
          key={i}
          className="star"
          style={{ color: i < value ? color : 'rgba(255,255,255,0.12)' }}
        >
          &#9733;
        </span>
      ))}
    </span>
  );
}
