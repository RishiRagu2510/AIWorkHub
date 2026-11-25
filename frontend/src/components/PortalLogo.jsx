import { activePortal } from '../utils/portals';
export default function PortalLogo({ portal = activePortal }) {
  const src = `/portals/${portal}.svg`;
  return (
    <img
      className="portal-brand-mark"
      src={src}
      width="40"
      height="40"
      alt=""
      aria-hidden="true"
      onError={(e) => {
        if (e.currentTarget.src !== '/brand.svg') {
          e.currentTarget.src = '/brand.svg';
        }
      }}
    />
  );
}
