import { mountPortal } from '../../src/bootstrap';
import LandingApp from '../../src/LandingApp';
// The landing page is its own small site, with no licence of its own: every company can sign in here and
// is offered only the portals its plan includes. It ships none of the portals' pages.
mountPortal(null, LandingApp);
