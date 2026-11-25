import { mountPortal } from '../../src/bootstrap';
import { routes } from './routes';
// This portal ships only its own pages. A path that belongs to another portal is sent to that portal's own site
// by the catch-all route in App.jsx, so a tenant that does not have a portal never downloads its code.
mountPortal(routes);
