import { Link } from 'react-router';
import { portalLinkProps } from '../utils/portals';
export default function PortalLink({ to, ...props }) {
 return <Link {...props} {...portalLinkProps(to)} />;
}
