import { useEffect, useState } from 'react';
import { request } from '../../services/api/client';

const photoCache = new Map();

export default function EmployeePhoto({ src, alt = 'Profile photo', initials, className = '', ...props }) {
  const [resolved, setResolved] = useState(() => (src && photoCache.has(src) ? photoCache.get(src) : null));
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    setHasError(false);
    if (!src) {
      setResolved(null);
      return;
    }

    if (!src.startsWith('/api/employees/')) {
      setResolved(src);
      return;
    }

    if (photoCache.has(src)) {
      setResolved(photoCache.get(src));
      return;
    }

    const controller = new AbortController();
    request(src, { responseType: 'blob', signal: controller.signal })
      .then((blob) => {
        if (controller.signal.aborted) return;
        if (blob && blob.size > 0 && blob.type.startsWith('image/')) {
          const blobUrl = URL.createObjectURL(blob);
          photoCache.set(src, blobUrl);
          setResolved(blobUrl);
        } else {
          setHasError(true);
        }
      })
      .catch(() => {
        setHasError(true);
      });

    return () => {
      controller.abort();
    };
  }, [src]);

  if (!src || hasError || (!resolved && src.startsWith('/api/employees/'))) {
    if (initials) {
      return (
        <div className={`employee-avatar-circle ${className}`} {...props}>
          {initials}
        </div>
      );
    }
    return (
      <span className={`employee-avatar-fallback ${className}`} aria-label={alt} {...props}>
        {alt ? alt.charAt(0).toUpperCase() : '●'}
      </span>
    );
  }

  return (
    <img
      {...props}
      src={resolved}
      alt={alt}
      className={className}
      onError={() => setHasError(true)}
      referrerPolicy="no-referrer"
    />
  );
}

